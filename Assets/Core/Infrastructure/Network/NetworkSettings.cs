using UnityEngine;

namespace Core.Infrastructure.Network
{
	/// <summary>
	/// Stores configurable network URLs for runtime use.
	/// </summary>
	public sealed class NetworkSettings : ScriptableObject
	{
		/// <summary>
		/// When true, in-code fake responses are used instead of real server calls.
		/// </summary>
		public bool UseFakeUrl;

		/// <summary>
		/// Base URL for real server calls.
		/// </summary>
		public string BaseUrl = "http://localhost:5000";

	}
}
