using UnityEngine;

namespace Shares.Model
{
	/// <summary>
	/// General environment settings for the application.
	/// Kept locally and ignored by Git to avoid leaking secrets.
	/// </summary>
	[CreateAssetMenu(fileName = "EnvSettings", menuName = "Settings/Env Settings")]
	public sealed class EnvSettings : ScriptableObject
	{
		[Header("API Keys")]
		public string GoogleApiKey = "";
		public string OpenAiApiKey = "";
		public string OllamaAuthorization = "";

		[Header("Endpoints")]
		public string BaseUrl = "http://localhost:4000";
		public string OllamaUrl = "http://localhost:11434";

		[Header("Environment")]
		public string EnvironmentName = "development";
		public bool EnableDebugLogs = true;

		private static EnvSettings _instance;

		/// <summary>
		/// Gets the loaded instance of EnvSettings from Resources.
		/// </summary>
		public static EnvSettings Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = Resources.Load<EnvSettings>("EnvSettings");
					if (_instance == null)
					{
						_instance = CreateInstance<EnvSettings>();
						Debug.LogWarning("[EnvSettings] EnvSettings asset not found in Resources. Using default fallback values.");
					}
				}
				return _instance;
			}
		}
	}
}
