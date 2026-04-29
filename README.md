# trolyAi

Unity project (Unity 2021 LTS+).

## Core Infrastructure

This repository uses a Unity-adapted MVC-inspired, event-driven, attribute-based architecture:

- **Views**: `MonoBehaviour` only; no direct access to Controllers/Models; communicate via `RequestController` using string keys.
- **Controllers**: pure C#; stateless; unit-testable; publish events via `EventBus`.
- **Models**: data-only; no Unity dependencies; only Controllers read/write.

Core infrastructure code lives under:

`Assets/Core/Infrastructure/`

It provides:

- Request routing from View to Controller via `[Request("key")]`.
- Event publishing from Controller to View via `EventBus.Publish("key", payload)`.
- Attribute-based view binding via `[OnEvent("key")]` and `BaseView`.
- Controller scoping via `[ControllerScope(ControllerScopeKey.ScopeName)]` with `[ControllerInit]` / `[ControllerShutdown]` hooks.
- Global scope change notifications via `Core.Infrastructure.Events.CoreGlobalEvents.ScopeChanged`.
- Attribute-based controller hook for scope changes via `[OnGlobalScopeChanged]` with `ScopeChangedPayload`.
- Cross-scope shared state via `Core.Infrastructure.State.GlobalVariables`.
- Scene names should match `ControllerScopeKey` enum values for auto activation.
- Simple HTTP helpers via `Core.Infrastructure.Network.HttpClient` (GET + JSON POST with UniTask).
- Network URL toggle tool via `Tools/Network Settings` (fake in-code responses vs real server calls).
- Start scene flow validates the stored auth token via `/token/validate` and routes to Login or GamePlay.

### GlobalVariables (Cross-scope shared data)

`Core.Infrastructure.State.GlobalVariables` is a process-wide key-value store for passing data between scopes.

- Read access: available to any scope.
- Write access (`Set`, `Remove`, `Clear`): allowed only when called from Controller code.
- Key API:
	- `Set(string key, object value)`
	- `TryGet<T>(string key, out T value)`
	- `GetOrDefault<T>(string key, T fallback = default)`
	- `Remove(string key)`
	- `Clear()`

## Feature Generator Tool

An editor tool is available to generate a new feature skeleton with the required folder structure, scene, asmdefs, and starter scripts.

Menu:

- `Tools/Feature Generator`

Output root:

- `Assets/Features/<FeatureName>/`

Subfeatures:

- Use the "Generate as Subfeature" toggle in the same window.
- Output root: `Assets/Features/<Parent>/SubFeatures/<Child>/`
- Subfeatures share the parent scope (no new scene/scope key is created).
- Child-to-parent signaling uses Action/Func callbacks injected by the parent.
- The generator updates the parent asmdef to reference the child assembly.

### Subfeature Lifecycle Rules (Mandatory)

When adding a new GamePlay subfeature (or any parent-scoped subfeature), follow these rules:

1. **Controller must expose lifecycle methods**
	- `public static void Install()`
	- `public static void Uninstall()`

2. **Controller must publish visibility events**
	- In `Install()`, publish: `EventBus.Publish(<Subfeature>Events.Installed, null)`
	- In `Uninstall()`, publish: `EventBus.Publish(<Subfeature>Events.Uninstalled, null)`

3. **Event keys are required in `<Subfeature>Events`**
	- `public const string Installed = "...installed.event";`
	- `public const string Uninstalled = "...uninstalled.event";`

4. **View must react to install/uninstall events**
	- Add `[OnEvent(<Subfeature>Events.Installed)]` handler and call `gameObject.SetActive(true)`
	- Add `[OnEvent(<Subfeature>Events.Uninstalled)]` handler and call `gameObject.SetActive(false)`

5. **Parent controller owns signal wiring**
	- Parent scope controller sets/clears child `SetParentSignals(...)` in its `OnEnterScope()` / `OnExitScope()`.
	- Parent controller switches active subfeatures by calling child `Install()` / `Uninstall()`.

6. **Do not bypass this lifecycle**
	- Do not toggle child view GameObjects directly from parent view scripts.
	- Do not skip Installed/Uninstalled events.

Note:

- When changing feature-related code patterns or conventions, update the Feature Generator templates accordingly to prevent new features from compiling with outdated code.

## Server: Long-Term Memory (ChromaDB)

The chat server supports optional long-term AI memory backed by ChromaDB. When enabled, the AI can remember important facts across conversations (user preferences, relationships, story progress, etc.).

### Architecture

