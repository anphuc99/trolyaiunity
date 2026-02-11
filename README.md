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
- Scene names should match `ControllerScopeKey` enum values for auto activation.

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

Note:

- When changing feature-related code patterns or conventions, update the Feature Generator templates accordingly to prevent new features from compiling with outdated code.
