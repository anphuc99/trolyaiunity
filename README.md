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
