using System.Reflection;
using System.Threading.Tasks;
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

			await UniTask.Yield();

			Assert.AreEqual("fake-jwt", token);
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

			await UniTask.Yield();

			Assert.AreEqual("Invalid credentials", error);
		}

		private static void SetHttpClientSettings(NetworkSettings settings)
		{
			var field = typeof(HttpClient).GetField("_settings", BindingFlags.NonPublic | BindingFlags.Static);
			field?.SetValue(null, settings);
		}
	}
}
