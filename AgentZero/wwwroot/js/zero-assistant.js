(function () {
  const shell = document.querySelector('[data-zero-assistant]');
  if (!shell) {
    return;
  }

  const messages = shell.querySelector('[data-zero-messages]');
  const form = shell.querySelector('[data-zero-form]');
  const input = shell.querySelector('[data-zero-input]');
  const meta = shell.querySelector('[data-zero-meta]');
  const status = shell.querySelector('[data-zero-status] span');
  const avatar = shell.querySelector('[data-zero-avatar]');
  const avatarStage = shell.querySelector('.zero-avatar-stage');
  const avatarStatus = shell.querySelector('[data-zero-avatar-status]');
  const inputIndicator = shell.querySelector('[data-zero-input-indicator]');
  const recordButton = shell.querySelector('[data-zero-record]');
  const listenButton = shell.querySelector('[data-zero-listen]');
  const voiceHint = shell.querySelector('[data-zero-voice-hint] span');
  const muteButton = shell.querySelector('[data-zero-mute]');
  const clearButton = shell.querySelector('[data-zero-clear]');
  const voiceSelect = shell.querySelector('[data-zero-voice]');
  const resultsPanel = shell.querySelector('[data-zero-results]');
  const resultTitle = shell.querySelector('[data-zero-result-title]');
  const resultBody = shell.querySelector('[data-zero-result-body]');
  const resultClose = shell.querySelector('[data-zero-result-close]');
  const chatUrl = shell.dataset.chatUrl;
  const historyUrl = shell.dataset.historyUrl;
  const voiceUrl = shell.dataset.voiceUrl;
  const speechRate = Number(shell.dataset.speechRate || '0.94');
  const speechPitch = Number(shell.dataset.speechPitch || '0.98');
  let conversationId = localStorage.getItem('progress-zero-conversation-id') || '';
  let recorder = null;
  let chunks = [];
  let recordingStream = null;
  let audioContext = null;
  let audioSource = null;
  let audioAnalyser = null;
  let audioProcessor = null;
  let inputAnimationFrame = 0;
  let audioSampleRate = 44100;
  let recordingStartedAt = 0;
  let muted = false;
  let sending = false;
  let speaking = false;
  let continuousRestartTimer = 0;
  let continuousListen = false;
  let speechDetected = false;
  let speechFrames = 0;
  let silenceFrames = 0;
  let autoStopRequested = false;
  let recordingMode = 'manual';
  const voiceStorageKey = 'progress-zero-browser-voice';
  const preferredAudioSampleRate = 16000;
  const maxPreSpeechChunks = 24;

  function setStatus(text, listening) {
    status.textContent = text;
    avatarStatus.textContent = text;
    avatar.classList.toggle('listening', Boolean(listening));
    avatarStage?.classList.toggle('listening', Boolean(listening));
  }

  function setSpeaking(state) {
    const isSpeaking = Boolean(state);
    speaking = isSpeaking;
    avatarStage?.classList.toggle('speaking', isSpeaking);
    avatar.classList.toggle('speaking', isSpeaking);
  }

  function markSpeaking(isSpeaking) {
    setSpeaking(isSpeaking);
    if (!speaking) {
      scheduleContinuousRestart();
    }
  }

  function addMessage(role, text) {
    const row = document.createElement('article');
    row.className = `zero-message ${role}`;
    const bubble = document.createElement('div');
    bubble.className = 'zero-bubble';
    if (role === 'assistant') {
      bubble.classList.add('zero-markdown');
      bubble.innerHTML = renderMarkdown(text);
    } else {
      bubble.textContent = text;
    }
    row.appendChild(bubble);
    messages.appendChild(row);
    messages.scrollTop = messages.scrollHeight;
  }

  function renderConversation(messagesList) {
    messages.innerHTML = '';
    if (!Array.isArray(messagesList) || messagesList.length === 0) {
      addMessage('assistant', 'Ready. Type a request or use the microphone.');
      return;
    }

    messagesList.forEach(function (message) {
      addMessage(message.role || 'assistant', message.content || '');
    });
  }

  async function loadConversationHistory() {
    if (!conversationId || !historyUrl) {
      renderConversation([]);
      return;
    }

    try {
      const url = new URL(historyUrl, window.location.origin);
      url.searchParams.set('conversationId', conversationId);
      url.searchParams.set('limit', '100');
      const response = await fetch(url.toString(), {
        method: 'GET',
        headers: { Accept: 'application/json' }
      });

      if (!response.ok) {
        throw new Error('Could not load Zero conversation history.');
      }

      const payload = await response.json();
      const loadedConversationId = payload.conversationId || conversationId;
      const historyMessages = Array.isArray(payload.messages) ? payload.messages : [];

      if (loadedConversationId) {
        conversationId = loadedConversationId;
        localStorage.setItem('progress-zero-conversation-id', loadedConversationId);
      }

      if (historyMessages.length === 0) {
        conversationId = '';
        localStorage.removeItem('progress-zero-conversation-id');
      }

      renderConversation(historyMessages);
    } catch {
      renderConversation([]);
      meta.textContent = 'Could not reload previous Zero conversation';
    }
  }

  function renderMarkdown(value) {
    const lines = String(value || '').replace(/\r\n/g, '\n').split('\n');
    const blocks = [];
    let paragraph = [];
    let index = 0;

    function flushParagraph() {
      if (paragraph.length === 0) {
        return;
      }

      blocks.push(`<p>${renderInline(paragraph.join(' ').trim())}</p>`);
      paragraph = [];
    }

    while (index < lines.length) {
      const line = lines[index];
      if (!line.trim()) {
        flushParagraph();
        index++;
        continue;
      }

      if (line.trim().startsWith('```')) {
        flushParagraph();
        const code = [];
        index++;
        while (index < lines.length && !lines[index].trim().startsWith('```')) {
          code.push(lines[index]);
          index++;
        }
        if (index < lines.length) {
          index++;
        }
        blocks.push(`<pre><code>${escapeHtml(code.join('\n'))}</code></pre>`);
        continue;
      }

      if (isMarkdownTable(lines, index)) {
        flushParagraph();
        const table = [lines[index], lines[index + 1]];
        index += 2;
        while (index < lines.length && looksLikeTableRow(lines[index])) {
          table.push(lines[index]);
          index++;
        }
        blocks.push(renderTable(table));
        continue;
      }

      const heading = line.match(/^(#{1,4})\s+(.+)$/);
      if (heading) {
        flushParagraph();
        blocks.push(`<h${heading[1].length}>${renderInline(heading[2].trim())}</h${heading[1].length}>`);
        index++;
        continue;
      }

      if (/^\s*[-*]\s+/.test(line)) {
        flushParagraph();
        const items = [];
        while (index < lines.length && /^\s*[-*]\s+/.test(lines[index])) {
          items.push(lines[index].replace(/^\s*[-*]\s+/, ''));
          index++;
        }
        blocks.push(`<ul>${items.map(function (item) { return `<li>${renderInline(item)}</li>`; }).join('')}</ul>`);
        continue;
      }

      if (/^\s*\d+\.\s+/.test(line)) {
        flushParagraph();
        const items = [];
        while (index < lines.length && /^\s*\d+\.\s+/.test(lines[index])) {
          items.push(lines[index].replace(/^\s*\d+\.\s+/, ''));
          index++;
        }
        blocks.push(`<ol>${items.map(function (item) { return `<li>${renderInline(item)}</li>`; }).join('')}</ol>`);
        continue;
      }

      paragraph.push(line);
      index++;
    }

    flushParagraph();
    return blocks.join('');
  }

  function isMarkdownTable(lines, index) {
    return index + 1 < lines.length &&
      looksLikeTableRow(lines[index]) &&
      /^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?\s*$/.test(lines[index + 1]);
  }

  function looksLikeTableRow(line) {
    return line.includes('|') && line.split('|').length > 2;
  }

  function renderTable(lines) {
    const headers = splitTableRow(lines[0]);
    const rows = lines.slice(2).map(splitTableRow);
    return [
      '<div class="zero-table-wrap"><table>',
      '<thead><tr>',
      headers.map(function (cell) { return `<th>${renderInline(cell)}</th>`; }).join(''),
      '</tr></thead><tbody>',
      rows.map(function (row) {
        return `<tr>${headers.map(function (_, cellIndex) {
          return `<td>${renderInline(row[cellIndex] || '')}</td>`;
        }).join('')}</tr>`;
      }).join(''),
      '</tbody></table></div>'
    ].join('');
  }

  function splitTableRow(line) {
    return line
      .trim()
      .replace(/^\|/, '')
      .replace(/\|$/, '')
      .split('|')
      .map(function (cell) { return cell.trim(); });
  }

  function renderInline(value) {
    return escapeHtml(value)
      .replace(/`([^`]+)`/g, '<code>$1</code>')
      .replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
      .replace(/\*([^*]+)\*/g, '<em>$1</em>');
  }

  function speak(text, audioDataUrl) {
    if (muted) {
      return;
    }

    if (audioDataUrl) {
      const audio = new Audio(audioDataUrl);
      markSpeaking(true);
      audio.addEventListener('ended', function () { markSpeaking(false); }, { once: true });
      audio.addEventListener('error', function () { markSpeaking(false); }, { once: true });
      audio.play().catch(function () { markSpeaking(false); });
      return;
    }

    if ('speechSynthesis' in window) {
      const utterance = new SpeechSynthesisUtterance(text);
      utterance.rate = speechRate;
      utterance.pitch = speechPitch;
      const selectedVoice = getSelectedVoice();
      if (selectedVoice) {
        utterance.voice = selectedVoice;
      }
      utterance.onstart = function () { markSpeaking(true); };
      utterance.onend = function () { markSpeaking(false); };
      utterance.onerror = function () { markSpeaking(false); };
      window.speechSynthesis.cancel();
      markSpeaking(true);
      window.speechSynthesis.speak(utterance);
    }
  }

  function getAvailableVoices() {
    if (!('speechSynthesis' in window) || typeof window.speechSynthesis.getVoices !== 'function') {
      return [];
    }

    return window.speechSynthesis.getVoices();
  }

  function getSelectedVoice() {
    const selected = voiceSelect?.value || localStorage.getItem(voiceStorageKey) || '';
    if (!selected) {
      return null;
    }

    return getAvailableVoices().find(function (voice) {
      return voice.voiceURI === selected || voice.name === selected;
    }) || null;
  }

  function populateVoiceSelect() {
    if (!voiceSelect) {
      return;
    }

    if (!('speechSynthesis' in window)) {
      voiceSelect.disabled = true;
      voiceSelect.innerHTML = '<option value="">Browser voice unavailable</option>';
      return;
    }

    const selected = localStorage.getItem(voiceStorageKey) || voiceSelect.value || '';
    const voices = getAvailableVoices();
    const options = ['<option value="">Browser default</option>'];

    voices
      .slice()
      .sort(function (left, right) {
        return `${left.lang} ${left.name}`.localeCompare(`${right.lang} ${right.name}`);
      })
      .forEach(function (voice) {
        const value = escapeHtml(voice.voiceURI || voice.name);
        const label = escapeHtml(`${voice.name} (${voice.lang})`);
        const isSelected = selected === voice.voiceURI || selected === voice.name ? ' selected' : '';
        options.push(`<option value="${value}"${isSelected}>${label}</option>`);
      });

    voiceSelect.innerHTML = options.join('');
  }

  async function sendMessage(text) {
    const message = text.trim();
    if (!message || sending) {
      return;
    }

    sending = true;
    addMessage('user', message);
    input.value = '';
    meta.textContent = 'Thinking...';
    setStatus('Thinking', false);

    try {
      const response = await fetch(chatUrl, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ message, conversationId })
      });

      if (!response.ok) {
        const payload = await response.json().catch(function () { return {}; });
        throw new Error(payload.error || 'Assistant request failed.');
      }

      const payload = await response.json();
      conversationId = payload.conversationId || conversationId;
      if (conversationId) {
        localStorage.setItem('progress-zero-conversation-id', conversationId);
      }

      addMessage('assistant', payload.replyText || payload.message || '');
      if (payload.panelTab) {
        showResult(payload.panelTab);
      }

      speak(payload.replyText || payload.message || '', payload.audioDataUrl);
      meta.textContent = payload.modelId ? `${payload.providerLabel || 'AI'} / ${payload.modelId}` : 'Session ready';
      setStatus('Ready', false);
    } finally {
      sending = false;
    }
  }

  function showResult(tab) {
    resultTitle.textContent = tab.title || 'Result';
    const rows = [];
    if (tab.fileSearch && Array.isArray(tab.fileSearch.matches)) {
      rows.push(`<p class="text-muted">${escapeHtml(tab.fileSearch.summary || tab.summary || '')}</p>`);
      rows.push('<div class="zero-result-list">');
      tab.fileSearch.matches.forEach(function (item) {
        rows.push(`
          <div class="zero-result-row">
            <div class="zero-result-row-head">
              <strong>${escapeHtml(item.name || item.path)}</strong>
              <div class="zero-result-actions">
                <button type="button" class="zero-source-link" data-open-file-path="${escapeHtmlAttribute(item.path || '')}" title="Open source path">
                  <i class="bi bi-box-arrow-up-right"></i>
                </button>
                <button type="button" class="zero-source-link" data-copy-path="${escapeHtmlAttribute(item.path || '')}" title="Copy source path">
                  <i class="bi bi-copy"></i>
                </button>
              </div>
            </div>
            <span>${escapeHtml(item.path || '')}</span>
          </div>`);
      });
      rows.push('</div>');
    } else if (tab.storageUsage) {
      rows.push(`<p class="text-muted">${escapeHtml(tab.storageUsage.summary || tab.summary || '')}</p>`);
      rows.push('<div class="zero-result-list">');
      (tab.storageUsage.topFolders || []).forEach(function (item) {
        rows.push(`
          <div class="zero-result-row">
            <div class="zero-result-row-head">
              <strong>${escapeHtml(item.name)} - ${formatBytes(item.sizeBytes)}</strong>
              <div class="zero-result-actions">
                <button type="button" class="zero-source-link" data-open-file-path="${escapeHtmlAttribute(item.path || '')}" title="Open source path">
                  <i class="bi bi-box-arrow-up-right"></i>
                </button>
                <button type="button" class="zero-source-link" data-copy-path="${escapeHtmlAttribute(item.path || '')}" title="Copy source path">
                  <i class="bi bi-copy"></i>
                </button>
              </div>
            </div>
            <span>${escapeHtml(item.path || '')}</span>
          </div>`);
      });
      (tab.storageUsage.topFiles || []).forEach(function (item) {
        rows.push(`
          <div class="zero-result-row">
            <div class="zero-result-row-head">
              <strong>${escapeHtml(item.name)} - ${formatBytes(item.sizeBytes)}</strong>
              <div class="zero-result-actions">
                <button type="button" class="zero-source-link" data-open-file-path="${escapeHtmlAttribute(item.path || '')}" title="Open source path">
                  <i class="bi bi-box-arrow-up-right"></i>
                </button>
                <button type="button" class="zero-source-link" data-copy-path="${escapeHtmlAttribute(item.path || '')}" title="Copy source path">
                  <i class="bi bi-copy"></i>
                </button>
              </div>
            </div>
            <span>${escapeHtml(item.path || '')}</span>
          </div>`);
      });
      rows.push('</div>');
    } else if (tab.webSearch && Array.isArray(tab.webSearch.results)) {
      rows.push(`<p class="text-muted">${escapeHtml(tab.summary || '')}</p>`);
      rows.push('<div class="zero-result-list">');
      tab.webSearch.results.forEach(function (item) {
        rows.push(`
          <div class="zero-result-row">
            <div class="zero-result-row-head">
              <strong>${escapeHtml(item.title || item.url)}</strong>
              <a class="zero-source-link"
                 href="${escapeHtmlAttribute(item.url || '#')}"
                 target="_blank"
                 rel="noopener"
                 title="Open source website">
                <i class="bi bi-box-arrow-up-right"></i>
              </a>
            </div>
            <span class="zero-result-source">${escapeHtml(item.source || item.url || '')}</span>
            <p class="zero-result-snippet">${escapeHtml(item.snippet || '')}</p>
            <a class="zero-result-url" href="${escapeHtmlAttribute(item.url || '#')}" target="_blank" rel="noopener">${escapeHtml(item.url || '')}</a>
          </div>`);
      });
      rows.push('</div>');
    } else {
      rows.push(`<p>${escapeHtml(tab.summary || '')}</p>`);
    }

    resultBody.innerHTML = rows.join('');
    resultsPanel.hidden = false;
  }

  function escapeHtml(value) {
    return String(value || '')
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }

  function escapeHtmlAttribute(value) {
    return escapeHtml(value).replace(/`/g, '&#096;');
  }

  function formatBytes(bytes) {
    let value = Number(bytes || 0);
    const units = ['B', 'KB', 'MB', 'GB', 'TB'];
    let index = 0;
    while (value >= 1024 && index < units.length - 1) {
      value /= 1024;
      index++;
    }
    return index === 0 ? `${bytes} ${units[index]}` : `${value.toFixed(2)} ${units[index]}`;
  }

  async function toggleRecording() {
    if (recorder?.state === 'recording') {
      await stopServerRecording();
      return;
    }

    await startServerRecording('manual');
  }

  async function toggleListenMode() {
    continuousListen = !continuousListen;
    listenButton?.classList.toggle('active', continuousListen);
    if (listenButton) {
      listenButton.innerHTML = continuousListen ? '<i class="bi bi-ear-fill"></i>' : '<i class="bi bi-ear"></i>';
      listenButton.title = continuousListen ? 'Listen mode on' : 'Toggle listen mode';
    }
    setVoiceHint(continuousListen
      ? 'Listen mode is on. Speak naturally; Zero sends audio after speech and silence.'
      : 'Mic: click to talk. Ear: listen mode, then speak. No wake word required.');

    if (!continuousListen) {
      clearContinuousRestart();
      if (recorder?.state === 'recording' && recordingMode === 'continuous') {
        cleanupServerRecording();
      }
      setStatus('Ready', false);
      meta.textContent = 'Session ready';
      return;
    }

    if (recorder?.state === 'recording') {
      return;
    }

    await startServerRecording('continuous');
  }

  function setVoiceHint(text) {
    if (voiceHint) {
      voiceHint.textContent = text;
    }
  }

  async function startServerRecording(mode) {
    const AudioContextCtor = window.AudioContext || window.webkitAudioContext;
    if (!AudioContextCtor) {
      throw new Error('Web Audio recording is not available in this browser.');
    }

    cleanupServerRecording();
    recordingMode = mode || 'manual';
    speechDetected = false;
    speechFrames = 0;
    silenceFrames = 0;
    autoStopRequested = false;
    meta.textContent = 'Get ready...';
    setStatus('Ready', false);
    await playVoiceCue('start');

    recordingStream = await navigator.mediaDevices.getUserMedia({
      audio: {
        echoCancellation: true,
        noiseSuppression: true,
        autoGainControl: true,
        sampleRate: { ideal: preferredAudioSampleRate },
        channelCount: { ideal: 1 }
      }
    });

    try {
      audioContext = new AudioContextCtor({ sampleRate: preferredAudioSampleRate });
    } catch {
      audioContext = new AudioContextCtor();
    }

    audioSampleRate = audioContext.sampleRate;
    audioSource = audioContext.createMediaStreamSource(recordingStream);
    audioAnalyser = audioContext.createAnalyser();
    audioAnalyser.fftSize = 512;
    audioProcessor = audioContext.createScriptProcessor(4096, 1, 1);
    chunks = [];
    recordingStartedAt = Date.now();

    audioProcessor.onaudioprocess = function (event) {
      chunks.push(new Float32Array(event.inputBuffer.getChannelData(0)));
      if (!speechDetected && chunks.length > maxPreSpeechChunks) {
      chunks.splice(0, chunks.length - maxPreSpeechChunks);
      }
    };

    audioSource.connect(audioAnalyser);
    audioSource.connect(audioProcessor);
    audioProcessor.connect(audioContext.destination);
    recorder = { state: 'recording' };
    recordButton.classList.toggle('active', recordingMode === 'manual');
    recordButton.innerHTML = recordingMode === 'manual' ? '<i class="bi bi-stop-fill"></i>' : '<i class="bi bi-mic-fill"></i>';
    meta.textContent = recordingMode === 'continuous' ? 'Listen mode active' : 'Click to talk active';
    setStatus('Listening', true);
    startInputLoop();
  }

  async function stopServerRecording(autoStopped) {
    if (recorder?.state !== 'recording') {
      return;
    }

    recorder.state = 'inactive';
    recordButton.classList.remove('active');
    recordButton.innerHTML = '<i class="bi bi-mic-fill"></i>';
    setStatus('Transcribing', false);

    try {
      const samples = mergeChunks(chunks);
      const durationSeconds = samples.length / Math.max(1, audioSampleRate);
      if (!speechDetected || durationSeconds < 0.35) {
        cleanupServerRecording();
        await playVoiceCue('stop');
        setStatus('Ready', false);
        meta.textContent = recordingMode === 'continuous' ? 'Listen mode active' : 'No clear voice detected';
        return;
      }

      const trimmed = trimQuietEdges(samples, audioSampleRate);
      if (trimmed.length / Math.max(1, audioSampleRate) < 0.35) {
        cleanupServerRecording();
        await playVoiceCue('stop');
        setStatus('Ready', false);
        meta.textContent = recordingMode === 'continuous' ? 'Listen mode active' : 'No clear voice detected';
        return;
      }

      const normalized = audioSampleRate === preferredAudioSampleRate
        ? trimmed
        : resampleLinear(trimmed, audioSampleRate, preferredAudioSampleRate);
      const wav = encodeWav(normalized, preferredAudioSampleRate);
      cleanupServerRecording();
      await playVoiceCue('stop');

      const formData = new FormData();
      formData.append('audio', wav, 'recording.wav');
      formData.append('conversationId', conversationId);

      const response = await fetch(voiceUrl, { method: 'POST', body: formData });
      if (!response.ok) {
        const payload = await response.json().catch(function () { return {}; });
        throw new Error(payload.error || 'Voice request failed.');
      }

      const payload = await response.json();
      conversationId = payload.conversationId || conversationId;
      if (conversationId) {
        localStorage.setItem('progress-zero-conversation-id', conversationId);
      }

      if (payload.userText) {
        addMessage('user', payload.userText);
      }
      if (payload.replyText) {
        addMessage('assistant', payload.replyText);
        speak(payload.replyText, payload.audioDataUrl);
      }
      meta.textContent = payload.modelId ? `${payload.providerLabel || 'AI'} / ${payload.modelId}` : 'Session ready';
      setStatus('Ready', false);
    } catch (error) {
      cleanupServerRecording();
      addMessage('assistant', error.message);
      setStatus('Error', false);
      meta.textContent = 'Voice request failed';
    } finally {
      if (continuousListen && (autoStopped || recordingMode === 'continuous')) {
        scheduleContinuousRestart();
      }
    }
  }

  async function playVoiceCue(kind) {
    if (muted) {
      return;
    }

    const AudioContextCtor = window.AudioContext || window.webkitAudioContext;
    if (!AudioContextCtor) {
      return;
    }

    let cueContext;
    try {
      cueContext = new AudioContextCtor();
      if (cueContext.state === 'suspended') {
        await cueContext.resume().catch(function () { });
      }

      const oscillator = cueContext.createOscillator();
      const gain = cueContext.createGain();
      const now = cueContext.currentTime;
      const duration = 0.13;
      const startFrequency = kind === 'start' ? 740 : 520;
      const endFrequency = kind === 'start' ? 980 : 330;

      oscillator.type = 'sine';
      oscillator.frequency.setValueAtTime(startFrequency, now);
      oscillator.frequency.exponentialRampToValueAtTime(endFrequency, now + duration);
      gain.gain.setValueAtTime(0.0001, now);
      gain.gain.exponentialRampToValueAtTime(0.09, now + 0.02);
      gain.gain.exponentialRampToValueAtTime(0.0001, now + duration);

      oscillator.connect(gain);
      gain.connect(cueContext.destination);
      oscillator.start(now);
      oscillator.stop(now + duration);

      await new Promise(function (resolve) {
        oscillator.onended = resolve;
      });
    } catch {
      // Audio cues are optional; recording should still work if playback is blocked.
    } finally {
      cueContext?.close?.().catch(function () { });
    }
  }

  function scheduleContinuousRestart() {
    clearContinuousRestart();
    if (!continuousListen || speaking || recorder?.state === 'recording') {
      return;
    }

    continuousRestartTimer = window.setTimeout(function () {
      continuousRestartTimer = 0;
      if (!continuousListen || speaking || sending || recorder?.state === 'recording') {
        return;
      }

      startServerRecording('continuous').catch(function (error) {
        continuousListen = false;
        listenButton?.classList.remove('active');
        if (listenButton) {
          listenButton.innerHTML = '<i class="bi bi-ear"></i>';
        }
        addMessage('assistant', error.message);
        setStatus('Error', false);
      });
    }, 650);
  }

  function clearContinuousRestart() {
    if (!continuousRestartTimer) {
      return;
    }

    window.clearTimeout(continuousRestartTimer);
    continuousRestartTimer = 0;
  }

  function cleanupServerRecording() {
    stopInputLoop();
    if (audioProcessor) {
      try { audioProcessor.disconnect(); } catch { }
      audioProcessor.onaudioprocess = null;
      audioProcessor = null;
    }

    if (audioSource) {
      try { audioSource.disconnect(); } catch { }
      audioSource = null;
    }

    if (audioAnalyser) {
      try { audioAnalyser.disconnect(); } catch { }
      audioAnalyser = null;
    }

    if (audioContext) {
      audioContext.close().catch(function () { });
      audioContext = null;
    }

    if (recordingStream) {
      recordingStream.getTracks().forEach(function (track) { track.stop(); });
      recordingStream = null;
    }

    recorder = null;
    chunks = [];
    recordingStartedAt = 0;
    speechDetected = false;
    speechFrames = 0;
    silenceFrames = 0;
    autoStopRequested = false;
    resetInputWave();
    recordButton?.classList.remove('active');
    if (recordButton) {
      recordButton.innerHTML = '<i class="bi bi-mic-fill"></i>';
    }
  }

  function startInputLoop() {
    stopInputLoop();

    const tick = function () {
      if (!audioAnalyser) {
        return;
      }

      const level = readAnalyserLevel(audioAnalyser);
      setVoiceLevel(level);
      renderInputWaveFromAnalyser(audioAnalyser);
      updateVoiceActivity(level, recordingMode === 'continuous');
      inputAnimationFrame = window.requestAnimationFrame(tick);
    };

    tick();
  }

  function stopInputLoop() {
    if (inputAnimationFrame) {
      window.cancelAnimationFrame(inputAnimationFrame);
      inputAnimationFrame = 0;
    }

    setVoiceLevel(0);
    resetInputWave();
  }

  function readAnalyserLevel(node) {
    const data = new Uint8Array(node.frequencyBinCount);
    node.getByteFrequencyData(data);
    let total = 0;

    for (let i = 0; i < data.length; i++) {
      total += data[i];
    }

    const average = total / Math.max(1, data.length) / 255;
    const gated = average <= 0.03 ? 0 : (average - 0.03) / 0.97;
    return Math.min(1, Math.max(0, gated * 2.4));
  }

  function setVoiceLevel(level) {
    avatarStage?.style.setProperty('--audio-wave-level', level.toFixed(3));
    avatar?.style.setProperty('--audio-wave-level', level.toFixed(3));
  }

  function renderInputWaveFromAnalyser(node) {
    const bars = inputIndicator?.querySelectorAll('span') || [];
    if (!bars.length) {
      return;
    }

    const data = new Uint8Array(node.frequencyBinCount);
    node.getByteFrequencyData(data);
    const bucketSize = Math.max(1, Math.floor(data.length / bars.length));

    bars.forEach(function (bar, index) {
      let sum = 0;
      const start = index * bucketSize;
      const end = Math.min(data.length, start + bucketSize);
      for (let i = start; i < end; i++) {
        sum += data[i];
      }

      const average = end > start ? sum / (end - start) / 255 : 0;
      const height = 12 + Math.max(0, average - 0.02) * 92;
      bar.style.height = `${Math.min(100, Math.max(10, height))}%`;
      bar.style.opacity = `${Math.min(1, 0.2 + average * 1.3)}`;
    });
  }

  function resetInputWave() {
    const bars = inputIndicator?.querySelectorAll('span') || [];
    bars.forEach(function (bar) {
      bar.style.height = '18%';
      bar.style.opacity = '0.28';
    });
  }

  function updateVoiceActivity(level, allowAutoStop) {
    if (autoStopRequested) {
      return;
    }

    const speechThreshold = 0.12;
    const silenceThreshold = 0.05;
    const speechStartFrames = 5;
    const silenceStopFrames = 18;

    if (level >= speechThreshold) {
      speechFrames += 1;
      silenceFrames = 0;
      if (!speechDetected && speechFrames >= speechStartFrames) {
        speechDetected = true;
        setStatus('Listening', true);
      }
      return;
    }

    speechFrames = Math.max(0, speechFrames - 1);
    if (!speechDetected) {
      return;
    }

    if (allowAutoStop && level <= silenceThreshold) {
      silenceFrames += 1;
      if (silenceFrames >= silenceStopFrames) {
        autoStopRequested = true;
        stopServerRecording(true);
      }
      return;
    }

    silenceFrames = 0;
  }

  function trimQuietEdges(samples, sampleRate) {
    if (!samples || samples.length === 0) {
      return new Float32Array(0);
    }

    const threshold = 0.012;
    const padding = Math.floor(sampleRate * 0.18);
    let start = 0;
    let end = samples.length - 1;

    while (start < samples.length && Math.abs(samples[start]) < threshold) {
      start++;
    }

    while (end > start && Math.abs(samples[end]) < threshold) {
      end--;
    }

    start = Math.max(0, start - padding);
    end = Math.min(samples.length - 1, end + padding);
    return samples.slice(start, end + 1);
  }

  function resampleLinear(samples, fromRate, toRate) {
    if (!samples || samples.length === 0 || fromRate === toRate) {
      return samples || new Float32Array(0);
    }

    const ratio = fromRate / toRate;
    const newLength = Math.max(1, Math.round(samples.length / ratio));
    const result = new Float32Array(newLength);

    for (let i = 0; i < newLength; i++) {
      const sourceIndex = i * ratio;
      const left = Math.floor(sourceIndex);
      const right = Math.min(samples.length - 1, left + 1);
      const weight = sourceIndex - left;
      result[i] = samples[left] * (1 - weight) + samples[right] * weight;
    }

    return result;
  }

  function mergeChunks(buffers) {
    const length = buffers.reduce(function (total, buffer) { return total + buffer.length; }, 0);
    const result = new Float32Array(length);
    let offset = 0;

    buffers.forEach(function (buffer) {
      result.set(buffer, offset);
      offset += buffer.length;
    });

    return result;
  }

  function encodeWav(samples, rate) {
    const buffer = new ArrayBuffer(44 + samples.length * 2);
    const view = new DataView(buffer);

    writeString(view, 0, 'RIFF');
    view.setUint32(4, 36 + samples.length * 2, true);
    writeString(view, 8, 'WAVE');
    writeString(view, 12, 'fmt ');
    view.setUint32(16, 16, true);
    view.setUint16(20, 1, true);
    view.setUint16(22, 1, true);
    view.setUint32(24, rate, true);
    view.setUint32(28, rate * 2, true);
    view.setUint16(32, 2, true);
    view.setUint16(34, 16, true);
    writeString(view, 36, 'data');
    view.setUint32(40, samples.length * 2, true);

    let offset = 44;
    samples.forEach(function (sample) {
      const value = Math.max(-1, Math.min(1, sample));
      view.setInt16(offset, value < 0 ? value * 0x8000 : value * 0x7fff, true);
      offset += 2;
    });

    return new Blob([view], { type: 'audio/wav' });
  }

  function writeString(view, offset, value) {
    for (let i = 0; i < value.length; i++) {
      view.setUint8(offset + i, value.charCodeAt(i));
    }
  }

  form.addEventListener('submit', async function (event) {
    event.preventDefault();
    try {
      await sendMessage(input.value);
    } catch (error) {
      addMessage('assistant', error.message);
      setStatus('Error', false);
      meta.textContent = 'Request failed';
    }
  });

  input.addEventListener('keydown', function (event) {
    if (event.key !== 'Enter' || event.altKey || event.isComposing) {
      return;
    }

    event.preventDefault();
    form.requestSubmit();
  });

  recordButton?.addEventListener('click', async function () {
    try {
      if (continuousListen) {
        await toggleListenMode();
      }
      await toggleRecording();
    } catch (error) {
      addMessage('assistant', error.message);
      setStatus('Error', false);
    }
  });

  listenButton?.addEventListener('click', async function () {
    try {
      await toggleListenMode();
    } catch (error) {
      continuousListen = false;
      listenButton.classList.remove('active');
      listenButton.innerHTML = '<i class="bi bi-ear"></i>';
      addMessage('assistant', error.message);
      setStatus('Error', false);
    }
  });

  muteButton?.addEventListener('click', function () {
    muted = !muted;
    muteButton.classList.toggle('active', muted);
    muteButton.innerHTML = muted ? '<i class="bi bi-volume-mute-fill"></i>' : '<i class="bi bi-volume-up-fill"></i>';
    if (muted && 'speechSynthesis' in window) {
      window.speechSynthesis.cancel();
      setSpeaking(false);
    }
  });

  clearButton?.addEventListener('click', function () {
    conversationId = '';
    localStorage.removeItem('progress-zero-conversation-id');
    messages.innerHTML = '';
    addMessage('assistant', 'Conversation cleared.');
    setStatus('Ready', false);
  });

  resultClose?.addEventListener('click', function () {
    resultsPanel.hidden = true;
  });

  resultBody?.addEventListener('click', async function (event) {
    const openPath = event.target.closest('[data-open-file-path]');
    if (openPath) {
      const path = openPath.getAttribute('data-open-file-path');
      if (path) {
        window.open(`file:///${path.replace(/\\/g, '/')}`, '_blank');
      }
      return;
    }

    const copyPath = event.target.closest('[data-copy-path]');
    if (copyPath) {
      const path = copyPath.getAttribute('data-copy-path') || '';
      try {
        await navigator.clipboard.writeText(path);
        meta.textContent = 'Source path copied';
      } catch {
        meta.textContent = 'Could not copy source path';
      }
    }
  });

  shell.querySelectorAll('[data-zero-tab]').forEach(function (tab) {
    tab.addEventListener('click', function () {
      const id = tab.dataset.zeroTab;
      shell.querySelectorAll('[data-zero-tab]').forEach(function (item) { item.classList.remove('active'); });
      shell.querySelectorAll('[data-zero-panel]').forEach(function (item) { item.classList.remove('active'); });
      tab.classList.add('active');
      shell.querySelector(`[data-zero-panel="${id}"]`)?.classList.add('active');
    });
  });

  shell.querySelectorAll('[data-zero-prompt]').forEach(function (button) {
    button.addEventListener('click', function () {
      input.value = button.dataset.zeroPrompt || '';
      input.focus();
      shell.querySelector('[data-zero-tab="conversation"]')?.click();
    });
  });

  voiceSelect?.addEventListener('change', function () {
    localStorage.setItem(voiceStorageKey, voiceSelect.value || '');
  });

  populateVoiceSelect();
  if ('speechSynthesis' in window) {
    window.speechSynthesis.addEventListener?.('voiceschanged', populateVoiceSelect);
    window.speechSynthesis.onvoiceschanged = populateVoiceSelect;
  }

  loadConversationHistory();
})();
