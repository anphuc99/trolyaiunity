using System.Collections.Generic;
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
		private static readonly HashSet<string> ActiveSceneScopes = new HashSet<string>();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void Init()
		{
			HookSceneEvents();

			var activeScene = SceneManager.GetActiveScene();
			if (activeScene.IsValid() && !string.IsNullOrWhiteSpace(activeScene.name))
			{
				ActivateSceneScope(activeScene.name);
			}
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
			if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.name))
			{
				return;
			}

			ActivateSceneScope(scene.name);
		}

		private static void OnSceneUnloaded(Scene scene)
		{
			if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.name))
			{
				return;
			}

			DeactivateSceneScope(scene.name);
		}

		private static void ActivateSceneScope(string scopeKey)
		{
			if (ActiveSceneScopes.Contains(scopeKey))
			{
				return;
			}

			RequestController.ActivateScope(scopeKey);
			ActiveSceneScopes.Add(scopeKey);
		}

		private static void DeactivateSceneScope(string scopeKey)
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
