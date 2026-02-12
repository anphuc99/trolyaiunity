using System.Reflection;
using System.Threading.Tasks;
using Core.Infrastructure.Network;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Core.Tests.Network
{
	/// <summary>
	/// Unit tests for <see cref="HttpClient"/> when fake mode is enabled.
	/// </summary>
	public sealed class HttpClientTests
	{
		[SetUp]
		public void SetUp()
		{
			FakeServer.ResetToDefaults();
		}

		[TearDown]
		public void TearDown()
		{
			SetHttpClientSettings(null);
			FakeServer.ResetToDefaults();
		}

		/// <summary>
		/// Verifies GET requests return fake responses when fake mode is enabled.
		/// </summary>
		[Test]
		public async Task GetAsync_ReturnsFakeResponse()
		{
			var settings = UnityEngine.ScriptableObject.CreateInstance<NetworkSettings>();
			settings.UseFakeUrl = true;
			settings.BaseUrl = "http://localhost:5000";
			SetHttpClientSettings(settings);

			var response = await HttpClient.GetAsync("/health");

			Assert.AreEqual("{\"status\":\"ok\"}", response);
		}

		/// <summary>
		/// Verifies POST requests return fake responses when fake mode is enabled.
		/// </summary>
		[Test]
		public async Task PostJsonAsync_ReturnsFakeResponse()
		{
			var settings = UnityEngine.ScriptableObject.CreateInstance<NetworkSettings>();
			settings.UseFakeUrl = true;
			settings.BaseUrl = "http://localhost:5000";
			SetHttpClientSettings(settings);

			var response = await HttpClient.PostJsonAsync("/login", new LoginRequest
			{
				Username = "mimi",
				Password = "123456"
			});

			Assert.AreEqual("{\"Token\":\"fake-jwt\"}", response);
		}

		/// <summary>
		/// Simple payload for JSON serialization.
		/// </summary>
		[System.Serializable]
		private sealed class LoginRequest
		{
			public string Username;
			public string Password;
		}

		private static void SetHttpClientSettings(NetworkSettings settings)
		{
			var field = typeof(HttpClient).GetField("_settings", BindingFlags.NonPublic | BindingFlags.Static);
			field?.SetValue(null, settings);
		}
	}
}
