/**
 * app.js — composition root.
 * Wires DOM events to ChatConnection and UIRenderer.
 * Stays thin — every concern is in its own module.
 */

import { ChatConnection } from './chatConnection.js';
import { UIRenderer } from './uiRenderer.js';
import { getAvatarUrl } from './avatarService.js';

// ---------- DOM cache (one place, no scattered queries) ----------
const dom = {
    loginOverlay:     document.getElementById('loginOverlay'),
    loginForm:        document.getElementById('loginForm'),
    usernameInput:    document.getElementById('usernameInput'),
    customRoomInput:  document.getElementById('customRoomInput'),
    avatarPreview:    document.getElementById('avatarPreview'),
    chatShell:        document.getElementById('chatShell'),
    messages:         document.getElementById('messagesContainer'),
    messageForm:      document.getElementById('messageForm'),
    messageInput:     document.getElementById('messageInput'),
    sendBtn:          document.getElementById('sendBtn'),
    emojiBtn:         document.getElementById('emojiBtn'),
    emojiPicker:      document.getElementById('emojiPicker'),
    userList:         document.getElementById('userList'),
    roomUserCount:    document.getElementById('roomUserCount'),
    globalOnline:     document.getElementById('globalOnline'),
    currentRoom:      document.getElementById('currentRoom'),
    chatHeaderTitle:  document.getElementById('chatHeaderTitle'),
    leaveRoomBtn:     document.getElementById('leaveRoomBtn'),
    myAvatar:         document.getElementById('myAvatar'),
    myUsername:       document.getElementById('myUsername'),
    connectionDot:    document.getElementById('connectionDot'),
    connectionStatus: document.getElementById('connectionStatus'),
    typingIndicator:  document.getElementById('typingIndicator'),
    typingText:       document.getElementById('typingText'),
    toastContainer:   document.getElementById('toastContainer')
};

// ---------- Compose ----------
const ui = new UIRenderer(dom);
const chat = new ChatConnection('/chathub');

let selectedRoom = 'general';
let myUser = null;
let privateTarget = null;
const typingUsers = new Set();
const typingTimers = new Map();
let typingDebounce = null;

// ---------- Login flow ----------
function updateAvatarPreview() {
    ui.updateAvatarPreview(getAvatarUrl(dom.usernameInput.value || 'preview'));
}
dom.usernameInput.addEventListener('input', updateAvatarPreview);
updateAvatarPreview();

document.querySelectorAll('.room-pill').forEach(pill => {
    pill.addEventListener('click', () => {
        document.querySelectorAll('.room-pill').forEach(p => p.classList.remove('selected'));
        pill.classList.add('selected');
        selectedRoom = pill.dataset.room;
        dom.customRoomInput.value = '';
    });
});
document.querySelector('.room-pill[data-room="general"]').classList.add('selected');

dom.customRoomInput.addEventListener('input', () => {
    if (dom.customRoomInput.value.trim()) {
        document.querySelectorAll('.room-pill').forEach(p => p.classList.remove('selected'));
        selectedRoom = dom.customRoomInput.value.trim();
    }
});

dom.loginForm.addEventListener('submit', async (e) => {
    e.preventDefault();
    const userName = dom.usernameInput.value.trim();
    const room = (dom.customRoomInput.value.trim() || selectedRoom).toLowerCase();
    if (!userName || !room) return;

    try {
        await chat.start();
        await chat.joinRoom(userName, room);
    } catch (err) {
        console.error('Join failed:', err);
        alert('Could not connect to server. Is the server running?');
    }
});

// ---------- SignalR event handlers ----------
chat.on('connectionState', (state) => ui.updateConnectionState(state));

chat.on('JoinConfirmed', (data) => {
    myUser = data;
    ui.setMyIdentity(data);
    ui.setActiveRoom(data.room);
    ui.clearMessages();
    ui.showChat();
    ui.enableInput();
});

chat.on('MessageHistory', (messages) => {
    ui.renderHistory(messages);
});

