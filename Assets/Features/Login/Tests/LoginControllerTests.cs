using System.Reflection;
using System.Threading.Tasks;
using Core.Infrastructure.Authentication;
using Core.Infrastructure.Events;
using Core.Infrastructure.Network;
using Cysharp.Threading.Tasks;
using Features.Login.Controller;
using Features.Login.Events;
using Features.Login.Model;
using NUnit.Framework;

namespace Features.Login.Tests
{
	/// <summary>
	/// Basic tests for Login controller.
	/// </summary>
	public sealed class LoginControllerTests
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
		public async Task SubmitLogin_PublishesSuccess()
		{
			string token = null;
			EventBus.Subscribe(LoginEvents.LoginSucceeded, payload => token = payload as string);

			LoginController.SubmitLogin(new LoginRequestPayload
			{
				Username = "mimi",
				Password = "123456"
			});

			await UniTask.WaitUntil(() => token != null);
			Assert.AreEqual("fake-access-token", token);
			Assert.AreEqual("fake-access-token", AuthTokenModel.AccessToken);
			Assert.AreEqual("fake-refresh-token", AuthTokenModel.RefreshToken);
		}

		[Test]
		public async Task SubmitLogin_PublishesFailure()
		{
			string error = null;
			EventBus.Subscribe(LoginEvents.LoginFailed, payload => error = payload as string);

			LoginController.SubmitLogin(new LoginRequestPayload
			{
				Username = "mimi",
				Password = "wrong"
			});

			await UniTask.WaitUntil(() => error != null);
			Assert.AreEqual("Invalid credentials", error);
			Assert.IsTrue(string.IsNullOrWhiteSpace(AuthTokenModel.AccessToken));
			Assert.IsTrue(string.IsNullOrWhiteSpace(AuthTokenModel.RefreshToken));
		}

		private static void SetHttpClientSettings(NetworkSettings settings)
		{
			var field = typeof(HttpClient).GetField("_settings", BindingFlags.NonPublic | BindingFlags.Static);
			field?.SetValue(null, settings);
		}
	}
}
