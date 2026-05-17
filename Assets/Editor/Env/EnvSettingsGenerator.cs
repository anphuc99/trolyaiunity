using UnityEditor;
using UnityEngine;
using System.IO;
using Shares.Model;

namespace Editor.Env
{
	[InitializeOnLoad]
	public static class EnvSettingsGenerator
	{
		private const string ResourceFolder = "Assets/Resources";
		private const string SettingsAssetPath = "Assets/Resources/EnvSettings.asset";

		static EnvSettingsGenerator()
		{
			// Ensure it runs on the main thread during compile/load
			EditorApplication.delayCall += CreateEnvSettingsIfMissing;
		}

		[MenuItem("Settings/Create Env Settings Asset")]
		public static void CreateEnvSettingsIfMissing()
		{
			if (Application.isPlaying) return;

			if (!Directory.Exists(ResourceFolder))
			{
				Directory.CreateDirectory(ResourceFolder);
				AssetDatabase.Refresh();
			}

			var assetExists = AssetDatabase.LoadAssetAtPath<EnvSettings>(SettingsAssetPath) != null;
			if (!assetExists)
			{
				var asset = ScriptableObject.CreateInstance<EnvSettings>();
				AssetDatabase.CreateAsset(asset, SettingsAssetPath);
				AssetDatabase.SaveAssets();
				AssetDatabase.Refresh();
				Debug.Log($"[EnvSettingsGenerator] Created EnvSettings asset at: {SettingsAssetPath}");
			}
		}
	}
}
