# 📣 ChatHub

Real-time chat-applikation bygget på **SignalR** med både en **JavaScript-klient** og en **Blazor Server-klient**, der deler samme backend hub. Discord-inspireret dark UI med avatars, emoji picker, typing indicator, private beskeder, chat-rooms og live online count.

Bygget som en del af UCL Datamatiker, Uge 19 — Synkrone opgaver / SignalR.

---

## ⚡ TL;DR — Tech stack i én linje

**Frontend:** HTML/CSS/ES modules + Blazor Server (C#)
**Backend:** ASP.NET Core 8 + SignalR
**Transport:** WebSocket (Lag 7) over TCP (Lag 4) — fallback til SSE/Long Polling
**State:** In-memory (`ConcurrentDictionary`) bag interfaces for nem swap til Redis/DB
**Arkitektur:** Clean Architecture-inspireret med SOLID & SoC, DI-baseret

---

## 🏗️ Arkitektur — Big Picture

![ChatHub architecture overview](./docs/architecture.svg)

Diagrammet viser hvordan komponenterne hænger sammen — bemærk specielt at pilene mellem hub, interfaces og services begge peger **ind mod interfacet**. Det er essensen af Dependency Inversion.

### De 4 lag

- **Tier 1 — Klienterne** (blå/lilla): To forskellige UI'er, samme job. JS-klienten er ren HTML/JS, Blazor-klienten er C# der renderes via SignalR-circuit.
- **Tier 2 — ChatHub** (rød): Det centrale orkestreringspunkt. Modtager `invoke`-kald fra klienterne og delegerer videre. Jeg har bevidst holdt den tynd — ingen forretningslogik her.
- **Tier 3 — Interfaces** (grå): Kontrakter der definerer *hvad* services kan, ikke *hvordan*. Hub'en kender kun til disse.
- **Tier 4 — Services** (grøn): De konkrete implementations med faktisk logik.
- **DI Container** (gul): Bindeleddet. Ved app-start mapper den hvert interface til sin konkrete klasse.

### WebSocket — den vedvarende forbindelse

Når en bruger logger ind, åbnes en **vedvarende forbindelse** mellem browser og server (begge veje). Begge parter kan sende beskeder når som helst — ingen polling, ingen overhead ved gentagne HTTP-requests.

### Dependency Inversion i praksis

Pilene i diagrammet viser hvordan afhængighederne peger:

```
ChatHub  →  Interface  ←  Service
```

Begge peger ind mod interfacet. Det betyder:

- Hub'en (high-level) afhænger af **abstraktion**
- Service'en (low-level) afhænger også af **abstraktion**
- Begge kender kun til interfacet — ikke til hinanden

I koden ser det sådan ud:

**1. Interface — kontrakten:**
```csharp
// Interfaces/IUserPresenceService.cs
public interface IUserPresenceService
{
    void AddUser(ConnectedUser user);
    ConnectedUser? GetUser(string connectionId);
    // ...
}
```

**2. Hub — afhænger kun af interfacet:**
```csharp
// Hubs/ChatHub.cs
public ChatHub(IUserPresenceService presence, ...)  // ← interface
{
    _presence = presence;
}
```

**3. Service — implementerer interfacet:**
```csharp
// Services/UserPresenceService.cs
public class UserPresenceService : IUserPresenceService  // ← implementer
{
    // konkret kode med ConcurrentDictionary
}
```

**4. DI Container — binder dem sammen ved runtime:**
```csharp
// Extensions/ServiceCollectionExtensions.cs
services.AddSingleton<IUserPresenceService, UserPresenceService>();
//                    ↑ interface           ↑ konkret implementation
```

Det betyder `UserPresenceService` kan udskiftes med en Redis-version eller EF Core-version uden at ændre én linje i `ChatHub`. Det er **Dependency Inversion** (D'et i SOLID).

### Hvad sker der når man sender en besked?

1. Bruger trykker **Enter** i input-feltet
2. JS-klienten kalder `connection.invoke("SendMessage", "hej")` → pakkes til en WebSocket-frame
3. Frame'en lander i `ChatHub.SendMessage()` på serveren
4. Hub'en spørger `IUserPresenceService` om hvem afsenderen er og hvilket rum han er i
5. Hub'en spørger `IMessageHistoryService` om at gemme beskeden
6. Hub'en broadcaster til alle i rummet via `Clients.Group(room).SendAsync("ReceiveMessage", message)`
7. Alle klienter i rummet får frame'en og deres `connection.on("ReceiveMessage", ...)` handler kører
8. `uiRenderer.js` bygger et nyt DOM-element og scroller til bunden

Hele rejsen tager typisk under 50ms — det føles instant. ⚡

---

## 🌐 Netværk & Protokoller

### Hvor sidder SignalR i lagmodellerne?

OSI-modellens 7 lag og TCP/IP-stakkens 4 lag mapper sådan her til denne app:

| OSI Lag | TCP/IP Lag | Teknologi i denne app | Hvad sker der konkret |
|---|---|---|---|
| **7 — Application** | Application | SignalR / WebSocket frames | Hub-metoder, `SendMessage`, `JoinRoom` |
| **6 — Presentation** | Application | UTF-8, JSON serialization | C#-objekter ↔ JSON over wire |
| **5 — Session** | Application | WebSocket handshake | Vedvarende session efter HTTP upgrade |
| **4 — Transport** | Transport | **TCP** | Pålidelig levering, rækkefølge, flow control |
| **3 — Network** | Internet | IP | Routing mellem klient og server |
| **2 — Data Link** | Link | Ethernet / WiFi | Frames mellem netværksenheder |
| **1 — Physical** | Link | Kobber/fiber/radio | Selve signalet |

### WebSocket-handshake — fra HTTP til persistent forbindelse

WebSocket starter som en almindelig HTTP-request og **opgraderer** til en bidirektionel forbindelse:

```
Klient                          Server
   │                               │
   │ ── TCP 3-way handshake ──────►│   (SYN → SYN-ACK → ACK)
   │                               │
   │ ── HTTP GET /chathub ────────►│   Header: Upgrade: websocket
   │ ◄── HTTP 101 Switching ───────│   Same TCP-forbindelse, nu opgraderet
   │                               │
   │ ══ WebSocket frames begge veje ═│   Lav latency, ingen polling
```

### Hvorfor TCP og ikke UDP?

For chat skal beskeder **frem** og i **rigtig rækkefølge** — ellers ser samtalen rodet ud:

| Egenskab | TCP (denne app) | UDP |
|---|---|---|
| Garanteret levering | ✅ | ❌ |
| Rækkefølge bevares | ✅ | ❌ |
| Forbindelses-baseret | ✅ | ❌ (forbindelsesløs) |
| Hastighed | Lidt langsommere | Hurtigere |
| Typisk use case | Chat, web, email, fil-overførsel | Gaming, video-stream, DNS |

### SignalR's transport-fallback

SignalR forsøger transporter i denne rækkefølge — alle bygger ovenpå TCP:

1. **WebSocket** ← foretrukket (fuld duplex, lav latency)
2. **Server-Sent Events (SSE)** ← kun server→klient streaming
3. **Long Polling** ← HTTP-baseret fallback (virker næsten altid)

Hvis WebSocket blokeres af proxy/firewall, falder SignalR automatisk til næste niveau ved opstart.

---

## 🔒 Security

> **⚠️ Bemærk:** Dette er et lærings-/demo-projekt. Production-niveau security kræver flere tilføjelser — de er noteret nedenfor som "Næste skridt".

### Security-emner og status

| Område | Status i denne app | Hvordan det ville løses i produktion |
|---|---|---|
| **HTTPS / TLS** | ⚠️ HTTP i dev | Kestrel/IIS med HTTPS-cert + `app.UseHttpsRedirection()` + HSTS |
| **Authentication** | ❌ Selvvalgt username | ASP.NET Core Identity + JWT på `[Authorize]`-hub |
| **Input validation** | ✅ Trim + null/empty checks i hub | Tilføj længde-limits, regex, sanitization for HTML |
| **XSS protection** | ✅ JS escaper alle indhold via `textContent` / Razor auto-encoder | Behold mønstret, undgå `innerHTML`/`@Html.Raw()` |
| **CORS** | ⚠️ `AllowAnyOrigin` (dev only) | Whitelist konkrete origins |
| **DDoS / rate limiting** | ❌ Ingen | ASP.NET Core Rate Limiter middleware + Cloudflare |

### Detaljeret gennemgang

#### 🔐 HTTPS / TLS — Transport-niveau kryptering

**Lige nu:** HTTP på `localhost:5050` (kun dev). Beskeder er ukrypterede mellem browser og server.

**Production:** WebSocket bliver til **WSS** (WebSocket Secure) ovenpå TLS 1.3. Hver byte mellem klient og server krypteres, så netværks-sniffning ikke afslører chat-indhold. ASP.NET Core understøtter dette out-of-the-box via Kestrel + cert.

```csharp
// Program.cs (production-tilføjelser)
app.UseHttpsRedirection();
app.UseHsts(); // Strict-Transport-Security header
```

#### 🪪 Authentication — Hvem er du?

**Lige nu:** Brugeren skriver bare et username — ingen verifikation. Alle kan udgive sig for at være alle.

**Production-flow med ASP.NET Core Identity + JWT:**

```csharp
// Hub beskyttes
[Authorize]
public class ChatHub : Hub
{
    public override Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        // Brug verificeret userId i stedet for selvvalgt username
    }
}
```

JWT-token sendes med ved WebSocket-opstart:

```javascript
new signalR.HubConnectionBuilder()
    .withUrl("/chathub", { accessTokenFactory: () => myJwt })
```

Det her ville jeg implementere på samme måde som jeg har gjort i mit Slottet-projekt (.NET 8 Blazor Clean Architecture med ASP.NET Core Identity).

#### 🛡️ Input validation & XSS protection

**Lige nu (gjort):**
- Hub'en validerer `null`/`empty` på username, room og besked-tekst
- `string.Trim()` på input
- JS-klienten bruger `textContent` (ikke `innerHTML`) når beskeder rendres → browseren escaper HTML automatisk
- Blazor auto-encoder strings i `@variable` syntaks

**Production-tilføjelser:**
- Max-længde på beskeder (fx 500 chars) og brugernavne (fx 20 chars)
- Regex på brugernavne (fx kun `[a-zA-Z0-9_-]`)
- HTML-sanitization library hvis der skal være rich text (fx HtmlSanitizer NuGet)
- Server-side validation af alle felter (aldrig kun klient-side!)

```csharp
public async Task SendMessage(string text)
{
    if (string.IsNullOrWhiteSpace(text)) return;
    if (text.Length > 500) text = text[..500]; // truncate
    // ... rest of logic
}
```

#### 🌍 CORS — Cross-Origin Resource Sharing

**Lige nu:** `AllowAnyOrigin()` i `Program.cs` — alle domæner kan forbinde til hub'en. Det er fint til dev fordi Blazor-klienten kører på en anden port end backend.

**Production:** Whitelist konkrete origins:

```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("https://chat.minside.dk")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
```

#### 🚦 DDoS / Rate limiting

**Lige nu:** Intet beskytter mod en bot der spammer 1000 beskeder i sekundet og crasher hub'en.

**Production-løsning på flere niveauer:**

| Lag | Løsning |
|---|---|
| Edge | Cloudflare / Azure Front Door med DDoS-beskyttelse |
| Web server | Nginx rate limiting per IP |
| App | ASP.NET Core Rate Limiter middleware |
| Hub | Custom logic — fx max 5 beskeder per sekund per connection |

```csharp
// Program.cs — eksempel på app-niveau rate limit
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("chat", opt =>
    {
        opt.PermitLimit = 30;
        opt.Window = TimeSpan.FromMinutes(1);
    });
});
```

### General security best practices fulgt i koden

- ✅ **Parameterized DI** — ingen `new ConcreteService()` i hub
- ✅ **`ConcurrentDictionary`** for thread-safe state — undgår race conditions
- ✅ **Immutable records** for `ChatMessage` og `ConnectedUser` — kan ikke muteres efter oprettelse
- ✅ **Server-side authority** — klienten kan ikke selv bestemme afsender (hub'en slår op via `ConnectionId`)
- ✅ **Logging** via `ILogger` — sporbarhed ved problemer
- ✅ **Auto-cleanup** — `OnDisconnectedAsync` rydder op i state, ingen memory leaks ved disconnect

---

## 📂 Mappestruktur

```
SignalRChat/
├── SignalRChat.sln
└── src/
    ├── SignalRChat.Server/              # SignalR backend + JS-klient
    │   ├── Models/                      # ChatMessage, ConnectedUser (records)
    │   ├── Interfaces/                  # IUserPresenceService, IAvatarService, IMessageHistoryService
    │   ├── Services/                    # Konkrete implementations (DI'd into hub)
    │   ├── Hubs/ChatHub.cs              # Tynd orkestrering — delegerer til services
    │   ├── Extensions/                  # AddChatServices() — holder Program.cs ren
    │   ├── Program.cs                   # 20 linjer, ingen forretningslogik
    │   └── wwwroot/
    │       ├── index.html               # Ren markup, ingen inline JS/CSS
    │       ├── css/                     # 5 filer, hver med sit eget ansvar
    │       │   ├── reset.css
    │       │   ├── variables.css        # Design tokens
    │       │   ├── layout.css           # Grid / shell
    │       │   ├── components.css       # Genbrugelige UI-komponenter
    │       │   └── animations.css       # Kun @keyframes
    │       └── js/                      # ES modules
    │           ├── app.js               # Composition root — binder det hele sammen
    │           ├── chatConnection.js    # SignalR wrapper
    │           ├── uiRenderer.js        # DOM rendering
    │           └── avatarService.js     # Avatar URL generation
    │
    └── SignalRChat.Blazor/              # Blazor Server-klient (bruger samme hub)
        ├── Models/                      # DTOs der matcher server contracts
        ├── Services/
        │   ├── IChatClientService.cs    # Abstraktion
        │   └── ChatClientService.cs     # SignalR client wrapper
        ├── Components/
        │   ├── App.razor
        │   ├── Routes.razor
        │   └── Pages/Chat.razor         # Hoved-UI (bruger IChatClientService)
        └── wwwroot/css/app.css          # Samme designsprog som JS-klienten
```

---

## 🎯 SOLID-principper i denne kodebase

- **S — Single Responsibility**: Hub'en orkestrerer; `UserPresenceService` tracker tilstedeværelse; `AvatarService` genererer avatars; `MessageHistoryService` gemmer historik. Ingen klasse gør to ting.
- **O — Open/Closed**: Vil man tilføje persistens af beskeder? Implementér `IMessageHistoryService` med EF Core og swap registreringen i `ServiceCollectionExtensions` — ingen kode i hub'en ændres.
- **L — Liskov Substitution**: Alle services bruges udelukkende via deres interface. En mock i tests fungerer 1:1.
- **I — Interface Segregation**: Tre små, fokuserede interfaces frem for ét stort `IChatService`-monstrum.
- **D — Dependency Inversion**: Hub'en kender kun til abstraktioner (`IUserPresenceService` osv.), ikke konkrete typer. DI-containeren binder dem sammen.

### Separation of Concerns (frontend)

JavaScript-klienten følger samme princip:

- `chatConnection.js` ved alt om SignalR — intet om DOM
- `uiRenderer.js` ved alt om DOM — intet om SignalR
- `app.js` er composition root: lytter på events fra connection, kalder renderer
- CSS er splittet: `variables.css` har designsystemet, `layout.css` har struktur, `components.css` har UI, `animations.css` har keyframes

Det betyder SignalR kan udskiftes med raw WebSocket uden at røre `uiRenderer.js`, eller hele UI'en kan skiftes ud uden at røre `chatConnection.js`.

---

## 🚀 Sådan kører du det

Kræver **.NET 8 SDK** installeret.

### Backend + JS-klient (én terminal)

```bash
cd src/SignalRChat.Server
dotnet run
```

Åbn **http://localhost:5050** i to browserfaner og chat med dig selv.

### Blazor-klienten (anden terminal — backend skal stadig køre)

```bash
cd src/SignalRChat.Blazor
dotnet run
```

Åbn **http://localhost:5051** — Blazor-klienten forbinder til samme hub på port 5050. JS-klienten og Blazor-klienten kan være åbne samtidig — de chatter sammen.

---

## ✨ Features

| Feature | JS-klient | Blazor-klient |
|---|---|---|
| Realtime chat | ✅ | ✅ |
| Chat-rooms / channels | ✅ | ✅ |
| Private beskeder (klik på bruger) | ✅ | ✅ |
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

- **.NET 8** — LTS-version
- **Avatars**: DiceBear API (gratis, ingen nøgle, deterministisk: samme username = samme avatar)
- **Fonts**: Space Grotesk (display + body) + JetBrains Mono (mono) fra Google Fonts
- **State**: In-memory (`ConcurrentDictionary`). Til produktion: swap `IUserPresenceService` til Redis eller backplane.
- **Messages**: Rolling buffer på 100 per room. Ingen DB.

---

## 🧪 Sådan kan det udvides

**Tilføj persistens af beskeder med EF Core:**
1. Opret `EfMessageHistoryService : IMessageHistoryService`
2. I `ServiceCollectionExtensions.cs`: skift `AddSingleton<IMessageHistoryService, MessageHistoryService>` til den nye implementation
3. Færdig. Hub'en og klienterne ændres ikke.

**Tilføj authentication:**
1. Tilføj ASP.NET Core Identity
2. Sæt `[Authorize]` på `ChatHub`
3. Brug `Context.User?.Identity?.Name` i stedet for selvvalgt username

---

Built with 📣 by [@codemikemike](https://github.com/codemikemike)
