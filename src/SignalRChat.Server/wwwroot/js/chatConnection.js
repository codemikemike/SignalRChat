/**
 * ChatConnection — wraps SignalR with a clean API.
 * SRP: connection lifecycle and message routing only.
 * Other modules subscribe via `.on(event, handler)` — they don't touch SignalR directly.
 */

export class ChatConnection {
    constructor(hubUrl) {
        this._connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl)
            .withAutomaticReconnect([0, 2000, 5000, 10000])
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        this._handlers = new Map();
        this._wireServerEvents();
        this._wireConnectionState();
    }

    _wireServerEvents() {
        const events = [
            'JoinConfirmed',
            'UserJoined',
            'UserLeft',
            'ReceiveMessage',
            'ReceivePrivateMessage',
            'MessageHistory',
            'RoomPresenceUpdated',
            'GlobalOnlineCountUpdated',
            'UserTyping',
            'Error'
        ];

        for (const event of events) {
            this._connection.on(event, (...args) => this._fire(event, ...args));
        }
    }

    _wireConnectionState() {
        this._connection.onreconnecting(() => this._fire('connectionState', 'reconnecting'));
        this._connection.onreconnected(() => this._fire('connectionState', 'connected'));
        this._connection.onclose(() => this._fire('connectionState', 'disconnected'));
    }

    on(event, handler) {
        if (!this._handlers.has(event)) {
            this._handlers.set(event, []);
        }
        this._handlers.get(event).push(handler);
    }

    _fire(event, ...args) {
        const handlers = this._handlers.get(event);
        if (handlers) {
            handlers.forEach(h => {
                try { h(...args); } catch (e) { console.error(`Handler for ${event} threw:`, e); }
            });
        }
    }

    async start() {
        this._fire('connectionState', 'connecting');
        await this._connection.start();
        this._fire('connectionState', 'connected');
    }

    async joinRoom(userName, room) {
        await this._connection.invoke('JoinRoom', userName, room);
    }

    async leaveRoom() {
        await this._connection.invoke('LeaveRoom');
    }

    async sendMessage(text) {
        await this._connection.invoke('SendMessage', text);
    }

    async sendPrivateMessage(targetUserName, text) {
        await this._connection.invoke('SendPrivateMessage', targetUserName, text);
    }

    async notifyTyping(isTyping) {
        await this._connection.invoke('NotifyTyping', isTyping);
    }
}