1. **Big AI** (gemini-3-flash-preview) emits optional sidecar fields (`ImportantMemoryEn`, `ImportantMemoryType`, `ImportantMemoryImportance`) on its first reply turn when something important happens.
2. The sidecar is extracted, validated, and stored in ChromaDB as an English-canonical vector document (fire-and-forget, non-blocking).
3. The sidecar fields are stripped before saving to chat history.
4. On next user message, a **cheap AI** (gemini-2.0-flash-lite) rewrites the user intent into English search queries, queries ChromaDB, and compresses the results into a brief.
5. The compressed memory brief is injected into the system prompt under `LONG-TERM MEMORY`.

### Environment Variables

| Variable | Required | Default | Description |
|---|---|---|---|
| `CHROMA_URL` | No | _(empty — memory disabled)_ | ChromaDB server URL (e.g. `http://localhost:8000`) |
| `CHEAP_AI_MODEL` | No | `gemini-2.0-flash-lite` | Model for intent rewriting and memory compression |
| `GOOGLE_API_KEY` | Yes (for memory) | — | Shared with Gemini chat; used by cheap AI service |

### New Service Files

- `server/src/services/vector-memory.service.ts` — ChromaDB wrapper (upsert/query/delete)
- `server/src/services/memory-extraction.service.ts` — Parse/strip memory sidecar from AI reply
- `server/src/services/cheap-ai.service.ts` — Gemini Flash Lite for intent rewriting + compression
- `server/src/services/memory-retrieval.service.ts` — Orchestrates retrieve + compress pipeline

## Chat: Auto Talk Mode (Client)

The GamePlay Chat client supports a batch auto-conversation mode controlled from parent menu, keyboard shortcut, or the Apply button.

- Menu: `Chat tự động` (toggle on/off)
- Shortcut: `Ctrl+R` (same toggle behavior)
- Apply button: `_buttonApplyAutoChat` starts/stops batch auto chat using the count from `_inputNumberAutochat`
- **Phase 1 – Generation**: sends context `AI tự nói chuyện ít nhất {N} tin nhắn mỗi lượt` then repeatedly requests `GenerateReplyFromHistory`, buffering all turns silently (no display, no TTS playback). Chat input is disabled during this phase.
- **Phase 2 – Playback**: once the accumulated turn count reaches or exceeds N, all buffered turns are enqueued and played back sequentially with TTS audio (audio is pre-loaded by Controller during Phase 1).
- After playback completes, auto chat stops automatically.
- Auto mode also stops when user toggles off, chat ends/uninstalls, no active scene characters remain, or a request fails.

## Chat: Local AI via Ollama (PC Desktop)

On desktop platforms (Windows, macOS, Linux), the Chat client uses a local Ollama instance (`gemma4:e4b`) instead of sending messages to the server's cloud AI (Gemini). This eliminates cloud API costs for PC users and provides faster response times.

### How It Works

1. **Client detects desktop platform** (`Application.platform` check in `ChatController`).
2. **Prepare**: Client calls `POST /api/chat/prepare-local` to get the server-built system prompt and chat history (the server still owns prompt engineering, user data, and memory retrieval).
3. **Generate locally**: Client sends the prompt + history + user message to the local Ollama instance at `http://localhost:11434/api/chat`.
4. **Save to server**: Client sends the locally-generated reply back to the server via `POST /api/chat/save-local`, which stores it in chat history and performs memory extraction.
5. **Display**: The reply is parsed and published to views as normal.

### Prerequisites

- **Ollama** must be installed and running locally: https://ollama.com
- Pull the model: `ollama pull gemma4:e4b`
- Ollama runs on `http://localhost:11434` by default.

### Audio Messages

Audio messages (voice recordings) still go through the server's cloud AI since Ollama does not support audio input. The local AI path is bypassed when the payload contains audio.

### Server Endpoints

| Endpoint | Method | Purpose |
|---|---|---|
| `/api/chat/prepare-local` | POST | Returns system prompt + history for local AI |
| `/api/chat/save-local` | POST | Saves user message + AI reply to history |

### Key Files

- `Assets/Features/GamePlay/SubFeatures/Chat/Scripts/Infrastructure/OllamaService.cs` — Ollama HTTP client
- `Assets/Features/GamePlay/SubFeatures/Chat/Scripts/Controller/ChatController.cs` — Platform detection + local AI flow
- `Assets/Features/GamePlay/SubFeatures/Chat/Scripts/Model/ChatModel.cs` — Ollama/local AI payload models
- `server/src/controllers/chat/chat.controller.ts` — `prepareLocalPrompt` + `saveLocalReply` handlers
- `server/src/routes/chat.routes.ts` — Route registration
