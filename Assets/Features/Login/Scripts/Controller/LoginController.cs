using Core.Infrastructure.Network;
using Core.Infrastructure.Scenes;
using System.Threading.Tasks;
using Features.Login.Events;
using Features.Login.Infrastructure;
using Features.Login.Infrastructure.Attributes;
using Features.Login.Model;
using Features.Login.Requests;
using Newtonsoft.Json;
using UnityEngine;

namespace Features.Login.Controller
{
	/// <summary>
	/// Controller for Login.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.LoginGameplay)]
	public static class LoginController
	{
		/// <summary>
		/// Called when the controller scope is entered.
		/// </summary>
		[Core.Infrastructure.Attributes.ControllerInit]
		public static void OnEnterScope()
		{
		}

		/// <summary>
		/// Called when the controller scope is exited.
		/// </summary>
		[Core.Infrastructure.Attributes.ControllerShutdown]
		public static void OnExitScope()
		{
		}

		/// <summary>
		/// Handles login requests from the view.
		/// </summary>
		/// <param name="payload">Login payload from the view.</param>
		[Features.Login.Infrastructure.Attributes.Request(LoginRequests.SubmitLogin)]
		public static void SubmitLogin(LoginRequestPayload payload)
		{
			if (payload == null)
			{
				EventBus.Publish(LoginEvents.LoginFailed, "Missing login payload.");
				return;
			}

			_ = PerformLoginAsync(payload);
		}

		/// <summary>
		/// Performs the login request and publishes the outcome.
		/// </summary>
		/// <param name="payload">Login payload.</param>
		/// <returns>Awaitable task.</returns>
		public static Task PerformLoginAsync(LoginRequestPayload payload)
		{
			if (payload == null)
			{
				EventBus.Publish(LoginEvents.LoginFailed, "Missing login payload.");
				return Task.CompletedTask;
			}

			return PerformLoginInternalAsync(payload);
		}

		private static async Task PerformLoginInternalAsync(LoginRequestPayload payload)
		{
			var responseJson = await HttpClient.PostJsonTaskAsync("/login", payload);
			if (string.IsNullOrWhiteSpace(responseJson))
			{
				EventBus.Publish(LoginEvents.LoginFailed, "Empty server response.");
				return;
			}

			var response = JsonConvert.DeserializeObject<LoginResponsePayload>(responseJson);
			if (response != null && !string.IsNullOrWhiteSpace(response.Token))
			{
				EventBus.Publish(LoginEvents.LoginSucceeded, response.Token);
				if (Application.isPlaying)
				{
					LoadScene.ByScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay);
				}
				return;
			}

			var errorMessage = response != null && !string.IsNullOrWhiteSpace(response.Error)
				? response.Error
				: "Invalid credentials.";
			EventBus.Publish(LoginEvents.LoginFailed, errorMessage);
		}
	}
}
