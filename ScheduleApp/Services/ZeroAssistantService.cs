using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ScheduleApp.Models;
using ScheduleApp.Models.ViewModels;

namespace ScheduleApp.Services;

public interface IZeroAssistantService
{
    Task<ZeroAssistantPageViewModel> GetPageModelAsync(CancellationToken cancellationToken = default);
    Task<ZeroConversationHistoryViewModel> GetConversationHistoryAsync(string? conversationId, int limit = 100, CancellationToken cancellationToken = default);
    Task<ZeroAssistantReplyViewModel> SendTextAsync(AiChatRequest request, CancellationToken cancellationToken = default);
    Task<ZeroAssistantReplyViewModel> ProcessVoiceAsync(Stream audioStream, string fileName, string contentType, string? conversationId = null, CancellationToken cancellationToken = default);
    Task<string> TranscribeVoiceAsync(Stream audioStream, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ZeroAssistantPiperVoiceViewModel>> GetPiperVoicesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> AddMemoryAsync(string text, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> RemoveMemoryAsync(string text, CancellationToken cancellationToken = default);
    Task ClearConversationAsync(string conversationId, CancellationToken cancellationToken = default);
}

public class ZeroAssistantService(
    IZeroAssistantDataService dataService,
    IZeroLocalToolService localToolService,
    IProjectManagementAgentService projectManagementAgentService,
    IWebSearchService webSearchService,
    IAiSettingsService aiSettingsService,
    IHttpClientFactory httpClientFactory,
    ILogger<ZeroAssistantService> logger) : IZeroAssistantService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<string, SpeechEndpointProtocol> protocolCache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<ZeroAssistantPageViewModel> GetPageModelAsync(CancellationToken cancellationToken = default)
    {
        var settings = await dataService.GetSettingsInputAsync(cancellationToken);
        var actions = localToolService.GetActionCatalog().ToList();
        actions.AddRange(BuildProgressActionCatalog());

        return new ZeroAssistantPageViewModel
        {
            Actions = actions,
            Memory = await dataService.GetMemoryAsync(cancellationToken),
            Settings = settings
        };
    }

    public async Task<ZeroConversationHistoryViewModel> GetConversationHistoryAsync(string? conversationId, int limit = 100, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return new ZeroConversationHistoryViewModel(string.Empty, []);
        }

        var messages = await dataService.GetHistoryAsync(conversationId, limit, cancellationToken);
        return new ZeroConversationHistoryViewModel(
            conversationId,
            messages.Select(item => new ZeroConversationTurnViewModel(item.Role, item.Content, item.CreatedUtc)).ToList());
    }

