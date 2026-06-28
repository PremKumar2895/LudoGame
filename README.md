# Ludo Royale

Online multiplayer Ludo for **Android & iOS** — free for all (no ads, no real‑money), built in **Unity**.
_Premium pastel direction · live voice & emotes · portrait · all‑ages._

- 2–10 players, **Free‑for‑all** or **Teams** (2v2 / 3v3 / 4v4 / 5v5)
- **Extended boards**: classic 4‑seat, hexagonal 6‑seat, octagonal 8‑seat, decagonal 10‑seat
- Live **voice chat** (per‑match Voice Room + team channels, fully opt‑out) and **emotes**
- **Stunning animations**: tap‑to‑roll dice VFX, personalised **stick‑legged character** tokens that **walk + kick‑off** on captures
- Social/meta: Friends, Profile/Stats, Store (free cosmetics), Daily Rewards, Leaderboards/Tournaments
- Robust **resilience**: reconnect/resume, lag & connectivity notifications, 60s turn timeout with system auto‑play

## Status

🛠️ **Phase 2 done, Phase 3 (online) underway.** `Ludo.Core` (pure C#) implements the full rules for **2–10 players** on all four boards (4/6/8/10), captures, safe cells, blockades, three-sixes, teams, win/ranking, and an auto-play AI. It's **verified playable in Unity** (procedural board, tap-to-roll dice, animated movement & captures, human-vs-bots — menu **Ludo ▸ New Play Scene**). Phase 3 added the **authoritative server core**: `MatchSnapshot` (wire format), `PlayerIntent`, and `AuthoritativeMatchServer` (server dice, move validation, 60s timer + auto-play). **16/16 unit-tested.**

Next: the **Photon Fusion** transport adapter — see [docs/Phase3-Online-Multiplayer.md](docs/Phase3-Online-Multiplayer.md).

➡️ **To run the Unity client, follow [SETUP.md](SETUP.md)** (install Unity 6.3 LTS, open `client/`, Firebase/Photon/Agora steps).
➡️ **Design & plan:** [docs/Ludo-Game-Design-and-Technical-Requirements.md](docs/Ludo-Game-Design-and-Technical-Requirements.md) · [docs/Ludo-Royale-Implementation-Process-Plan-v1.1-10players.docx](docs/Ludo-Royale-Implementation-Process-Plan-v1.1-10players.docx)

## Repo layout

```
config/    ludo.config.json          ← single config file with real keys (GIT-IGNORED, never pushed)
           ludo.config.example.json  ← blank template (committed)
src/
  Ludo.Core/   Engine-agnostic C# rules library + Unity package (com.premk.ludo-core)
  Ludo.Sim/    Console simulator + stability sweep
tests/
  Ludo.Tests/  Dependency-free unit tests (no NuGet needed)
client/      Unity 6.3 project — embeds Ludo.Core; EngineBootstrap + "Ludo ▸ Run Engine Self-Test"
docs/        Requirements, rules-engine spec, architecture, roadmap, process plan (.docx)
design/      Imported Claude Design export (Ludo Game Flow.dc.html, screenshots, character art)
Ludo.slnx    .NET solution (open in Rider / Visual Studio)
SETUP.md     Unity + Firebase + Photon + Agora setup guide
```

## Build & run the engine (free, no accounts)

Requires the free **.NET SDK** (9 or 10).

```bash
dotnet build Ludo.slnx -c Release      # build everything
dotnet run  --project tests/Ludo.Tests # run the unit tests  -> "12 passed, 0 failed."
dotnet run  --project src/Ludo.Sim     # play AI games + stability sweep for 2–10 players
```

## Configuration & secrets

The single config file **`config/ludo.config.json`** holds all keys. It is **git-ignored** — real
secrets never get pushed. The committed reference is **`config/ludo.config.example.json`** (blank).

- `gameplay` — rule tunables (wired into the engine; the client reads a secrets-free copy from `client/Assets/StreamingAssets/ludo.gameplay.json`).
- `services` — Photon / Firebase / Agora / Google IDs.
- ⚠️ `services.agora.appCertificate` is **server-only** — never ship it in the client (it goes in a Cloud Function token server).

Also git-ignored: `client/Assets/google-services.json`, `client/Assets/GoogleService-Info.plist`, `*.keystore`, `/artifacts/`, Unity `Library/`.