chat.on('ReceiveMessage', (message) => {
    ui.renderMessage(message);
    typingUsers.delete(message.user);
    ui.showTypingIndicator(typingUsers);
});

chat.on('ReceivePrivateMessage', (message) => {
    ui.renderMessage(message);
});

chat.on('UserJoined', (data) => {
    if (myUser && data.userName === myUser.userName) return;
    ui.showToast({ text: `${data.userName} joined`, avatar: data.avatar }, 'join');
});

chat.on('UserLeft', (data) => {
    if (myUser && data.userName === myUser.userName) return;
    ui.showToast({ text: `${data.userName} left`, avatar: data.avatar }, 'leave');
    typingUsers.delete(data.userName);
    ui.showTypingIndicator(typingUsers);
});

chat.on('RoomPresenceUpdated', (data) => {
    ui.renderUserList(data.users);
});

chat.on('GlobalOnlineCountUpdated', (count) => {
    ui.updateGlobalOnlineCount(count);
});

chat.on('UserTyping', (data) => {
    if (data.isTyping) {
        typingUsers.add(data.userName);
        clearTimeout(typingTimers.get(data.userName));
        typingTimers.set(data.userName, setTimeout(() => {
            typingUsers.delete(data.userName);
            ui.showTypingIndicator(typingUsers);
        }, 4000));
    } else {
        typingUsers.delete(data.userName);
    }
    ui.showTypingIndicator(typingUsers);
});

chat.on('Error', (msg) => {
    ui.showToast({ text: msg }, 'error');
});

// ---------- Message sending ----------
dom.messageForm.addEventListener('submit', async (e) => {
    e.preventDefault();
    const text = dom.messageInput.value.trim();
    if (!text) return;

    try {
        if (privateTarget) {
            await chat.sendPrivateMessage(privateTarget, text);
        } else {
            await chat.sendMessage(text);
        }
        dom.messageInput.value = '';
        clearPrivateTarget();
    } catch (err) {
        console.error('Send failed:', err);
        ui.showToast({ text: 'Could not send message' }, 'error');
    }
});

// ---------- Typing indicator (debounced) ----------
dom.messageInput.addEventListener('input', () => {
    if (!typingDebounce) {
        chat.notifyTyping(true).catch(() => {});
    }
    clearTimeout(typingDebounce);
    typingDebounce = setTimeout(() => {
        chat.notifyTyping(false).catch(() => {});
        typingDebounce = null;
    }, 2000);
});

// ---------- Emoji picker ----------
dom.emojiBtn.addEventListener('click', (e) => {
    e.stopPropagation();
    dom.emojiPicker.classList.toggle('hidden');
});

dom.emojiPicker.addEventListener('click', (e) => {
    if (e.target.dataset.emoji) {
        dom.messageInput.value += e.target.dataset.emoji;
        dom.messageInput.focus();
        dom.emojiPicker.classList.add('hidden');
    }
});

document.addEventListener('click', (e) => {
    if (!dom.emojiPicker.contains(e.target) && e.target !== dom.emojiBtn) {
        dom.emojiPicker.classList.add('hidden');
    }
});

// ---------- Private message via user-list click ----------
dom.userList.addEventListener('click', (e) => {
    const item = e.target.closest('.user-list-item');
    if (!item) return;
    const userName = item.dataset.username;
    if (!userName || (myUser && userName === myUser.userName)) return;

    privateTarget = userName;
    dom.messageInput.placeholder = `🔒 Private message to ${userName}…`;
    dom.messageInput.focus();
});

function clearPrivateTarget() {
    if (!privateTarget) return;
    privateTarget = null;
    if (myUser) {
        dom.messageInput.placeholder = `Send a message in #${myUser.room}…`;
    }
}

dom.messageInput.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') clearPrivateTarget();
});

// ---------- Leave / switch room ----------
dom.leaveRoomBtn.addEventListener('click', async () => {
    try {
        await chat.leaveRoom();
    } catch (err) { /* ignore */ }
    myUser = null;
    privateTarget = null;
    typingUsers.clear();
    ui.clearMessages();
    ui.showLogin();
});
