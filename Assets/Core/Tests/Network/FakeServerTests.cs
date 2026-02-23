using NUnit.Framework;
using Core.Infrastructure.Network;
using Newtonsoft.Json;

namespace Core.Tests.Network
{
	/// <summary>
	/// Unit tests for <see cref="FakeServer"/>.
	/// </summary>
	public sealed class FakeServerTests
	{
		[SetUp]
		public void SetUp()
		{
			FakeServer.ResetToDefaults();
		}

		[TearDown]
		public void TearDown()
		{
			FakeServer.ResetToDefaults();
		}

		/// <summary>
		/// Ensures default fake responses are available.
		/// </summary>
		[Test]
		public void TryGetResponse_ReturnsDefaultEntry()
		{
			var found = FakeServer.TryGetResponse("GET", NetworkEndpoints.Health, null, out var response);

			Assert.IsTrue(found);
			Assert.AreEqual("{\"status\":\"ok\"}", response);
		}

		/// <summary>
		/// Ensures fallback response is returned when no handler is registered.
		/// </summary>
		[Test]
		public void TryGetResponse_ReturnsFallbackWhenMissing()
		{
			var found = FakeServer.TryGetResponse("GET", "/unknown", null, out var response);

			Assert.IsFalse(found);
			Assert.AreEqual("{}", response);
		}

		/// <summary>
		/// Ensures login credentials are validated by the fake server.
		/// </summary>
		[Test]
		public void TryGetResponse_ValidatesLoginCredentials()
		{
			var payload = "{\"username\":\"mimi\",\"password\":\"123456\"}";

			var found = FakeServer.TryGetResponse("POST", NetworkEndpoints.Login, payload, out var response);

			Assert.IsTrue(found);
			StringAssert.Contains("\"accessToken\":\"fake-access-token\"", response);
			StringAssert.Contains("\"refreshToken\":\"fake-refresh-token\"", response);
		}

		/// <summary>
		/// Ensures a custom fake handler can be registered and resolved.
		/// </summary>
		[Test]
		public void Register_OverridesResponse()
		{
			FakeServer.Register("POST", NetworkEndpoints.Login, _ => "{\"token\":\"override\"}");

			var found = FakeServer.TryGetResponse("POST", NetworkEndpoints.Login, "{}", out var response);

			Assert.IsTrue(found);
			Assert.AreEqual("{\"token\":\"override\"}", response);
		}

		/// <summary>
		/// Ensures token validation accepts valid tokens.
		/// </summary>
		[Test]
		public void TryGetResponse_ValidatesTokenAccepted()
		{
			var payload = "{\"refreshToken\":\"fake-refresh-token\"}";

			var found = FakeServer.TryGetResponse("POST", NetworkEndpoints.TokenValidate, payload, out var response);

			Assert.IsTrue(found);
			var validation = JsonConvert.DeserializeObject<TokenValidationResponse>(response);
			Assert.IsTrue(validation.Valid);
			Assert.AreEqual("new-refresh-token", validation.RefreshToken);
			Assert.IsTrue(string.IsNullOrWhiteSpace(validation.Message));
		}

		/// <summary>
		/// Ensures token validation rejects invalid tokens.
		/// </summary>
		[Test]
		public void TryGetResponse_ValidatesTokenRejected()
		{
			var payload = "{\"refreshToken\":\"bad-token\"}";

			var found = FakeServer.TryGetResponse("POST", NetworkEndpoints.TokenValidate, payload, out var response);

			Assert.IsTrue(found);
			var validation = JsonConvert.DeserializeObject<TokenValidationResponse>(response);
			Assert.IsFalse(validation.Valid);
			Assert.AreEqual("Invalid refresh token", validation.Message);
		}

		/// <summary>
		/// Ensures token validation rejects expired tokens.
		/// </summary>
		[Test]
		public void TryGetResponse_ValidatesTokenExpired()
		{
			var payload = "{\"refreshToken\":\"fake-refresh-token-expired\"}";

			var found = FakeServer.TryGetResponse("POST", NetworkEndpoints.TokenValidate, payload, out var response);

			Assert.IsTrue(found);
			var validation = JsonConvert.DeserializeObject<TokenValidationResponse>(response);
			Assert.IsFalse(validation.Valid);
			Assert.AreEqual("Refresh token has expired", validation.Message);
		}

		[System.Serializable]
		private sealed class TokenValidationResponse
		{
			public bool Valid;
			public string Message;
			public string RefreshToken;
		}
	}
}
