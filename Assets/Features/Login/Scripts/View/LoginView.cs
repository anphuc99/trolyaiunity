using Core.Infrastructure.Views;
using Features.Login.Events;
using Features.Login.Infrastructure.Attributes;
using Features.Login.Model;
using Features.Login.Requests;
using UnityEngine;
using UnityEngine.UI;

namespace Features.Login.View
{
	/// <summary>
	/// View for Login.
	/// </summary>
	public sealed class LoginView : BaseView
	{
		[SerializeField]
		private InputField _usernameInput;

		[SerializeField]
		private InputField _passwordInput;

		[SerializeField]
		private Text _statusLabel;

		/// <summary>
		/// Sends the login request with the current input values.
		/// </summary>
		public void SubmitLogin()
		{
			if (_usernameInput == null || _passwordInput == null)
			{
				Debug.LogWarning("[LoginView] Missing input field references.", this);
				return;
			}

			var payload = new LoginRequestPayload
			{
				Username = _usernameInput.text,
				Password = _passwordInput.text
			};

			SendRequest(LoginRequests.SubmitLogin, payload);
			SetStatus("Logging in...");
		}

		/// <summary>
		/// Handles login success events.
		/// </summary>
		/// <param name="payload">JWT token string.</param>
		[OnEvent(LoginEvents.LoginSucceeded)]
		private void OnLoginSucceeded(object payload)
		{
			SetStatus("Login success.");
			Debug.Log("[LoginView] Login succeeded. Token: " + payload, this);
		}

		/// <summary>
		/// Handles login failure events.
		/// </summary>
		/// <param name="payload">Error message.</param>
		[OnEvent(LoginEvents.LoginFailed)]
		private void OnLoginFailed(object payload)
		{
			var message = payload as string ?? "Login failed.";
			SetStatus(message);
			Debug.LogWarning("[LoginView] Login failed: " + message, this);
		}

		private void SetStatus(string message)
		{
			if (_statusLabel == null)
			{
				return;
			}

			_statusLabel.text = message;
		}
	}
}
