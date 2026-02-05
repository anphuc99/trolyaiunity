using System.Collections.Generic;
using Core.Infrastructure.Attributes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Infrastructure.Requests
{
	/// <summary>
	/// Bridges Unity scene lifecycle to controller scopes.
	/// </summary>
	public static class ControllerScopeSceneBridge
	{
		private const string LogPrefix = "[ControllerScopeSceneBridge]";
		private static bool _hooked;
		private static readonly HashSet<ControllerScopeKey> ActiveSceneScopes = new HashSet<ControllerScopeKey>();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void Init()
		{
			HookSceneEvents();

			var activeScene = SceneManager.GetActiveScene();
			TryActivateSceneScope(activeScene);
		}

		private static void HookSceneEvents()
		{
			if (_hooked)
			{
				return;
			}

			SceneManager.sceneLoaded += OnSceneLoaded;
			SceneManager.sceneUnloaded += OnSceneUnloaded;
			_hooked = true;
		}

		private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			TryActivateSceneScope(scene);
		}

		private static void OnSceneUnloaded(Scene scene)
		{
			TryDeactivateSceneScope(scene);
		}

		private static void TryActivateSceneScope(Scene scene)
		{
			if (!TryResolveScope(scene, out var scopeKey))
			{
				return;
			}

			ActivateSceneScope(scopeKey);
		}

		private static void TryDeactivateSceneScope(Scene scene)
		{
			if (!TryResolveScope(scene, out var scopeKey))
			{
				return;
			}

			DeactivateSceneScope(scopeKey);
		}

		private static bool TryResolveScope(Scene scene, out ControllerScopeKey scopeKey)
		{
			scopeKey = ControllerScopeKey.Global;
			if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.name))
			{
				return false;
			}

			if (!System.Enum.TryParse(scene.name, ignoreCase: false, out ControllerScopeKey parsed))
			{
				Debug.LogWarning($"{LogPrefix} Scene '{scene.name}' does not map to a ControllerScopeKey enum value.");
				return false;
			}

			scopeKey = parsed;
			return true;
		}

		private static void ActivateSceneScope(ControllerScopeKey scopeKey)
		{
			if (ActiveSceneScopes.Contains(scopeKey))
			{
				return;
			}

			RequestController.ActivateScope(scopeKey);
			ActiveSceneScopes.Add(scopeKey);
		}

		private static void DeactivateSceneScope(ControllerScopeKey scopeKey)
		{
			if (!ActiveSceneScopes.Contains(scopeKey))
			{
				return;
			}

			RequestController.DeactivateScope(scopeKey);
			ActiveSceneScopes.Remove(scopeKey);
		}
	}
}
