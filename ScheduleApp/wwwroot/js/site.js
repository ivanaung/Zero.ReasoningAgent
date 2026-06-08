// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

(function () {
    const presetColors = [
        '#2453A6', '#2E5DA6', '#3B82F6', '#2563EB', '#1D4ED8', '#0EA5E9',
        '#06B6D4', '#14B8A6', '#10B981', '#22C55E', '#84CC16', '#EAB308',
        '#F59E0B', '#F97316', '#EF4444', '#DC2626', '#EC4899', '#DB2777',
        '#A855F7', '#7C3AED', '#8B5CF6', '#6366F1', '#64748B', '#475569',
        '#334155', '#0F172A', '#78716C', '#A16207', '#059669', '#BE123C'
    ];

    function normalizeHex(value) {
        if (!value) {
            return '';
        }

        let hex = String(value).trim().toUpperCase();
        if (!hex.startsWith('#')) {
            hex = '#' + hex;
        }

        if (/^#([0-9A-F]{3})$/.test(hex)) {
            hex = '#' + hex.slice(1).split('').map(ch => ch + ch).join('');
        }

        return /^#([0-9A-F]{6})$/.test(hex) ? hex : '';
    }

    window.setColorPickerValue = function (inputOrSelector, value) {
        const input = typeof inputOrSelector === 'string'
            ? document.querySelector(inputOrSelector)
            : inputOrSelector;

        if (!input) {
            return;
        }

        const normalized = normalizeHex(value) || value;
        input.value = normalized;
        input.dispatchEvent(new Event('input', { bubbles: true }));
        input.dispatchEvent(new Event('change', { bubbles: true }));
    };

    document.addEventListener('DOMContentLoaded', function () {
        if (typeof window.Coloris === 'function') {
            window.Coloris({
                el: '.coloris-input',
                theme: 'default',
                themeMode: 'light',
                alpha: false,
                format: 'hex',
                formatToggle: false,
                clearButton: false,
                closeButton: true,
                closeLabel: 'Close',
                margin: 6,
                swatches: presetColors
            });
        }

        document.querySelectorAll('.coloris-input').forEach(input => {
            input.addEventListener('blur', function () {
                const normalized = normalizeHex(this.value);
                if (normalized) {
                    this.value = normalized;
                    this.dispatchEvent(new Event('input', { bubbles: true }));
                }
            });
        });
    });

    function initializeAiAssistantWidget() {
        const shell = document.querySelector('[data-ai-widget]');
        if (!shell) {
            return;
        }

        const panel = shell.querySelector('.ai-assistant-panel');
        const toggle = shell.querySelector('[data-ai-toggle]');
        const minimize = shell.querySelector('[data-ai-minimize]');
        const closeButton = shell.querySelector('[data-ai-close]');
        const clearButton = shell.querySelector('[data-ai-clear]');
        const form = shell.querySelector('[data-ai-form]');
        const input = shell.querySelector('[data-ai-input]');
        const submit = shell.querySelector('[data-ai-submit]');
        const messages = shell.querySelector('[data-ai-messages]');
        const status = shell.querySelector('[data-ai-status]');
        const provider = shell.querySelector('[data-ai-provider-label]');
        const meta = shell.querySelector('[data-ai-meta]');
        const error = shell.querySelector('[data-ai-error]');
        const healthUrl = shell.dataset.aiHealthUrl || '/ai/health';
        const chatUrl = shell.dataset.aiChatUrl || '/ai/chat';
        const clearUrlTemplate = shell.dataset.aiClearUrlTemplate || '/ai/conversation/__conversationId__';
        const storageKey = 'ai-assistant-state';
        const transcriptKey = 'ai-assistant-transcript';
        const draftKey = 'ai-assistant-draft';
        let conversationId = sessionStorage.getItem('ai-assistant-conversation') || '';
        let health = null;
        let healthLoaded = false;
        let transcript = [];

        function focusInput() {
            if (!input || input.disabled || panel.hidden) {
                return;
            }

            window.requestAnimationFrame(() => {
                input.focus();
                const end = input.value.length;
                input.setSelectionRange(end, end);
            });
        }

        function setOpenState(isOpen) {
            shell.classList.toggle('is-open', isOpen);
            panel.hidden = !isOpen;
            toggle.setAttribute('aria-expanded', String(isOpen));
            localStorage.setItem(storageKey, isOpen ? 'open' : 'closed');

            if (isOpen) {
                void ensureHealthLoaded();
                setTimeout(focusInput, 120);
            }
        }

        function setBusyState(isBusy) {
            shell.classList.toggle('is-busy', isBusy);
            submit.disabled = isBusy;
            input.disabled = isBusy;
            meta.textContent = isBusy ? 'Assistant is working...' : 'Session ready';
        }

        function setError(message) {
            if (!message) {
                error.textContent = '';
                error.classList.add('d-none');
                return;
            }

            error.textContent = message;
            error.classList.remove('d-none');
        }

        function scrollMessages() {
            messages.scrollTop = messages.scrollHeight;
        }

        function persistTranscript() {
            sessionStorage.setItem(transcriptKey, JSON.stringify(transcript));
        }

        function persistDraft() {
            sessionStorage.setItem(draftKey, input.value || '');
        }

        async function readJsonResponse(response, fallbackMessage) {
            const contentType = response.headers.get('content-type') || '';
            if (contentType.toLowerCase().includes('application/json')) {
                return await response.json();
            }

            const rawText = await response.text();
            throw new Error(rawText && rawText.trim()
                ? rawText.trim()
                : (fallbackMessage || 'The server did not return JSON.'));
        }

        function loadTranscript() {
            const savedTranscript = sessionStorage.getItem(transcriptKey);
            if (!savedTranscript) {
                const initialBubble = messages.querySelector('.ai-message-assistant .ai-message-bubble');
                transcript = initialBubble
                    ? [{ role: 'assistant', text: initialBubble.textContent?.trim() || '', extraClass: '', actions: [] }]
                    : [];
                persistTranscript();
                return;
            }

            try {
                const parsed = JSON.parse(savedTranscript);
                transcript = Array.isArray(parsed) ? parsed : [];
            } catch {
                transcript = [];
            }
        }

        function renderTranscript() {
            messages.innerHTML = '';

            if (!transcript.length) {
                transcript = [{
                    role: 'assistant',
                    text: 'Ask about projects, blocked work, delayed tasks, or create/reschedule tasks with natural commands.',
                    extraClass: '',
                    actions: []
                }];
                persistTranscript();
            }

            transcript.forEach(item => {
                renderMessage(item.role, item.text, item.extraClass, item.actions);
            });
        }

        function renderMessage(role, text, extraClass, actions) {
            const wrapper = document.createElement('div');
            wrapper.className = 'ai-message ' + (role === 'user' ? 'ai-message-user' : 'ai-message-assistant');
            if (extraClass) {
                wrapper.classList.add(extraClass);
            }

            const bubble = document.createElement('div');
            bubble.className = 'ai-message-bubble';
            bubble.textContent = text;
            wrapper.appendChild(bubble);
            if (Array.isArray(actions) && actions.length) {
                const actionList = document.createElement('div');
                actionList.className = 'ai-action-list';

                actions.forEach(action => {
                    const actionCard = document.createElement('div');
                    actionCard.className = 'ai-action-card ai-action-' + (action.status || 'info');

                    const title = document.createElement('div');
                    title.className = 'ai-action-title';
                    title.textContent = action.title || 'Action';
                    actionCard.appendChild(title);

                    const detail = /^\s*[\[{]/.test(action.detail || '')
                        ? document.createElement('pre')
                        : document.createElement('div');
                    detail.className = 'ai-action-detail';
                    detail.textContent = action.detail || '';
                    actionCard.appendChild(detail);

                    actionList.appendChild(actionCard);
                });

                wrapper.appendChild(actionList);
            }
            messages.appendChild(wrapper);
            scrollMessages();
            return bubble;
        }

        function createMessage(role, text, extraClass, actions) {
            transcript.push({
                role: role,
                text: text || '',
                extraClass: extraClass || '',
                actions: Array.isArray(actions) ? actions : []
            });
            persistTranscript();
            return renderMessage(role, text, extraClass, actions);
        }

        async function loadHealth() {
            try {
                const response = await fetch(healthUrl, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
                health = await readJsonResponse(response, 'Assistant health check failed.');
                const isOnline = Boolean(health.enabled && health.healthy);
                status.textContent = isOnline ? 'Online' : 'Offline';
                status.classList.toggle('is-online', isOnline);
                provider.textContent = health.enabled
                    ? [health.provider, health.modelId].filter(Boolean).join(' / ')
                    : 'Assistant disabled';
                meta.textContent = health.message || 'Session ready';
            } catch {
                status.textContent = 'Offline';
                status.classList.remove('is-online');
                provider.textContent = 'Provider unavailable';
                meta.textContent = 'Assistant health check failed.';
            }
        }

        async function ensureHealthLoaded() {
            if (healthLoaded) {
                return;
            }

            healthLoaded = true;
            await loadHealth();
        }

        function buildAssistantMessage(response) {
            if (!response) {
                return 'No response received.';
            }

            const actions = Array.isArray(response.actions) && response.actions.length
                ? '\n\n' + response.actions.map(action => '• ' + action.title + (action.detail ? ': ' + action.detail : '')).join('\n')
                : '';

            return (response.message || 'No response received.') + actions;
        }

        async function clearConversation() {
            transcript = [{
                role: 'assistant',
                text: 'Ask about projects, blocked work, delayed tasks, or create/reschedule tasks with natural commands.',
                extraClass: '',
                actions: []
            }];
            persistTranscript();
            renderTranscript();
            setError('');
            input.value = '';
            persistDraft();

            if (!conversationId) {
                return;
            }

            try {
                const clearUrl = clearUrlTemplate.replace('__conversationId__', encodeURIComponent(conversationId));
                await fetch(clearUrl, {
                    method: 'DELETE',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
            } catch {
                // Ignore clear failures on the client; a new conversation id will be used.
            }

            conversationId = '';
            sessionStorage.removeItem('ai-assistant-conversation');
            meta.textContent = 'Conversation cleared';
            focusInput();
        }

        async function sendMessage(message) {
            const trimmed = message.trim();
            if (!trimmed) {
                return;
            }

            setError('');
            createMessage('user', trimmed);
            input.value = '';
            persistDraft();
            setBusyState(true);

            try {
                const response = await fetch(chatUrl, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    body: JSON.stringify({
                        message: trimmed,
                        conversationId: conversationId,
                        stream: false
                    })
                });

                const payload = await readJsonResponse(response, 'The AI assistant is unavailable.');
                if (!response.ok) {
                    throw new Error(payload.error || 'The AI assistant is unavailable.');
                }

                if (payload.conversationId) {
                    conversationId = payload.conversationId;
                    sessionStorage.setItem('ai-assistant-conversation', conversationId);
                }

                createMessage('assistant', payload.message || 'No response received.', payload.requiresApproval ? 'ai-message-warning' : '', payload.actions);
                provider.textContent = [payload.providerLabel, payload.modelId].filter(Boolean).join(' / ') || provider.textContent;
                meta.textContent = payload.requiresApproval ? 'Approval required for part of this request' : 'Response received';
            } catch (fetchError) {
                const messageText = fetchError instanceof Error ? fetchError.message : 'The AI assistant is unavailable.';
                createMessage('assistant', messageText, 'ai-message-error');
                setError(messageText);
            } finally {
                setBusyState(false);
                focusInput();
            }
        }

        toggle?.addEventListener('click', function () {
            setOpenState(!shell.classList.contains('is-open'));
        });

        minimize?.addEventListener('click', function () {
            setOpenState(false);
        });

        closeButton?.addEventListener('click', function () {
            setOpenState(false);
        });

        clearButton?.addEventListener('click', function () {
            clearConversation();
        });

        form?.addEventListener('submit', function (event) {
            event.preventDefault();
            void sendMessage(input.value);
        });

        input?.addEventListener('keydown', function (event) {
            if (event.key === 'Enter' && !event.shiftKey) {
                event.preventDefault();
                void sendMessage(input.value);
            }
        });

        input?.addEventListener('input', function () {
            persistDraft();
        });

        loadTranscript();
        renderTranscript();
        input.value = sessionStorage.getItem(draftKey) || '';

        if (localStorage.getItem(storageKey) === 'open') {
            setOpenState(true);
        }
    }
})();
