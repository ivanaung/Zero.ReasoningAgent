using System.ComponentModel;
using System.IO.Enumeration;
using System.Text.RegularExpressions;
using ScheduleApp.Models.ViewModels;

namespace ScheduleApp.Services;

public interface IZeroLocalToolService
{
    IReadOnlyList<ZeroAssistantActionViewModel> GetActionCatalog();
    bool TryRunDeterministicStorageUsage(string userText, out string replyText, out ZeroAssistantPanelTabViewModel? panelTab);
    bool TryRunDeterministicFileSearch(string userText, out string replyText, out ZeroAssistantPanelTabViewModel? panelTab);
    string FindLocalFiles(string query, string? rootPath = null);
    string AnalyzeStorageUsage(string? rootPath = null);
    ZeroAssistantPanelTabViewModel? TakeLastPanelTab();
}

public class ZeroLocalToolService : IZeroLocalToolService
{
    private const int MaxFileSearchResults = 40;
    private const int MaxStorageUsageItems = 15;
    private static readonly TimeSpan SearchTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan StorageUsageTimeout = TimeSpan.FromSeconds(20);
    private static readonly Regex TrailingRootPattern = new(
        @"\b(?:in|under|inside)\s+(?<root>""[^""]+""|[A-Za-z]:(?:\\[^<>:""/\\|?*\r\n]+)*)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex TrailingDrivePattern = new(
        @"\b(?:in|under|inside|on)\s+(?:(?:the\s+)?)?(?<drive>[A-Za-z])(?::)?\s+drive\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private ZeroAssistantPanelTabViewModel? _lastPanelTab;

    public IReadOnlyList<ZeroAssistantActionViewModel> GetActionCatalog() =>
    [
        new()
        {
            Key = "find-local-files",
            Title = "Find Local Files",
            Description = "Search local drives or a selected root folder and return matching files and folders.",
            PromptHint = "find files report in D:\\Work"
        },
        new()
        {
            Key = "top-storage-usage",
            Title = "Top Storage Usage",
            Description = "Scan a drive or folder and list the largest files and folders.",
            PromptHint = "top storage usage in D drive"
        },
        new()
        {
            Key = "conversation-memory",
            Title = "Use Saved Context",
            Description = "Uses saved Zero memory and recent local conversation history.",
            PromptHint = "remember that my normal project timezone is Auckland"
        }
    ];

    public ZeroAssistantPanelTabViewModel? TakeLastPanelTab()
    {
        var tab = _lastPanelTab;
        _lastPanelTab = null;
        return tab;
    }

    public bool TryRunDeterministicStorageUsage(string userText, out string replyText, out ZeroAssistantPanelTabViewModel? panelTab)
    {
        replyText = string.Empty;
        panelTab = null;
        if (!TryParseStorageUsageRequest(userText, out var rootPath))
        {
            return false;
        }

        var result = AnalyzeStorageUsageInternal(rootPath);
        panelTab = BuildPanelTab(result);
        replyText = BuildStorageUsageReply(result);
        return true;
    }

    public bool TryRunDeterministicFileSearch(string userText, out string replyText, out ZeroAssistantPanelTabViewModel? panelTab)
    {
        replyText = string.Empty;
        panelTab = null;
        if (!TryParseFileSearchRequest(userText, out var query, out var rootPath))
        {
            return false;
        }

        var result = SearchFileSystem(query, rootPath);
        panelTab = BuildPanelTab(result);
        replyText = BuildSearchReply(query, result);
        return true;
    }

    [Description("Find local files or folders by name on the server machine. Use only when the user asks to locate a file, folder, document, report, project, or directory.")]
    public string FindLocalFiles(
        [Description("The file or folder name fragment to search for.")] string query,
        [Description("Optional root folder or drive to search in, for example D:\\Work or C:\\Users.")] string? rootPath = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "No search query was provided.";
        }

        var result = SearchFileSystem(query.Trim(), rootPath);
        _lastPanelTab = BuildPanelTab(result);
        return result.Matches.Count == 0
            ? $"No matching files or folders were found for '{query}'."
            : BuildSearchReply(query, result);
    }

