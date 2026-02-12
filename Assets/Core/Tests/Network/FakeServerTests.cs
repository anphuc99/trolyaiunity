using NUnit.Framework;
using Core.Infrastructure.Network;

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
			var found = FakeServer.TryGetResponse("GET", "/health", null, out var response);

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
			var payload = "{\"Username\":\"mimi\",\"Password\":\"123456\"}";

			var found = FakeServer.TryGetResponse("POST", "/login", payload, out var response);

			Assert.IsTrue(found);
			Assert.AreEqual("{\"Token\":\"fake-jwt\"}", response);
		}

		/// <summary>
		/// Ensures a custom fake handler can be registered and resolved.
		/// </summary>
		[Test]
		public void Register_OverridesResponse()
		{
			FakeServer.Register("POST", "/login", _ => "{\"token\":\"override\"}");

			var found = FakeServer.TryGetResponse("POST", "/login", "{}", out var response);

			Assert.IsTrue(found);
			Assert.AreEqual("{\"token\":\"override\"}", response);
		}
	}
}
