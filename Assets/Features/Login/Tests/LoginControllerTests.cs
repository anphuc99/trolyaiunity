using System.Collections.Generic;
using Core.Infrastructure.Events;
using Features.Login.Controller;
using Features.Login.Events;
using Features.Login.View;
using NUnit.Framework;

namespace Features.Login.Tests
{
	/// <summary>
	/// Unit tests for LoginController.
	/// </summary>
	public sealed class LoginControllerTests
	{
		private List<(string key, object payload)> _publishedEvents;

		[SetUp]
		public void SetUp()
		{
			EventBus.ClearAll();
			_publishedEvents = new List<(string, object)>();
			EventBus.Subscribe(LoginEvents.LoginSuccess, p => _publishedEvents.Add((LoginEvents.LoginSuccess, p)));
			EventBus.Subscribe(LoginEvents.LoginFailed, p => _publishedEvents.Add((LoginEvents.LoginFailed, p)));
		}

		[TearDown]
		public void TearDown()
		{
			EventBus.ClearAll();
		}

		[Test]
		public void HandleLogin_WithValidCredentials_PublishesLoginSuccess()
		{
			var payload = new LoginPayload("testuser", "password123");

			LoginController.HandleLogin(payload);

			Assert.AreEqual(1, _publishedEvents.Count);
			Assert.AreEqual(LoginEvents.LoginSuccess, _publishedEvents[0].key);
			Assert.AreEqual("testuser", _publishedEvents[0].payload);
		}

		[Test]
		public void HandleLogin_WithNullPayload_PublishesLoginFailed()
		{
			LoginController.HandleLogin(null);

			Assert.AreEqual(1, _publishedEvents.Count);
			Assert.AreEqual(LoginEvents.LoginFailed, _publishedEvents[0].key);
			Assert.AreEqual("Invalid login payload.", _publishedEvents[0].payload);
		}

		[Test]
		public void HandleLogin_WithEmptyUsername_PublishesLoginFailed()
		{
			var payload = new LoginPayload("", "password123");

			LoginController.HandleLogin(payload);

			Assert.AreEqual(1, _publishedEvents.Count);
			Assert.AreEqual(LoginEvents.LoginFailed, _publishedEvents[0].key);
			StringAssert.Contains("Tên đăng nhập", (string)_publishedEvents[0].payload);
		}

		[Test]
		public void HandleLogin_WithEmptyPassword_PublishesLoginFailed()
		{
			var payload = new LoginPayload("testuser", "");

			LoginController.HandleLogin(payload);

			Assert.AreEqual(1, _publishedEvents.Count);
			Assert.AreEqual(LoginEvents.LoginFailed, _publishedEvents[0].key);
			StringAssert.Contains("Mật mã", (string)_publishedEvents[0].payload);
		}

		[Test]
		public void HandleLogin_WithWhitespaceUsername_PublishesLoginFailed()
		{
			var payload = new LoginPayload("   ", "password123");

			LoginController.HandleLogin(payload);

			Assert.AreEqual(1, _publishedEvents.Count);
			Assert.AreEqual(LoginEvents.LoginFailed, _publishedEvents[0].key);
		}

		[Test]
		public void HandleLogin_WithInvalidPayloadType_PublishesLoginFailed()
		{
			LoginController.HandleLogin("not a LoginPayload");

			Assert.AreEqual(1, _publishedEvents.Count);
			Assert.AreEqual(LoginEvents.LoginFailed, _publishedEvents[0].key);
		}

		[Test]
		public void HandleSocialLogin_WithValidProvider_PublishesLoginSuccess()
		{
			LoginController.HandleSocialLogin("google");

			Assert.AreEqual(1, _publishedEvents.Count);
			Assert.AreEqual(LoginEvents.LoginSuccess, _publishedEvents[0].key);
			Assert.AreEqual("google", _publishedEvents[0].payload);
		}

		[Test]
		public void HandleSocialLogin_WithNullProvider_PublishesLoginFailed()
		{
			LoginController.HandleSocialLogin(null);

			Assert.AreEqual(1, _publishedEvents.Count);
			Assert.AreEqual(LoginEvents.LoginFailed, _publishedEvents[0].key);
		}

		[Test]
		public void HandleSocialLogin_WithEmptyProvider_PublishesLoginFailed()
		{
			LoginController.HandleSocialLogin("");

			Assert.AreEqual(1, _publishedEvents.Count);
			Assert.AreEqual(LoginEvents.LoginFailed, _publishedEvents[0].key);
		}

		[Test]
		public void HandleForgotPassword_DoesNotPublishAnyEvent()
		{
			LoginController.HandleForgotPassword(null);

			Assert.AreEqual(0, _publishedEvents.Count);
		}

		[Test]
		public void HandleRegister_DoesNotPublishAnyEvent()
		{
			LoginController.HandleRegister(null);

			Assert.AreEqual(0, _publishedEvents.Count);
		}
	}
}
