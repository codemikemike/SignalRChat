/**
 * UIRenderer — pure DOM rendering helpers.
 * SRP: takes data, produces DOM. Knows nothing about SignalR.
 */

export class UIRenderer {
    constructor(dom) {
        this.dom = dom; // pre-cached DOM references
        this._currentUser = null;
    }

    setCurrentUser(user) {
        this._currentUser = user;
    }

    renderMessage(message) {
        const isSelf = this._currentUser && message.user === this._currentUser.userName;
        const isPrivate = message.type === 2 || message.type === 'Private';

        const wrapper = document.createElement('div');
        wrapper.className = `message ${isSelf ? 'message-self' : ''} ${isPrivate ? 'message-private' : ''}`;

        wrapper.innerHTML = `
            <img class="message-avatar" src="${message.avatar}" alt="" />
            <div class="message-body">
                <div class="message-meta">
                    <span class="message-author">${this._escape(message.user)}</span>
                    <span class="message-timestamp">${this._formatTime(message.timestamp)}</span>
                </div>
                <div class="message-text">${this._escape(message.text)}</div>
            </div>
        `;

        this.dom.messages.appendChild(wrapper);
        this._scrollToBottom();
    }

    renderSystemMessage(text) {
        const div = document.createElement('div');
        div.className = 'message-system';
        div.textContent = text;
        this.dom.messages.appendChild(div);
        this._scrollToBottom();
    }

    clearMessages() {
        this.dom.messages.innerHTML = '';
    }

    renderHistory(messages) {
        if (!messages || messages.length === 0) return;
        messages.forEach(m => this.renderMessage(m));
    }

    renderUserList(users) {
        this.dom.userList.innerHTML = '';
        const sorted = [...users].sort((a, b) => a.userName.localeCompare(b.userName));

        for (const user of sorted) {
            const isSelf = this._currentUser && user.userName === this._currentUser.userName;
            const li = document.createElement('li');
            li.className = 'user-list-item';
            li.dataset.username = user.userName;
            li.title = isSelf ? 'You' : `Click to send a private message to ${user.userName}`;
            li.innerHTML = `
                <img class="user-list-avatar" src="${user.avatar}" alt="" />
                <span class="user-list-name ${isSelf ? 'user-list-self' : ''}">${this._escape(user.userName)}</span>
            `;
            this.dom.userList.appendChild(li);
        }

        this.dom.roomUserCount.textContent = users.length;
    }

    updateGlobalOnlineCount(count) {
        this.dom.globalOnline.textContent = count;
    }

    updateConnectionState(state) {
        const dot = this.dom.connectionDot;
        const text = this.dom.connectionStatus;

        dot.className = 'status-dot';

        switch (state) {
            case 'connecting':
                dot.classList.add('status-connecting');
                text.textContent = 'Connecting…';
                break;
            case 'connected':
                dot.classList.add('status-connected');
                text.textContent = 'Connected';
                break;
            case 'reconnecting':
                dot.classList.add('status-connecting');
                text.textContent = 'Reconnecting…';
                break;
            case 'disconnected':
                dot.classList.add('status-disconnected');
                text.textContent = 'Disconnected';
                break;
        }
    }

    setActiveRoom(room) {
        this.dom.currentRoom.textContent = `#${room}`;
        this.dom.chatHeaderTitle.textContent = `# ${room}`;
        this.dom.messageInput.placeholder = `Send a message in #${room}…`;
    }

    setMyIdentity(user) {
        this._currentUser = user;
        this.dom.myAvatar.src = user.avatar;
        this.dom.myUsername.textContent = user.userName;
    }

    showToast(message, type = 'info') {
        const toast = document.createElement('div');
        toast.className = `toast toast-${type}`;
        toast.innerHTML = `
            <div class="toast-content">
                ${message.avatar ? `<img class="toast-avatar" src="${message.avatar}" alt="" />` : ''}
                <span>${this._escape(message.text)}</span>
            </div>
        `;
        this.dom.toastContainer.appendChild(toast);

        setTimeout(() => {
            toast.style.animation = 'toastOut 0.3s var(--ease-out) forwards';
            setTimeout(() => toast.remove(), 300);
        }, 4000);
    }

    enableInput() {
        this.dom.messageInput.disabled = false;
        this.dom.sendBtn.disabled = false;
        this.dom.messageInput.focus();
    }

    showLogin() {
        this.dom.loginOverlay.classList.remove('hidden');
        this.dom.chatShell.classList.add('hidden');
    }

    showChat() {
        this.dom.loginOverlay.classList.add('hidden');
        this.dom.chatShell.classList.remove('hidden');
    }

    updateAvatarPreview(url) {
        this.dom.avatarPreview.src = url;
    }

    showTypingIndicator(typingUsers) {
        if (typingUsers.size === 0) {
            this.dom.typingIndicator.classList.add('hidden');
            return;
        }

        const names = Array.from(typingUsers);
        let text;
        if (names.length === 1) text = `${names[0]} is typing…`;
        else if (names.length === 2) text = `${names[0]} and ${names[1]} are typing…`;
        else text = `${names.length} people are typing…`;

        this.dom.typingText.textContent = text;
        this.dom.typingIndicator.classList.remove('hidden');
    }

    _scrollToBottom() {
        this.dom.messages.scrollTop = this.dom.messages.scrollHeight;
    }

    _formatTime(timestamp) {
        const d = new Date(timestamp);
        return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    }

    _escape(str) {
        const div = document.createElement('div');
        div.textContent = str ?? '';
        return div.innerHTML;
    }
}
