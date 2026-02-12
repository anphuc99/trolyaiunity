using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Infrastructure.Authentication;
using Core.Infrastructure.Network;
using Core.Infrastructure.Scenes;
using Features.StartScene.Events;
using Features.StartScene.Infrastructure;
using Features.StartScene.Infrastructure.Attributes;
using Features.StartScene.Model;
using Features.StartScene.Requests;
using Newtonsoft.Json;
using UnityEngine;

namespace Features.StartScene.Controller
{
	/// <summary>
	/// Controller for StartScene.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.StartSceneGameplay)]
	public static class StartSceneController
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
		/// Sample request handler that echoes payload to a view event.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(StartSceneRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(StartSceneEvents.Echoed, payload);
		}

		/// <summary>
		/// Checks the current auth token with the server.
		/// </summary>
		[Request(StartSceneRequests.CheckToken)]
		public static void CheckToken()
		{
			_ = CheckTokenAsync();
		}

		/// <summary>
		/// Performs token validation and triggers the appropriate scene flow.
		/// </summary>
		/// <returns>Awaitable task.</returns>
		public static Task CheckTokenAsync()
		{
			return CheckTokenInternalAsync();
		}

		private static async Task CheckTokenInternalAsync()
		{
			var token = AuthTokenModel.Token;
			if (string.IsNullOrWhiteSpace(token))
			{
				RejectToken("Missing token.");
				return;
			}

			var payload = new TokenValidationRequestPayload
			{
				Token = token
			};

			var responseJson = await HttpClient.PostJsonTaskAsync(NetworkEndpoints.TokenValidate, payload);
			if (string.IsNullOrWhiteSpace(responseJson))
			{
				RejectToken("Empty validation response.");
				return;
			}

			var response = JsonConvert.DeserializeObject<TokenValidationResponsePayload>(responseJson);
			if (response != null && response.Valid)
			{
				AcceptToken(token);
				return;
			}

			var errorMessage = response != null && !string.IsNullOrWhiteSpace(response.Error)
				? response.Error
				: "Token rejected.";
			RejectToken(errorMessage);
		}

		private static void AcceptToken(string token)
		{
			EventBus.Publish(StartSceneEvents.TokenAccepted, token);
			if (Application.isPlaying)
			{
				_ = FetchCharactersAndRedirectAsync();
			}
		}

		private static async Task FetchCharactersAndRedirectAsync()
		{
			var responseJson = await HttpClient.GetTaskAsync(NetworkEndpoints.Characters);
			if (string.IsNullOrWhiteSpace(responseJson))
			{
				LoadScene.ByScope(Core.Infrastructure.Attributes.ControllerScopeKey.CreateCharaterGameplay);
				return;
			}

			try
			{
				var characters = JsonConvert.DeserializeObject<List<object>>(responseJson);
				if (characters != null && characters.Count > 0)
				{
					LoadScene.ByScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay);
				}
				else
				{
					LoadScene.ByScope(Core.Infrastructure.Attributes.ControllerScopeKey.CreateCharaterGameplay);
				}
			}
			catch
			{
				LoadScene.ByScope(Core.Infrastructure.Attributes.ControllerScopeKey.CreateCharaterGameplay);
			}
		}

		private static void RejectToken(string message)
		{
			EventBus.Publish(StartSceneEvents.TokenRejected, message);
			if (Application.isPlaying)
			{
				LoadScene.ByScope(Core.Infrastructure.Attributes.ControllerScopeKey.LoginGameplay);
			}
		}
	}
}
