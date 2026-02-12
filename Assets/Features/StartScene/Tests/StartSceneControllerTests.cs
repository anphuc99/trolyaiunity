using System.Reflection;
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
			AuthTokenModel.Token = null;
			SetHttpClientSettings(new NetworkSettings
			{
				UseFakeUrl = true,
				BaseUrl = "http://localhost:5000"
			});
		}

		[TearDown]
		public void TearDown()
		{
			EventBus.ClearAll();
			FakeServer.ResetToDefaults();
			AuthTokenModel.Token = null;
			SetHttpClientSettings(null);
		}

		[Test]
		public async UniTask CheckToken_PublishesAcceptedForValidToken()
		{
			string acceptedToken = null;
			EventBus.Subscribe(StartSceneEvents.TokenAccepted, payload => acceptedToken = payload as string);

			AuthTokenModel.Token = "fake-jwt";
			StartSceneController.CheckToken();

			await UniTask.Yield();

			Assert.AreEqual("fake-jwt", acceptedToken);
		}

		[Test]
		public async UniTask CheckToken_PublishesRejectedForMissingToken()
		{
			string error = null;
			EventBus.Subscribe(StartSceneEvents.TokenRejected, payload => error = payload as string);

			AuthTokenModel.Token = null;
			StartSceneController.CheckToken();

			await UniTask.Yield();

			Assert.AreEqual("Missing token.", error);
		}

		[Test]
		public async UniTask CheckToken_PublishesRejectedForExpiredToken()
		{
			string error = null;
			EventBus.Subscribe(StartSceneEvents.TokenRejected, payload => error = payload as string);

			AuthTokenModel.Token = "fake-jwt-expired";
			StartSceneController.CheckToken();

			await UniTask.Yield();

			Assert.AreEqual("Token expired", error);
		}

		private static void SetHttpClientSettings(NetworkSettings settings)
		{
			var field = typeof(HttpClient).GetField("_settings", BindingFlags.NonPublic | BindingFlags.Static);
			field?.SetValue(null, settings);
		}
	}
}