    public async Task<ZeroAssistantReplyViewModel> SendTextAsync(AiChatRequest request, CancellationToken cancellationToken = default)
    {
        var text = request.Message.Trim();
        var conversationId = string.IsNullOrWhiteSpace(request.ConversationId)
            ? Guid.NewGuid().ToString("n")
            : request.ConversationId!;

        if (string.IsNullOrWhiteSpace(text))
        {
            return new ZeroAssistantReplyViewModel
            {
                ConversationId = conversationId,
                ReplyText = "Type or say something first.",
                Status = "idle"
            };
        }

        var zeroSettings = await dataService.GetSettingsAsync(cancellationToken);
        var aiSettings = await aiSettingsService.GetAsync(cancellationToken);

        if (TryHandleMemoryCommand(text, out var memoryText))
        {
            await dataService.AddMemoryAsync(memoryText, cancellationToken);
            var reply = $"I saved that to Zero memory: {memoryText}";
            await SaveTurnAsync(conversationId, text, reply, cancellationToken);
            return await AddSpeechAsync(BuildReply(conversationId, text, reply, aiSettings.ProviderType.ToString(), aiSettings.ModelId, null), zeroSettings, cancellationToken);
        }

        if (zeroSettings.EnableLocalFileTools &&
            localToolService.TryRunDeterministicStorageUsage(text, out var storageReply, out var storagePanel))
        {
            await SaveTurnAsync(conversationId, text, storageReply, cancellationToken);
            return await AddSpeechAsync(BuildReply(conversationId, text, storageReply, aiSettings.ProviderType.ToString(), aiSettings.ModelId, storagePanel), zeroSettings, cancellationToken);
        }

        if (zeroSettings.EnableLocalFileTools &&
            localToolService.TryRunDeterministicFileSearch(text, out var searchReply, out var searchPanel))
        {
            await SaveTurnAsync(conversationId, text, searchReply, cancellationToken);
            return await AddSpeechAsync(BuildReply(conversationId, text, searchReply, aiSettings.ProviderType.ToString(), aiSettings.ModelId, searchPanel), zeroSettings, cancellationToken);
        }

        if (ShouldUseDeterministicWebSearch(text))
        {
            var searchResponse = await webSearchService.SearchAsync(text, 5, cancellationToken);
            var webReply = BuildWebSearchReply(searchResponse);
            await SaveTurnAsync(conversationId, text, webReply, cancellationToken);
            return await AddSpeechAsync(
                BuildReply(
                    conversationId,
                    text,
                    webReply,
                    aiSettings.ProviderType.ToString(),
                    aiSettings.ModelId,
                    BuildWebSearchPanel(searchResponse)),
                zeroSettings,
                cancellationToken);
        }

        var effectiveMessage = await BuildEffectiveMessageAsync(text, conversationId, zeroSettings.HistoryLimit, cancellationToken);
        var progressResponse = await projectManagementAgentService.SendAsync(new AiChatRequest
        {
            Message = effectiveMessage,
            ConversationId = conversationId,
            Stream = request.Stream
        }, cancellationToken);

        await SaveTurnAsync(conversationId, text, progressResponse.Message, cancellationToken);
        return await AddSpeechAsync(new ZeroAssistantReplyViewModel
        {
            ConversationId = conversationId,
            UserText = text,
            ReplyText = progressResponse.Message,
            Status = "idle",
            ProviderLabel = progressResponse.ProviderLabel,
            ModelId = progressResponse.ModelId
        }, zeroSettings, cancellationToken);
    }

    public async Task<ZeroAssistantReplyViewModel> ProcessVoiceAsync(Stream audioStream, string fileName, string contentType, string? conversationId = null, CancellationToken cancellationToken = default)
    {
        var transcript = await TranscribeVoiceAsync(audioStream, fileName, contentType, cancellationToken);
        if (IsNoSpeechTranscript(transcript))
        {
            return new ZeroAssistantReplyViewModel
            {
                ConversationId = string.IsNullOrWhiteSpace(conversationId) ? Guid.NewGuid().ToString("n") : conversationId,
                Status = "idle"
            };
        }

        var reply = await SendTextAsync(new AiChatRequest
        {
            Message = transcript,
            ConversationId = conversationId
        }, cancellationToken);

        return reply;
    }

    public async Task<string> TranscribeVoiceAsync(Stream audioStream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var settings = await dataService.GetSettingsAsync(cancellationToken);
        if (!settings.EnableVoice)
        {
            throw new InvalidOperationException("Zero voice assistant is disabled in settings.");
        }

        var protocol = await DetectSpeechProtocolAsync(settings.WhisperUrl, cancellationToken);
        if (protocol == SpeechEndpointProtocol.Wyoming)
        {
            return await TranscribeWyomingAsync(settings.WhisperUrl, audioStream, cancellationToken);
        }

        return await TranscribeHttpAsync(audioStream, fileName, contentType, settings, cancellationToken);
    }

