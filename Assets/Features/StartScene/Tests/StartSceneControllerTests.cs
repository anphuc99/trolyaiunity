using System.Reflection;
using System.Threading.Tasks;
using Core.Infrastructure.Authentication;
using Core.Infrastructure.Events;
using Core.Infrastructure.Network;
using Cysharp.Threading.Tasks;
using Features.StartScene.Controller;
using Features.StartScene.Events;
using NUnit.Framework;

namespace Features.StartScene.Tests
{
	/// <summary>
	/// Basic tests for StartScene controller.
	/// </summary>
	public sealed class StartSceneControllerTests
	{
		[SetUp]
		public void SetUp()
		{
			EventBus.ClearAll();
			FakeServer.ResetToDefaults();
			AuthTokenModel.AccessToken = null;
			AuthTokenModel.RefreshToken = null;
			var settings = UnityEngine.ScriptableObject.CreateInstance<NetworkSettings>();
			settings.UseFakeUrl = true;
			settings.BaseUrl = "http://localhost:5000";
			SetHttpClientSettings(settings);
		}

		[TearDown]
		public void TearDown()
		{
			EventBus.ClearAll();
			FakeServer.ResetToDefaults();
			AuthTokenModel.AccessToken = null;
			AuthTokenModel.RefreshToken = null;
			SetHttpClientSettings(null);
		}

		[Test]
		public async Task CheckToken_PublishesAcceptedForValidToken()
		{
			string acceptedToken = null;
			EventBus.Subscribe(StartSceneEvents.TokenAccepted, payload => acceptedToken = payload as string);

			AuthTokenModel.RefreshToken = "fake-refresh-token";
			await StartSceneController.CheckTokenAsync();

			Assert.AreEqual("new-access-token", acceptedToken);
			Assert.AreEqual("new-access-token", AuthTokenModel.AccessToken);
			Assert.AreEqual("new-refresh-token", AuthTokenModel.RefreshToken);
		}

		[Test]
		public async Task CheckToken_PublishesRejectedForMissingToken()
		{
			string error = null;
			EventBus.Subscribe(StartSceneEvents.TokenRejected, payload => error = payload as string);

			AuthTokenModel.RefreshToken = null;
			await StartSceneController.CheckTokenAsync();

			Assert.AreEqual("Missing refresh token.", error);
		}

		[Test]
		public async Task CheckToken_PublishesRejectedForExpiredToken()
		{
			string error = null;
			EventBus.Subscribe(StartSceneEvents.TokenRejected, payload => error = payload as string);

			AuthTokenModel.RefreshToken = "fake-jwt-expired";
			await StartSceneController.CheckTokenAsync();

			Assert.AreEqual("Token expired", error);
		}

		private static void SetHttpClientSettings(NetworkSettings settings)
		{
			var field = typeof(HttpClient).GetField("_settings", BindingFlags.NonPublic | BindingFlags.Static);
			field?.SetValue(null, settings);
		}
	}
}
