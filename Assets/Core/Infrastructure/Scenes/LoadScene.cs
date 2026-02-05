using Core.Infrastructure.Attributes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Infrastructure.Scenes
{
	/// <summary>
	/// Provides a simple, centralized way to load scenes by controller scope key.
	/// </summary>
	public static class LoadScene
	{
		/// <summary>
		/// Loads the scene whose name matches the provided <see cref="ControllerScopeKey"/>.
		/// </summary>
		/// <param name="scopeKey">The controller scope key that maps to a scene name.</param>
		/// <param name="mode">The scene load mode.</param>
		/// <remarks>
		/// Side effects: triggers Unity scene loading, which will activate/deactivate controller scopes
		/// through <see cref="Requests.ControllerScopeSceneBridge"/>.
		/// </remarks>
		public static void ByScope(ControllerScopeKey scopeKey, LoadSceneMode mode = LoadSceneMode.Single)
		{
			var sceneName = scopeKey.ToString();
			if (string.IsNullOrWhiteSpace(sceneName))
			{
				Debug.LogWarning("[LoadScene] Scope key produced an empty scene name.");
				return;
			}

			// Scene names must match ControllerScopeKey values for auto-scope activation.
			SceneManager.LoadScene(sceneName, mode);
		}
	}
}