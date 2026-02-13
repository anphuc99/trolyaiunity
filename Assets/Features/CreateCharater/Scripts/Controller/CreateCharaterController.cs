using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Infrastructure.Network;
using Features.CreateCharater.Events;
using Features.CreateCharater.Infrastructure;
using Features.CreateCharater.Infrastructure.Attributes;
using Features.CreateCharater.Model;
using Features.CreateCharater.Requests;
using Newtonsoft.Json;
using UnityEngine;

namespace Features.CreateCharater.Controller
{
	/// <summary>
	/// Controller for CreateCharater.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.CreateCharaterGameplay)]
	public static class CreateCharaterController
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
			CreateCharaterModel.AvailablePersonalities.Clear();
		}

		/// <summary>
		/// Sample request handler that echoes payload to a view event.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(CreateCharaterRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(CreateCharaterEvents.Echoed, payload);
		}

		/// <summary>
		/// Fetches personalities from the server.
		/// </summary>
		[Request(CreateCharaterRequests.FetchPersonalities)]
		public static void FetchPersonalities()
		{
			_ = FetchPersonalitiesAsync();
		}

		private static async Task FetchPersonalitiesAsync()
		{
			var json = await HttpClient.GetTaskAsync(NetworkEndpoints.Personalities);
			if (string.IsNullOrWhiteSpace(json))
			{
				EventBus.Publish(CreateCharaterEvents.PersonalitiesLoaded, null);
				return;
			}

			try
			{
				var data = JsonConvert.DeserializeObject<List<PersonalityData>>(json);
				CreateCharaterModel.AvailablePersonalities = data ?? new List<PersonalityData>();
				EventBus.Publish(CreateCharaterEvents.PersonalitiesLoaded, CreateCharaterModel.AvailablePersonalities);
			}
			catch (System.Exception e)
			{
				Debug.LogError($"[CreateCharaterController] Failed to parse personalities: {e.Message}");
				EventBus.Publish(CreateCharaterEvents.PersonalitiesLoaded, null);
			}
		}

		/// <summary>
		/// Submits a new character to the server.
		/// </summary>
		/// <param name="payload">Character creation payload.</param>
		[Request(CreateCharaterRequests.SubmitCharacter)]
		public static void SubmitCharacter(CreateCharacterPayload payload)
		{
			if (payload == null)
			{
				EventBus.Publish(CreateCharaterEvents.CharacterCreationFailed, "Dữ liệu không hợp lệ.");
				return;
			}

			_ = SubmitCharacterAsync(payload);
		}

		private static async Task SubmitCharacterAsync(CreateCharacterPayload payload)
		{
			var result = await HttpClient.PostJsonTaskAsync(NetworkEndpoints.Characters, payload);
			if (string.IsNullOrWhiteSpace(result))
			{
				EventBus.Publish(CreateCharaterEvents.CharacterCreationFailed, "Server không phản hồi.");
				return;
			}

			// Assuming a simple success/fail check based on response content or similar
			// For now, if we got a response, we'll treat it as success per fake server
			EventBus.Publish(CreateCharaterEvents.CharacterCreationSucceeded);
		}
	}
}
