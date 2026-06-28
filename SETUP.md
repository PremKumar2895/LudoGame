# Ludo Royale — Developer Setup (Phase 2: Unity client)

This gets the Unity client running on your machine and wires up Firebase / Photon / Agora.
The game **engine** (`src/Ludo.Core`) already works and is unit-tested; this is about the Unity front end.

> **Security first:** `config/ludo.config.json`, `client/Assets/google-services.json` and
> `client/Assets/GoogleService-Info.plist` contain your real keys and are **git-ignored** — they
> never get pushed. The committed reference is `config/ludo.config.example.json` (blank).

---

## 0. What's already prepared for you

- `src/Ludo.Core/` — the rules engine, now also a **Unity local package** (`com.premk.ludo-core`).
- `client/` — a minimal Unity project that **embeds the engine** and includes:
  - `Assets/Ludo/Runtime/EngineBootstrap.cs` — play a full AI game from a scene.
  - `Assets/Ludo/Editor/EngineSelfTest.cs` — menu **Ludo ▸ Run Engine Self-Test** (no Play mode needed).
  - `Assets/StreamingAssets/ludo.gameplay.json` — gameplay tunables (no secrets).
  - `Assets/google-services.json` + `Assets/GoogleService-Info.plist` — generated from your Firebase config.

---

## 1. Install the Unity Editor (the one blocker)

You have Unity **Hub**; you also need an **Editor**:

1. Open Unity Hub ▸ **Installs** ▸ **Install Editor**.
2. Choose **Unity 6.3 LTS** (version `6000.3.x`).
3. Tick these modules:
   - **Android Build Support** (expand it: **Android SDK & NDK Tools** + **OpenJDK**) — required.
   - **iOS Build Support** — optional now (iOS builds still need a Mac later).
4. Install. (First install downloads a few GB.)

> Android development works fully on Windows. iOS only needs a Mac when you actually build for iOS.

---

## 2. Open the project & verify the engine

1. Unity Hub ▸ **Projects** ▸ **Add** ▸ **Add project from disk** ▸ select
   `C:\Users\premk\React_Project\LudoGame\LudoGame\client`.
2. Open it. First import takes a minute (it resolves packages and compiles).
   - If Hub flags a version difference, open with your installed `6000.3.x` and accept the upgrade.
3. Verify the engine compiled inside Unity: top menu **Ludo ▸ Run Engine Self-Test**.
   - The **Console** should show a series of `PASS` lines ending with `... passed, 0 failed`.
4. (Optional) Runtime check: create an empty scene, add an empty GameObject, attach
   **`EngineBootstrap`**, press **Play** → the Console logs a finished 4-player game.
5. **▶️ Play the prototype:** menu **Ludo ▸ New Play Scene (vs Bots)**, then press **Play**.
   - **Tap anywhere** to roll on your turn; **tap a highlighted token** to move it. Bots play the rest.
   - On the **GameDirector** component, change `players` (2–10) and tick `teams` to try every board size.
   - The board renders **procedurally for any size** (4/6/8/10) — the pastel art & stickman skins land in the animation phase; this proves the full game loop end-to-end.

> If packages fail to resolve, see **Troubleshooting** at the bottom.

---

## 3. Firebase (per https://firebase.google.com/docs/unity/setup)

The two config files are already in `client/Assets/` (generated from your `config/ludo.config.json`).
If you later re-download them from the Firebase console, just replace these (don't rename — no `(1)` suffixes).

**Import the SDK:**
1. Download the **Firebase Unity SDK** zip from <https://firebase.google.com/download/unity> and unzip it.
2. In Unity: **Assets ▸ Import Package ▸ Custom Package…** and import these `.unitypackage` files
   (each pulls in the External Dependency Manager automatically):
   - `FirebaseAuth.unitypackage` — login (Google / Guest)
   - `FirebaseFirestore.unitypackage` — profiles, friends, match summaries
   - `FirebaseCrashlytics.unitypackage` — crash logging
   - `FirebaseMessaging.unitypackage` — push (your-turn / invites)
   - `FirebaseAnalytics.unitypackage` — analytics
3. Let the **External Dependency Manager** run (Android resolver downloads the native libs).
4. Initialization code (`FirebaseApp.CheckAndFixDependenciesAsync()`) gets added when we build the
   accounts layer (Phase 6) — no action needed yet.

**Android Google Sign-In note:** Google Sign-In on Android also needs your app's **SHA-1** registered
in the Firebase console (Project Settings ▸ your Android app), then **re-download `google-services.json`**.
Email/Guest auth, Firestore and Crashlytics work without it.

> iOS Firebase requires "method swizzling" enabled (the default) — don't disable it.

---

## 4. Photon Fusion (real-time multiplayer)

1. Install **Photon Fusion 2** (Asset Store / Photon SDK import).
2. In the Photon app settings asset (created on import), paste your **Fusion App ID** from
   `config/ludo.config.json` ▸ `services.photon.fusionAppId`.
3. (Voice App ID is for Photon Voice — only if we choose Photon Voice over Agora later.)

We integrate Fusion in **Phase 3** (online authoritative match). The engine already separates rules
from transport, so this slots in cleanly.

---

## 5. Agora (voice chat) — and an important security rule

1. Install the **Agora Voice SDK for Unity**.
2. The client uses **`services.agora.appId`** (this is client-safe).
3. ⚠️ **`services.agora.appCertificate` must NEVER ship in the app.** It is used **server-side only**
   to mint short-lived voice tokens. We'll host that in a **Firebase Cloud Function** (Phase 7).
   Keep the certificate out of any client code/build.

---

## 6. Sentry (optional crash/error tracking)

Your `services.sentry.dsn` is a client DSN (safe to embed). We can add the Sentry Unity SDK in the
diagnostics phase if you want it alongside Crashlytics.

---

## 7. Security recap — what is git-ignored (never commit)

- `config/ludo.config.json` (real keys) — edit freely; it stays local.
- `client/Assets/google-services.json`, `client/Assets/GoogleService-Info.plist`.
- `*.keystore`, `*.p12`, `/artifacts/`, Unity `Library/`/`Temp/`.

Committed/shareable: `config/ludo.config.example.json` (blank template), all source, docs.

If you ever run `git add -A`, double-check `git status` does **not** list the files above.

---

## 8. Troubleshooting

- **Engine package not found / `Ludo.Core` missing:** confirm `client/Packages/manifest.json` has
  `"com.premk.ludo-core": "file:../../src/Ludo.Core"`, and that `src/Ludo.Core/` contains
  `package.json` + `Ludo.Core.asmdef`.
- **Package resolve errors on open:** open **Window ▸ Package Manager**, remove a failing line from
  `manifest.json` (e.g. newtonsoft) and let Unity re-resolve; Firebase will re-add what it needs.
- **`CS8632` nullable warnings:** harmless. `src/Ludo.Core/csc.rsp` enables the nullable context; if
  your Unity version ignores per-asmdef rsp files, the warnings are still safe to leave.
- **Project won't open from `client/`:** create a fresh **3D (URP/Built-in) Core** project via Hub,
  then copy `client/Assets/Ludo`, `client/Assets/StreamingAssets`, the two Firebase files, and add the
  `com.premk.ludo-core` line to the new project's `Packages/manifest.json`.

---

## Where we are on the roadmap

- ✅ **Phase 1** — engine (`Ludo.Core`), tested, runs in Unity.
- ▶️ **Phase 2** — Unity client: board rendering, tap-to-roll dice, token movement vs bots. *(next)*
- ⏭️ **Phase 3+** — Photon online, resilience, 6/8/10 boards, accounts (Firebase), voice (Agora), animation.
