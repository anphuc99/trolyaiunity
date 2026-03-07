using System.IO;
using Core.Infrastructure.Network;
using UnityEditor;
using UnityEngine;

namespace EditorTools.Network
{
	/// <summary>
	/// Editor tool to toggle fake vs real network URLs.
	/// </summary>
	public sealed class NetworkSettingsWindow : EditorWindow
	{
		private const string WindowTitle = "Network Settings";
		private const string MenuPath = "Tools/Network Settings";
		private const string ResourceFolder = "Assets/Resources";
		private const string SettingsAssetPath = "Assets/Resources/NetworkSettings.asset";

		private NetworkSettings _settings;

		[MenuItem(MenuPath)]
		private static void Open()
		{
			GetWindow<NetworkSettingsWindow>(WindowTitle);
		}

		private void OnEnable()
		{
			LoadOrCreateSettings();
		}

		private void OnGUI()
		{
			if (_settings == null)
			{
				EditorGUILayout.HelpBox("NetworkSettings asset could not be loaded.", MessageType.Error);
				if (GUILayout.Button("Create Settings"))
				{
					LoadOrCreateSettings();
				}
				return;
			}

			EditorGUILayout.LabelField("Network URL Mode", EditorStyles.boldLabel);
			EditorGUILayout.Space();

			var useFake = EditorGUILayout.Toggle("Use Fake URL", _settings.UseFakeUrl);
			var baseUrl = EditorGUILayout.TextField("Base URL", _settings.BaseUrl);

			if (useFake != _settings.UseFakeUrl || baseUrl != _settings.BaseUrl)
			{
				Undo.RecordObject(_settings, "Update Network Settings");
				_settings.UseFakeUrl = useFake;
				_settings.BaseUrl = baseUrl;
				EditorUtility.SetDirty(_settings);
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Active Mode", GetActiveModePreview(), EditorStyles.helpBox);
		}

		private void LoadOrCreateSettings()
		{
			_settings = AssetDatabase.LoadAssetAtPath<NetworkSettings>(SettingsAssetPath);
			if (_settings != null)
			{
				return;
			}

			EnsureFolderExists(ResourceFolder);
			_settings = ScriptableObject.CreateInstance<NetworkSettings>();
			AssetDatabase.CreateAsset(_settings, SettingsAssetPath);
			AssetDatabase.SaveAssets();
		}

		private static void EnsureFolderExists(string folderPath)
		{
			if (AssetDatabase.IsValidFolder(folderPath))
			{
				return;
			}

			var parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
			var name = Path.GetFileName(folderPath);
			if (!string.IsNullOrWhiteSpace(parent) && !string.IsNullOrWhiteSpace(name))
			{
				AssetDatabase.CreateFolder(parent, name);
			}
		}

		private string GetActiveModePreview()
		{
			if (_settings == null)
			{
				return "<missing settings>";
			}

			var mode = _settings.UseFakeUrl ? "FAKE (in-code)" : "REAL";
			var url = string.IsNullOrWhiteSpace(_settings.BaseUrl) ? "<empty>" : _settings.BaseUrl;
			return $"{mode} | Base URL: {url}";
		}
	}
}