    [Description("Analyze storage usage and return the largest files and folders in a selected local drive or folder.")]
    public string AnalyzeStorageUsage(
        [Description("Optional root folder or drive to scan, for example D:\\, D:\\Projects, or C:\\Users.")] string? rootPath = null)
    {
        var result = AnalyzeStorageUsageInternal(rootPath);
        _lastPanelTab = BuildPanelTab(result);
        return BuildStorageUsageReply(result);
    }

    private static StorageUsageResultViewModel AnalyzeStorageUsageInternal(string? requestedRoot)
    {
        var searchRoots = ResolveSearchRoots(requestedRoot);
        var startedAtUtc = DateTime.UtcNow;
        var timedOut = false;
        var filesScanned = 0;
        long totalBytesScanned = 0;
        var topFiles = new List<StorageUsageItemViewModel>();
        var folderSizes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in searchRoots)
        {
            timedOut = CollectStorageUsage(NormalizeSearchRoot(root), topFiles, folderSizes, ref filesScanned, ref totalBytesScanned, startedAtUtc);
            if (timedOut)
            {
                break;
            }
        }

        var topFolders = folderSizes
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Take(MaxStorageUsageItems)
            .Select(CreateFolderUsageItem)
            .ToList();

        var rootLabel = !string.IsNullOrWhiteSpace(requestedRoot)
            ? NormalizeSearchRoot(requestedRoot)
            : searchRoots.Count == 0 ? "local drives" : string.Join(", ", searchRoots);
        var summary = $"Top storage usage for {rootLabel}.";
        if (timedOut)
        {
            summary += " Scan timed out before every location was fully processed.";
        }