    public async Task<IReadOnlyList<ZeroAssistantPiperVoiceViewModel>> GetPiperVoicesAsync(CancellationToken cancellationToken = default)
    {
        var settings = await dataService.GetSettingsAsync(cancellationToken);
        var protocol = await DetectSpeechProtocolAsync(settings.PiperUrl, cancellationToken);
        if (protocol != SpeechEndpointProtocol.Wyoming)
        {
            return [];
        }

        try
        {
            return await DescribeWyomingVoicesAsync(settings.PiperUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Zero Piper voice discovery failed.");
            return [];
        }
    }

    public Task<IReadOnlyList<string>> AddMemoryAsync(string text, CancellationToken cancellationToken = default) =>
        dataService.AddMemoryAsync(text, cancellationToken);

    public Task<IReadOnlyList<string>> RemoveMemoryAsync(string text, CancellationToken cancellationToken = default) =>
        dataService.RemoveMemoryAsync(text, cancellationToken);

    public Task ClearConversationAsync(string conversationId, CancellationToken cancellationToken = default) =>
        dataService.ClearConversationAsync(conversationId, cancellationToken);

    private async Task<string> BuildEffectiveMessageAsync(string userText, string conversationId, int historyLimit, CancellationToken cancellationToken)
    {
        var memory = await dataService.GetMemoryAsync(cancellationToken);
        var history = await dataService.GetHistoryAsync(conversationId, historyLimit, cancellationToken);
        var parts = new List<string>
        {
            "Zero Assistant context for this local-first reasoning workspace:"
        };

        if (memory.Count > 0)
        {
            parts.Add("Saved Zero memory:");
            parts.AddRange(memory.Select(item => $"- {item}"));
        }

        if (history.Count > 0)
        {
            parts.Add("Recent Zero conversation:");
            parts.AddRange(history.Select(item => $"{item.Role}: {item.Content}"));
        }

        parts.Add("Use Zero tools for live tasks, events, calendar, email, notifications, memory, search, and finance data.");
        parts.Add("Use web_search only for public live web information such as latest news, current versions, prices, public documentation, and public websites.");
        parts.Add($"Current user request: {userText}");
        return string.Join(Environment.NewLine, parts);
    }

    private async Task SaveTurnAsync(string conversationId, string userText, string replyText, CancellationToken cancellationToken)
    {
        await dataService.AddConversationTurnAsync(conversationId, "user", userText, cancellationToken);
        await dataService.AddConversationTurnAsync(conversationId, "assistant", replyText, cancellationToken);
    }

    private async Task<ZeroAssistantReplyViewModel> AddSpeechAsync(ZeroAssistantReplyViewModel reply, ZeroAssistantSettings settings, CancellationToken cancellationToken)
    {
        if (!settings.EnableVoice || string.IsNullOrWhiteSpace(reply.ReplyText))
        {
            return reply;
        }

        try
        {
            reply.AudioDataUrl = await SpeakAsync(reply.ReplyText, settings, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Zero TTS failed.");
        }

        return reply;
    }

    private async Task<string?> SpeakAsync(string text, ZeroAssistantSettings settings, CancellationToken cancellationToken)
    {
        var protocol = await DetectSpeechProtocolAsync(settings.PiperUrl, cancellationToken);
        if (protocol == SpeechEndpointProtocol.Wyoming)
        {
            var tts = await SpeakWyomingAsync(settings.PiperUrl, text, settings.PiperVoice, cancellationToken);
            return BuildDataUrl(tts.Audio, tts.ContentType);
        }

        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds);
        var endpoint = $"{settings.PiperUrl.TrimEnd('/')}/{settings.PiperEndpoint.TrimStart('/')}";
        using var response = await client.PostAsJsonAsync(endpoint, new { text }, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.Length == 0)
        {
            return null;
        }

        return BuildDataUrl(bytes, response.Content.Headers.ContentType?.MediaType ?? "audio/wav");
    }

    private async Task<string> TranscribeHttpAsync(Stream audioStream, string fileName, string contentType, ZeroAssistantSettings settings, CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        using var audio = new StreamContent(audioStream);
        audio.Headers.ContentType = MediaTypeHeaderValue.Parse(string.IsNullOrWhiteSpace(contentType) ? "audio/wav" : contentType);
        content.Add(audio, "audio_file", string.IsNullOrWhiteSpace(fileName) ? "recording.wav" : fileName);

        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds);
        var endpoint = $"{settings.WhisperUrl.TrimEnd('/')}/asr?output=text&task=transcribe&encode=true";
        using var response = await client.PostAsync(endpoint, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var text = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
        return IsNoSpeechTranscript(text) ? string.Empty : text;
    }

    private async Task<SpeechEndpointProtocol> DetectSpeechProtocolAsync(string endpointUrl, CancellationToken cancellationToken)
    {
        if (protocolCache.TryGetValue(endpointUrl, out var cached))
        {
            return cached;
        }

        if (!TryParseEndpointUri(endpointUrl, out var uri))
        {
            protocolCache[endpointUrl] = SpeechEndpointProtocol.Http;
            return SpeechEndpointProtocol.Http;
        }

        try
        {
            using var tcp = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            await tcp.ConnectAsync(uri.Host, uri.Port, timeout.Token);
            await using var stream = tcp.GetStream();

            await SendWyomingEventAsync(stream, new WyomingEvent("describe"), timeout.Token);
            var reply = await ReadWyomingEventAsync(stream, timeout.Token);
            if (reply is not null && string.Equals(reply.Type, "info", StringComparison.OrdinalIgnoreCase))
            {
                protocolCache[endpointUrl] = SpeechEndpointProtocol.Wyoming;
                return SpeechEndpointProtocol.Wyoming;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Endpoint {EndpointUrl} did not respond as Wyoming.", endpointUrl);
        }

        protocolCache[endpointUrl] = SpeechEndpointProtocol.Http;
        return SpeechEndpointProtocol.Http;
    }

    private async Task<string> TranscribeWyomingAsync(string endpointUrl, Stream audioStream, CancellationToken cancellationToken)
    {
        var wav = await ReadWavAudioAsync(audioStream, cancellationToken);

        using var tcp = await ConnectWyomingAsync(endpointUrl, cancellationToken);
        await using var stream = tcp.GetStream();

        await SendWyomingEventAsync(stream, new WyomingEvent("transcribe"), cancellationToken);
        await SendWyomingEventAsync(stream, new WyomingEvent("audio-start", new Dictionary<string, object?>
        {
            ["rate"] = wav.SampleRate,
            ["width"] = wav.BytesPerSample,
            ["channels"] = wav.Channels
        }), cancellationToken);

        const int chunkSize = 4096;
        var offset = 0;
        while (offset < wav.PcmBytes.Length)
        {
            var count = Math.Min(chunkSize, wav.PcmBytes.Length - offset);
            var chunk = new byte[count];
            Buffer.BlockCopy(wav.PcmBytes, offset, chunk, 0, count);
            await SendWyomingEventAsync(stream, new WyomingEvent("audio-chunk", new Dictionary<string, object?>
            {
                ["rate"] = wav.SampleRate,
                ["width"] = wav.BytesPerSample,
                ["channels"] = wav.Channels
            }, chunk), cancellationToken);
            offset += count;
        }

        await SendWyomingEventAsync(stream, new WyomingEvent("audio-stop"), cancellationToken);

        while (true)
        {
            var reply = await ReadWyomingEventAsync(stream, cancellationToken)
                ?? throw new InvalidOperationException("Whisper service closed the Wyoming stream without a transcript.");

            if (string.Equals(reply.Type, "transcript", StringComparison.OrdinalIgnoreCase))
            {
                var text = reply.GetString("text");
                return IsNoSpeechTranscript(text) ? string.Empty : text!;
            }

            if (string.Equals(reply.Type, "error", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(reply.GetString("message") ?? "Wyoming Whisper returned an error.");
            }
        }
    }

    private async Task<IReadOnlyList<ZeroAssistantPiperVoiceViewModel>> DescribeWyomingVoicesAsync(string endpointUrl, CancellationToken cancellationToken)
    {
        using var tcp = await ConnectWyomingAsync(endpointUrl, cancellationToken);
        await using var stream = tcp.GetStream();

        await SendWyomingEventAsync(stream, new WyomingEvent("describe"), cancellationToken);
        var reply = await ReadWyomingEventAsync(stream, cancellationToken);
        if (reply is null || !string.Equals(reply.Type, "info", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return ExtractPiperVoices(reply);
    }

    private static IReadOnlyList<ZeroAssistantPiperVoiceViewModel> ExtractPiperVoices(WyomingEvent info)
    {
        if (!info.Data.TryGetValue("tts", out var tts) || tts.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var voices = new List<ZeroAssistantPiperVoiceViewModel>();
        foreach (var service in tts.EnumerateArray())
        {
            if (!service.TryGetProperty("voices", out var serviceVoices) || serviceVoices.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var voice in serviceVoices.EnumerateArray())
            {
                var name = voice.TryGetProperty("name", out var nameProperty) ? nameProperty.GetString() : null;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var description = voice.TryGetProperty("description", out var descriptionProperty) ? descriptionProperty.GetString() : null;
                var language = GetFirstString(voice, "languages");
                var labelParts = new[] { description, language, name }
                    .Where(part => !string.IsNullOrWhiteSpace(part))
                    .Distinct(StringComparer.OrdinalIgnoreCase);
                voices.Add(new ZeroAssistantPiperVoiceViewModel(name, string.Join(" - ", labelParts)));
            }
        }

        return voices
            .DistinctBy(voice => voice.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(voice => voice.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? GetFirstString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            return property.GetString();
        }

        return property.ValueKind == JsonValueKind.Array
            ? property.EnumerateArray().FirstOrDefault(item => item.ValueKind == JsonValueKind.String).GetString()
            : null;
    }

    private async Task<TtsResult> SpeakWyomingAsync(string endpointUrl, string text, string? voiceName, CancellationToken cancellationToken)
    {
        using var tcp = await ConnectWyomingAsync(endpointUrl, cancellationToken);
        await using var stream = tcp.GetStream();

        var synthesizeData = new Dictionary<string, object?>
        {
            ["text"] = text
        };
        if (!string.IsNullOrWhiteSpace(voiceName))
        {
            synthesizeData["voice"] = new Dictionary<string, object?>
            {
                ["name"] = voiceName.Trim()
            };
        }

        await SendWyomingEventAsync(stream, new WyomingEvent("synthesize", synthesizeData), cancellationToken);

        var format = new WyomingAudioFormat(22050, 2, 1);
        using var pcm = new MemoryStream();

        while (true)
        {
            var reply = await ReadWyomingEventAsync(stream, cancellationToken)
                ?? throw new InvalidOperationException("Piper service closed the Wyoming stream without audio.");

            if (string.Equals(reply.Type, "audio-start", StringComparison.OrdinalIgnoreCase))
            {
                format = new WyomingAudioFormat(
                    reply.GetInt32("rate") ?? format.SampleRate,
                    reply.GetInt32("width") ?? format.BytesPerSample,
                    reply.GetInt32("channels") ?? format.Channels);
                continue;
            }

            if (string.Equals(reply.Type, "audio-chunk", StringComparison.OrdinalIgnoreCase))
            {
                if (reply.Payload is { Length: > 0 })
                {
                    await pcm.WriteAsync(reply.Payload, cancellationToken);
                }
                continue;
            }

            if (string.Equals(reply.Type, "audio-stop", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (string.Equals(reply.Type, "error", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(reply.GetString("message") ?? "Wyoming Piper returned an error.");
            }
        }

        var wav = BuildWavFromPcm(pcm.ToArray(), format.SampleRate, format.BytesPerSample, format.Channels);
        return new TtsResult(wav, "audio/wav");
    }

    private static async Task<WavAudioData> ReadWavAudioAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return ParseWavAudio(buffer.ToArray());
    }

    private static WavAudioData ParseWavAudio(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

        if (bytes.Length < 44 || reader.ReadUInt32() != 0x46464952 || reader.ReadUInt32() < 36 || reader.ReadUInt32() != 0x45564157)
        {
            throw new InvalidOperationException("Unsupported WAV audio format.");
        }

        short channels = 1;
        var sampleRate = 16000;
        short bitsPerSample = 16;
        byte[]? pcmData = null;

        while (stream.Position + 8 <= stream.Length)
        {
            var chunkId = reader.ReadUInt32();
            var chunkSize = reader.ReadInt32();
            if (chunkSize < 0 || stream.Position + chunkSize > stream.Length)
            {
                throw new InvalidOperationException("Invalid WAV chunk size.");
            }

            if (chunkId == 0x20746D66)
            {
                _ = reader.ReadInt16();
                channels = reader.ReadInt16();
                sampleRate = reader.ReadInt32();
                _ = reader.ReadInt32();
                _ = reader.ReadInt16();
                bitsPerSample = reader.ReadInt16();

                var remaining = chunkSize - 16;
                if (remaining > 0)
                {
                    reader.ReadBytes(remaining);
                }
            }
            else if (chunkId == 0x61746164)
            {
                pcmData = reader.ReadBytes(chunkSize);
            }
            else
            {
                reader.ReadBytes(chunkSize);
            }

            if ((chunkSize & 1) == 1 && stream.Position < stream.Length)
            {
                stream.Position += 1;
            }
        }

        if (pcmData is null || pcmData.Length == 0)
        {
            throw new InvalidOperationException("WAV audio does not contain a data chunk.");
        }

        return new WavAudioData(pcmData, sampleRate, Math.Max(1, bitsPerSample / 8), channels);
    }

    private static byte[] BuildWavFromPcm(byte[] pcmBytes, int sampleRate, int bytesPerSample, int channels)
    {
        var bitsPerSample = bytesPerSample * 8;
        var bytesPerSecond = sampleRate * bytesPerSample * channels;
        var blockAlign = bytesPerSample * channels;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + pcmBytes.Length);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(bytesPerSecond);
        writer.Write((short)blockAlign);
        writer.Write((short)bitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(pcmBytes.Length);
        writer.Write(pcmBytes);
        writer.Flush();
        return stream.ToArray();
    }

    private async Task<TcpClient> ConnectWyomingAsync(string endpointUrl, CancellationToken cancellationToken)
    {
        if (!TryParseEndpointUri(endpointUrl, out var uri))
        {
            throw new InvalidOperationException($"Invalid endpoint URL: {endpointUrl}");
        }

        var tcp = new TcpClient();
        try
        {
            await tcp.ConnectAsync(uri.Host, uri.Port, cancellationToken);
            return tcp;
        }
        catch
        {
            tcp.Dispose();
            throw;
        }
    }

    private static bool TryParseEndpointUri(string endpointUrl, out Uri uri)
    {
        if (Uri.TryCreate(endpointUrl, UriKind.Absolute, out uri!))
        {
            return true;
        }

        return Uri.TryCreate($"tcp://{endpointUrl}", UriKind.Absolute, out uri!);
    }

    private static async Task SendWyomingEventAsync(NetworkStream stream, WyomingEvent evt, CancellationToken cancellationToken)
    {
        var header = new Dictionary<string, object?>
        {
            ["type"] = evt.Type
        };

        if (evt.Data.Count > 0)
        {
            header["data"] = evt.Data;
        }

        if (evt.Payload is { Length: > 0 })
        {
            header["payload_length"] = evt.Payload.Length;
        }

        var headerBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(header) + "\n");
        await stream.WriteAsync(headerBytes, cancellationToken);

        if (evt.Payload is { Length: > 0 })
        {
            await stream.WriteAsync(evt.Payload, cancellationToken);
        }

        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<WyomingEvent?> ReadWyomingEventAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var headerBytes = await ReadLineBytesAsync(stream, cancellationToken);
        if (headerBytes.Length == 0)
        {
            return null;
        }

        using var headerDoc = JsonDocument.Parse(headerBytes);
        var root = headerDoc.RootElement;
        var type = root.GetProperty("type").GetString() ?? "";
        var payloadLength = root.TryGetProperty("payload_length", out var payloadProp) ? payloadProp.GetInt32() : 0;
        var dataLength = root.TryGetProperty("data_length", out var dataProp) ? dataProp.GetInt32() : 0;

        Dictionary<string, JsonElement> mergedData = new(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("data", out var inlineData) && inlineData.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in inlineData.EnumerateObject())
            {
                mergedData[property.Name] = property.Value.Clone();
            }
        }

        if (dataLength > 0)
        {
            var extraDataBytes = await ReadExactAsync(stream, dataLength, cancellationToken);
            using var extraDoc = JsonDocument.Parse(extraDataBytes);
            if (extraDoc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in extraDoc.RootElement.EnumerateObject())
                {
                    mergedData[property.Name] = property.Value.Clone();
                }
            }
        }

        byte[]? payload = null;
        if (payloadLength > 0)
        {
            payload = await ReadExactAsync(stream, payloadLength, cancellationToken);
        }

        return new WyomingEvent(type, mergedData, payload);
    }

    private static async Task<byte[]> ReadLineBytesAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var oneByte = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(oneByte, cancellationToken);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (oneByte[0] == (byte)'\n')
            {
                return buffer.ToArray();
            }

            buffer.WriteByte(oneByte[0]);
        }
    }

    private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int length, CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken);
            if (read == 0)
            {
                throw new IOException("Unexpected end of stream.");
            }

            offset += read;
        }

        return buffer;
    }

    private static string? BuildDataUrl(byte[]? audio, string? contentType)
    {
        if (audio is null || audio.Length == 0)
        {
            return null;
        }

        return $"data:{contentType ?? "audio/wav"};base64,{Convert.ToBase64String(audio)}";
    }

    private static bool TryHandleMemoryCommand(string text, out string memoryText)
    {
        memoryText = string.Empty;
        var prefixes = new[] { "remember that ", "remember ", "save memory ", "add memory " };
        var match = prefixes.FirstOrDefault(prefix => text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (match == null)
        {
            return false;
        }

        memoryText = text[match.Length..].Trim();
        return !string.IsNullOrWhiteSpace(memoryText);
    }

    private static bool ShouldUseDeterministicWebSearch(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (Regex.IsMatch(text, @"\b(project|task|meeting|reminder|gmail|inbox|calendar|file|folder|directory|drive|memory)\b", RegexOptions.IgnoreCase))
        {
            return false;
        }

        return Regex.IsMatch(text, @"\b(latest|current|recent|news|price|prices|law|laws|rule|rules|version|versions|documentation|docs|website|web site|search google|look up|lookup|find online|search online|search web)\b", RegexOptions.IgnoreCase);
    }

    private static string BuildWebSearchReply(WebSearchResponse response)
    {
        if (!response.Success || response.Results.Count == 0)
        {
            var reason = string.IsNullOrWhiteSpace(response.ErrorMessage)
                ? "The search results were not strong enough to answer reliably."
                : response.ErrorMessage;
            return $"{reason} Try a narrower public web query.";
        }

        var lines = new List<string>
        {
            $"I searched the public web for \"{response.Query}\"."
        };

        var top = response.Results.Take(3).ToList();
        foreach (var item in top)
        {
            var snippet = string.IsNullOrWhiteSpace(item.Snippet) ? "No snippet returned." : item.Snippet.Trim();
            lines.Add($"- {item.Title}: {snippet} ({item.Url})");
        }

        if (response.Results.Count < 2)
        {
            lines.Add("Result coverage is limited. A more specific query may produce better evidence.");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static ZeroAssistantPanelTabViewModel BuildWebSearchPanel(WebSearchResponse response) => new()
    {
        Id = $"result-{Guid.NewGuid():N}",
        Title = $"Web: {TrimForPanelTitle(response.Query)}",
        Kind = "web-search",
        Summary = response.Success
            ? $"Found {response.Results.Count} public web results in {response.ElapsedMilliseconds} ms."
            : response.ErrorMessage ?? "Web search did not return usable results.",
        WebSearch = new WebSearchPanelViewModel(
            response.Query,
            response.Results
                .Select(item => new WebSearchPanelResultViewModel(item.Title, item.Url, item.Snippet, item.Source))
                .ToList(),
            response.Success,
            response.ErrorMessage,
            response.ElapsedMilliseconds)
    };

    private static string TrimForPanelTitle(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? "Search"
            : value.Length <= 18 ? value : $"{value[..15]}...";

    private static bool IsNoSpeechTranscript(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var normalized = Regex.Replace(text.Trim().ToLowerInvariant(), @"[^\p{L}\p{N}\s]", "");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return true;
        }

        return normalized is "i could not hear anything clearly"
            or "could not hear anything clearly"
            or "you"
            or "thank you"
            or "thanks"
            or "thanks for watching"
            or "bye"
            or "goodbye"
            or "subscribe";
    }

    private static ZeroAssistantReplyViewModel BuildReply(
        string conversationId,
        string userText,
        string replyText,
        string provider,
        string? modelId,
        ZeroAssistantPanelTabViewModel? panelTab) => new()
        {
            ConversationId = conversationId,
            UserText = userText,
            ReplyText = replyText,
            Status = "idle",
            ProviderLabel = provider,
            ModelId = modelId,
            PanelTab = panelTab
        };

    private static IReadOnlyList<ZeroAssistantActionViewModel> BuildProgressActionCatalog() =>
    [
        new() { Key = "progress-project-summary", Title = "Task Summary", Description = "Ask about task status, overdue work, blocked items, and progress.", PromptHint = "show my active task summary" },
        new() { Key = "progress-create-task", Title = "Create Task", Description = "Create or schedule Zero tasks through the existing scheduling rules.", PromptHint = "create task deployment prep tomorrow at 9am" },
        new() { Key = "progress-calendar", Title = "Calendar Context", Description = "Ask about assigned work, due soon tasks, or calendar windows.", PromptHint = "what is due this week" },
        new() { Key = "progress-google", Title = "Email & Calendar", Description = "Use linked email and calendar data when configured.", PromptHint = "summarize my latest inbox messages" },
        new() { Key = "progress-finance", Title = "Finance Tools", Description = "Ask for finance summaries, upcoming bills, or project finance status.", PromptHint = "show this month's business expense summary" }
    ];

    private enum SpeechEndpointProtocol
    {
        Http,
        Wyoming
    }

    private sealed record TtsResult(byte[] Audio, string ContentType);

    private sealed record WyomingAudioFormat(int SampleRate, int BytesPerSample, int Channels);

    private sealed record WavAudioData(byte[] PcmBytes, int SampleRate, int BytesPerSample, int Channels);

    private sealed record WyomingEvent(string Type, Dictionary<string, JsonElement>? JsonData = null, byte[]? Payload = null)
    {
        public WyomingEvent(string type, Dictionary<string, object?> data)
            : this(type, data.ToDictionary(
                pair => pair.Key,
                pair => JsonSerializer.SerializeToElement(pair.Value, JsonOptions),
                StringComparer.OrdinalIgnoreCase), null)
        {
        }

        public WyomingEvent(string type, Dictionary<string, object?> data, byte[] payload)
            : this(type, data.ToDictionary(
                pair => pair.Key,
                pair => JsonSerializer.SerializeToElement(pair.Value, JsonOptions),
                StringComparer.OrdinalIgnoreCase), payload)
        {
        }

        public Dictionary<string, JsonElement> Data => JsonData ?? [];

        public string? GetString(string key)
        {
            return Data.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        public int? GetInt32(string key)
        {
            if (!Data.TryGetValue(key, out var value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            {
                return number;
            }

            return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number)
                ? number
                : null;
        }
    }
}
