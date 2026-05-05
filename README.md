# 📣 ChatHub

Real-time chat built on **SignalR** with both a **JavaScript client** and a **Blazor Server client**, sharing the same backend hub. Discord-inspired dark UI with avatars, emoji picker, typing indicator, private messaging, chat rooms, and live online counts.

Built as part of UCL Datamatiker, Uge 19 — Synkrone opgaver / SignalR.

---

## 🏗️ Arkitektur — SOLID & SoC

```
SignalRChat/
├── SignalRChat.sln
└── src/
    ├── SignalRChat.Server/              # SignalR backend + JS client
    │   ├── Models/                      # ChatMessage, ConnectedUser (records)
    │   ├── Interfaces/                  # IUserPresenceService, IAvatarService, IMessageHistoryService
    │   ├── Services/                    # Concrete implementations (DI'd into hub)
    │   ├── Hubs/ChatHub.cs              # Thin orchestration — delegates to services
    │   ├── Extensions/                  # AddChatServices() — keeps Program.cs clean
    │   ├── Program.cs                   # 20 lines, no business logic
    │   └── wwwroot/
    │       ├── index.html               # Pure markup, zero inline JS/CSS
    │       ├── css/                     # 5 files, each with one job
    │       │   ├── reset.css
    │       │   ├── variables.css        # Design tokens
    │       │   ├── layout.css           # Grid / shell
    │       │   ├── components.css       # Reusable UI pieces
    │       │   └── animations.css       # @keyframes only
    │       └── js/                      # ES modules
    │           ├── app.js               # Composition root — wires it all
    │           ├── chatConnection.js    # SignalR wrapper
    │           ├── uiRenderer.js        # DOM rendering
    │           └── avatarService.js     # Avatar URL generation
    │
    └── SignalRChat.Blazor/              # Blazor Server client (consumes same hub)
        ├── Models/                      # DTOs matching server contracts
        ├── Services/
        │   ├── IChatClientService.cs    # Abstraction
        │   └── ChatClientService.cs     # SignalR client wrapper
        ├── Components/
        │   ├── App.razor
        │   ├── Routes.razor
        │   └── Pages/Chat.razor         # Main UI (uses IChatClientService)
        └── wwwroot/css/app.css          # Same design language as JS client
```

### SOLID-principper i denne kodebase

- **S — Single Responsibility**: Hub'en orkestrerer; `UserPresenceService` tracker tilstedeværelse; `AvatarService` genererer avatars; `MessageHistoryService` gemmer historik. Ingen klasse gør to ting.
- **O — Open/Closed**: Vil du tilføje persistens af beskeder? Implementér `IMessageHistoryService` med EF Core og swap registreringen i `ServiceCollectionExtensions` — ingen kode i hub'en ændres.
- **L — Liskov Substitution**: Alle services bruges udelukkende via deres interface. En mock i tests fungerer 1:1.
- **I — Interface Segregation**: Tre små, fokuserede interfaces frem for ét stort `IChatService`-monstrum.
- **D — Dependency Inversion**: Hub'en kender kun til abstraktioner (`IUserPresenceService` osv.), ikke konkrete typer. DI-containeren binder dem sammen.

### Separation of Concerns (frontend)

JavaScript-klienten viser samme princip:

- `chatConnection.js` ved alt om SignalR — intet om DOM
- `uiRenderer.js` ved alt om DOM — intet om SignalR
- `app.js` er composition root: lytter på events fra connection, kalder renderer
- CSS er splittet: `variables.css` har designsystemet, `layout.css` har struktur, `components.css` har UI, `animations.css` har keyframes

Det betyder du kan udskifte SignalR med raw WebSocket uden at røre `uiRenderer.js`, eller skifte hele UI'en uden at røre `chatConnection.js`.

---

## 🚀 Sådan kører du det

Du skal have **.NET 8 SDK** installeret.

### Kør backend + JS-klient (én terminal)

```bash
cd src/SignalRChat.Server
dotnet run
```

Åbn **http://localhost:5050** i to browserfaner og chat med dig selv.

### Kør Blazor-klienten (anden terminal — backend skal stadig køre)

```bash
cd src/SignalRChat.Blazor
dotnet run
```

Åbn **http://localhost:5051** — Blazor-klienten forbinder til samme hub på port 5050. Du kan have JS-klienten og Blazor-klienten åbne samtidig — de chatter sammen.

---

## ✨ Features

| Feature | JS-klient | Blazor-klient |
|---|---|---|
| Realtime chat | ✅ | ✅ |
| Chat-rooms / channels | ✅ | ✅ |
| Private messages (klik på bruger) | ✅ | ✅ |
| Typing indicator | ✅ | ⏳ (kun receiver) |
| Online users per room | ✅ | ✅ |
| Global online count | ✅ | ✅ |
| Auto-reconnect | ✅ | ✅ |
| Message history ved join | ✅ | ✅ |
| Unique avatars per username | ✅ | ✅ |
| Emoji picker | ✅ | ⏳ |
| Toast notifications (join/leave) | ✅ | ✅ |

---

## 🔧 Tekniske valg

- **.NET 8** — LTS, samme target som dit Slottet-projekt
- **Avatars**: DiceBear API (gratis, ingen nøgle, deterministisk: samme username = samme avatar)
- **Fonts**: Space Grotesk (display + body) + JetBrains Mono (mono) fra Google Fonts
- **State**: In-memory (`ConcurrentDictionary`). For produktion: swap `IUserPresenceService` til Redis eller backplane.
- **Messages**: Rolling buffer på 100 per room. Ingen DB.

---

## 🧪 Sådan udvider du

**Tilføj persistens af beskeder med EF Core:**
1. Opret `EfMessageHistoryService : IMessageHistoryService`
2. I `ServiceCollectionExtensions.cs`: skift `AddSingleton<IMessageHistoryService, MessageHistoryService>` til din nye implementation
3. Færdig. Hub'en og klienterne ændres ikke.

**Tilføj authentication:**
1. Tilføj ASP.NET Core Identity som du gjorde i Slottet
2. Sæt `[Authorize]` på `ChatHub`
3. Brug `Context.User?.Identity?.Name` i stedet for selvvalgt username

---

Built with 📣 by [@codemikemike](https://github.com/codemikemike)