        return new StorageUsageResultViewModel(
            $"Storage: {TrimForTabTitle(rootLabel)}",
            summary,
            searchRoots,
            topFiles.OrderByDescending(item => item.SizeBytes).ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase).Take(MaxStorageUsageItems).ToList(),
            topFolders,
            filesScanned,
            totalBytesScanned,
            timedOut);
    }

    private static FileSearchResultViewModel SearchFileSystem(string query, string? requestedRoot)
    {
        var searchRoots = ResolveSearchRoots(requestedRoot);
        var searchSpec = BuildSearchSpec(query);
        var matches = new List<FileSearchMatchViewModel>();
        var visitedRoots = new List<string>();
        var startedAtUtc = DateTime.UtcNow;
        var timedOut = false;

        foreach (var root in searchRoots)
        {
            visitedRoots.Add(root);
            timedOut = CollectMatches(root, searchSpec, matches, startedAtUtc);
            if (timedOut || matches.Count >= MaxFileSearchResults)
            {
                break;
            }
        }

        var resultLabel = matches.Count == 1 ? "1 match" : $"{matches.Count} matches";
        var rootLabel = !string.IsNullOrWhiteSpace(requestedRoot)
            ? NormalizeSearchRoot(requestedRoot)
            : visitedRoots.Count == 0 ? "local drives" : string.Join(", ", visitedRoots);
        var summary = $"{resultLabel} for \"{query}\" in {rootLabel}.";
        if (timedOut)
        {
            summary += " Search timed out. Narrow the folder or use a more specific pattern.";
        }

        return new FileSearchResultViewModel(
            $"Files: {TrimForTabTitle(query)}",
            summary,
            query,
            visitedRoots,
            matches,
            matches.Count >= MaxFileSearchResults,
            timedOut);
    }

    private static IReadOnlyList<string> ResolveSearchRoots(string? requestedRoot)
    {
        if (!string.IsNullOrWhiteSpace(requestedRoot))
        {
            var normalizedRoot = NormalizeSearchRoot(requestedRoot);
            return Directory.Exists(normalizedRoot) ? [normalizedRoot] : [];
        }

        return DriveInfo.GetDrives()
            .Where(drive => drive.IsReady && drive.DriveType is DriveType.Fixed or DriveType.Removable)
            .Select(drive => drive.RootDirectory.FullName)
            .ToList();
    }

    private static string NormalizeSearchRoot(string requestedRoot)
    {
        var root = requestedRoot.Trim().Trim('"');
        var driveMatch = Regex.Match(root, @"^(?<drive>[A-Za-z])(?::)?\s*drive$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (driveMatch.Success)
        {
            root = $"{driveMatch.Groups["drive"].Value.ToUpperInvariant()}:\\";
        }

        if (Regex.IsMatch(root, @"^[A-Za-z]:$", RegexOptions.CultureInvariant))
        {
            root += "\\";
        }

        return root;
    }

    private static bool CollectMatches(string root, FileSearchSpec searchSpec, List<FileSearchMatchViewModel> matches, DateTime startedAtUtc)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
            AttributesToSkip = FileAttributes.System | FileAttributes.ReparsePoint
        };

        IEnumerable<string> entries;
        try
        {
            entries = Directory.EnumerateFileSystemEntries(root, searchSpec.EnumerationPattern, options);
        }
        catch
        {
            return false;
        }

        foreach (var entry in entries)
        {
            if (DateTime.UtcNow - startedAtUtc >= SearchTimeout)
            {
                return true;
            }

            var name = Path.GetFileName(entry);
            if (string.IsNullOrWhiteSpace(name) || !searchSpec.IsMatch(name))
            {
                continue;
            }

            try
            {
                var attributes = File.GetAttributes(entry);
                var isDirectory = attributes.HasFlag(FileAttributes.Directory);
                long? sizeBytes = null;
                DateTimeOffset? lastModifiedUtc = null;

                if (isDirectory)
                {
                    lastModifiedUtc = new DirectoryInfo(entry).LastWriteTimeUtc;
                }
                else
                {
                    var fileInfo = new FileInfo(entry);
                    sizeBytes = fileInfo.Length;
                    lastModifiedUtc = fileInfo.LastWriteTimeUtc;
                }

                matches.Add(new FileSearchMatchViewModel(name, entry, isDirectory ? "folder" : "file", Path.GetDirectoryName(entry) ?? root, sizeBytes, lastModifiedUtc));
            }
            catch
            {
            }

            if (matches.Count >= MaxFileSearchResults)
            {
                return false;
            }
        }

        return false;
    }

    private static bool CollectStorageUsage(string root, List<StorageUsageItemViewModel> topFiles, Dictionary<string, long> folderSizes, ref int filesScanned, ref long totalBytesScanned, DateTime startedAtUtc)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
            AttributesToSkip = FileAttributes.System | FileAttributes.ReparsePoint
        };

        IEnumerable<string> entries;
        try
        {
            entries = Directory.EnumerateFiles(root, "*", options);
        }
        catch
        {
            return false;
        }

        foreach (var entry in entries)
        {
            if (DateTime.UtcNow - startedAtUtc >= StorageUsageTimeout)
            {
                return true;
            }

            try
            {
                var fileInfo = new FileInfo(entry);
                if (!fileInfo.Exists)
                {
                    continue;
                }

                filesScanned++;
                totalBytesScanned += fileInfo.Length;
                AddTopUsageItem(topFiles, new StorageUsageItemViewModel(fileInfo.Name, fileInfo.FullName, "file", fileInfo.Length, fileInfo.LastWriteTimeUtc));

                var currentDirectory = fileInfo.DirectoryName;
                while (!string.IsNullOrWhiteSpace(currentDirectory) && IsPathWithinRoot(currentDirectory, root))
                {
                    folderSizes[currentDirectory] = folderSizes.TryGetValue(currentDirectory, out var currentSize)
                        ? currentSize + fileInfo.Length
                        : fileInfo.Length;

                    if (string.Equals(currentDirectory.TrimEnd('\\'), root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    currentDirectory = Path.GetDirectoryName(currentDirectory);
                }
            }
            catch
            {
            }
        }

        return false;
    }

    private static ZeroAssistantPanelTabViewModel BuildPanelTab(FileSearchResultViewModel result) => new()
    {
        Id = $"result-{Guid.NewGuid():N}",
        Title = result.TabTitle,
        Kind = "file-search",
        Summary = result.Summary,
        FileSearch = result
    };

    private static ZeroAssistantPanelTabViewModel BuildPanelTab(StorageUsageResultViewModel result) => new()
    {
        Id = $"result-{Guid.NewGuid():N}",
        Title = result.TabTitle,
        Kind = "storage-usage",
        Summary = result.Summary,
        StorageUsage = result
    };

    private static string BuildSearchReply(string query, FileSearchResultViewModel result)
    {
        if (result.TimedOut && result.Matches.Count == 0)
        {
            return $"Search for '{query}' timed out before finding matches. Try a narrower folder like `in D:\\Work`.";
        }

        if (result.Matches.Count == 0)
        {
            return $"No matching files or folders were found for '{query}'.";
        }

        var lines = new List<string>
        {
            $"Found {result.Matches.Count} item{(result.Matches.Count == 1 ? string.Empty : "s")} for '{query}'.",
            "I opened the result in a workspace tab with the path list."
        };

        lines.AddRange(result.Matches.Take(5).Select(match => $"- {match.Path}"));
        if (result.Truncated)
        {
            lines.Add($"Search stopped after the first {MaxFileSearchResults} matches.");
        }

        if (result.TimedOut)
        {
            lines.Add("Search timed out before scanning every location. Narrow the folder if you need deeper results.");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildStorageUsageReply(StorageUsageResultViewModel result)
    {
        var lines = new List<string>
        {
            $"Scanned {result.FilesScanned} file{(result.FilesScanned == 1 ? string.Empty : "s")} and {FormatFileSize(result.TotalBytesScanned)} of data.",
            "I opened a workspace tab with the largest folders and files."
        };

        if (result.TopFolders.Count > 0)
        {
            lines.Add($"Largest folder: {result.TopFolders[0].Path} ({FormatFileSize(result.TopFolders[0].SizeBytes)})");
        }

        if (result.TopFiles.Count > 0)
        {
            lines.Add($"Largest file: {result.TopFiles[0].Path} ({FormatFileSize(result.TopFiles[0].SizeBytes)})");
        }

        if (result.TimedOut)
        {
            lines.Add("Scan timed out before every location was processed.");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static bool TryParseFileSearchRequest(string userText, out string query, out string? rootPath)
    {
        query = string.Empty;
        rootPath = null;
        if (string.IsNullOrWhiteSpace(userText))
        {
            return false;
        }

        var working = userText.Trim();
        var lower = working.ToLowerInvariant();
        if (!lower.Contains("find") && !lower.Contains("search") && !lower.Contains("locate") && !lower.Contains("look for") && !lower.Contains("where is") && !lower.Contains("where are"))
        {
            return false;
        }

        var rootMatch = TrailingRootPattern.Match(working);
        if (rootMatch.Success)
        {
            rootPath = rootMatch.Groups["root"].Value.Trim().Trim('"');
            working = working[..rootMatch.Index].TrimEnd();
        }
        else
        {
            var driveMatch = TrailingDrivePattern.Match(working);
            if (driveMatch.Success)
            {
                rootPath = $"{driveMatch.Groups["drive"].Value.ToUpperInvariant()}:\\";
                working = working[..driveMatch.Index].TrimEnd();
            }
        }

        working = Regex.Replace(working, @"^(?:please\s+)?(?:can you\s+|could you\s+)?(?:find|search(?:\s+for)?|look\s+for|locate|where\s+is|where\s+are)\s+", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        working = Regex.Replace(working, @"^(?:local\s+)?(?:files?|folders?|documents?|directories?)\s+", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        working = Regex.Replace(working, @"\s+(?:files?|folders?|documents?|directories?)\s*$", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        working = working.Trim().Trim('"', '\'', '.', '?', '!');
        if (working.Length < 2)
        {
            return false;
        }

        query = working;
        return true;
    }

    private static bool TryParseStorageUsageRequest(string userText, out string? rootPath)
    {
        rootPath = null;
        if (string.IsNullOrWhiteSpace(userText))
        {
            return false;
        }

        var working = userText.Trim();
        var lower = working.ToLowerInvariant();
        var looksLikeStorageRequest =
            lower.Contains("storage usage") ||
            lower.Contains("disk usage") ||
            lower.Contains("largest files") ||
            lower.Contains("largest folders") ||
            lower.Contains("biggest files") ||
            lower.Contains("biggest folders") ||
            (lower.Contains("top") && lower.Contains("storage"));

        if (!looksLikeStorageRequest)
        {
            return false;
        }

        var rootMatch = TrailingRootPattern.Match(working);
        if (rootMatch.Success)
        {
            rootPath = rootMatch.Groups["root"].Value.Trim().Trim('"');
            return true;
        }

        var driveMatch = TrailingDrivePattern.Match(working);
        if (driveMatch.Success)
        {
            rootPath = $"{driveMatch.Groups["drive"].Value.ToUpperInvariant()}:\\";
        }

        return true;
    }

    private static FileSearchSpec BuildSearchSpec(string query)
    {
        var normalizedQuery = query.Trim().Trim('"');
        if (normalizedQuery.Contains('*') || normalizedQuery.Contains('?'))
        {
            return new FileSearchSpec(normalizedQuery, name => FileSystemName.MatchesSimpleExpression(normalizedQuery, name, ignoreCase: true));
        }

        if (normalizedQuery.StartsWith(".", StringComparison.Ordinal) && normalizedQuery.Count(ch => ch == '.') == 1)
        {
            var extensionPattern = $"*{normalizedQuery}";
            return new FileSearchSpec(extensionPattern, name => name.EndsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase));
        }

        return new FileSearchSpec("*", name => name.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static void AddTopUsageItem(List<StorageUsageItemViewModel> items, StorageUsageItemViewModel candidate)
    {
        items.Add(candidate);
        items.Sort(static (left, right) =>
        {
            var compare = right.SizeBytes.CompareTo(left.SizeBytes);
            return compare != 0 ? compare : StringComparer.OrdinalIgnoreCase.Compare(left.Path, right.Path);
        });

        if (items.Count > MaxStorageUsageItems)
        {
            items.RemoveRange(MaxStorageUsageItems, items.Count - MaxStorageUsageItems);
        }
    }

    private static StorageUsageItemViewModel CreateFolderUsageItem(KeyValuePair<string, long> folder)
    {
        DateTimeOffset? lastModifiedUtc = null;
        try
        {
            var directoryInfo = new DirectoryInfo(folder.Key);
            if (directoryInfo.Exists)
            {
                lastModifiedUtc = directoryInfo.LastWriteTimeUtc;
            }
        }
        catch
        {
        }

        return new StorageUsageItemViewModel(Path.GetFileName(folder.Key.TrimEnd('\\')) is { Length: > 0 } name ? name : folder.Key, folder.Key, "folder", folder.Value, lastModifiedUtc);
    }

    private static bool IsPathWithinRoot(string path, string root)
    {
        var normalizedPath = EnsureTrailingSeparator(path);
        var normalizedRoot = EnsureTrailingSeparator(root);
        return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;

    private static string TrimForTabTitle(string value) => value.Length <= 18 ? value : $"{value[..15]}...";

    private static string FormatFileSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0 ? $"{bytes} {units[unitIndex]}" : $"{value:0.##} {units[unitIndex]}";
    }

    private sealed record FileSearchSpec(string EnumerationPattern, Func<string, bool> IsMatch);
}
