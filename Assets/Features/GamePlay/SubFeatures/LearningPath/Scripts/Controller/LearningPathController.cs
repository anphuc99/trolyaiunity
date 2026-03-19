using System;
using System.Threading.Tasks;
using Core.Infrastructure.Network;
using Features.GamePlay.SubFeatures.LearningPath.Events;
using Features.GamePlay.SubFeatures.LearningPath.Infrastructure;
using Features.GamePlay.SubFeatures.LearningPath.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.LearningPath.Model;
using Features.GamePlay.SubFeatures.LearningPath.Requests;
using Newtonsoft.Json;

namespace Features.GamePlay.SubFeatures.LearningPath.Controller
{
	/// <summary>
	/// Controller for LearningPath.
	/// Manages CRUD operations for user-designed learning paths.
	/// Each learning path stores a context (story/scenario description)
	/// and a comma-separated vocabulary list used for dialogue.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay)]
	public static class LearningPathController
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
			LearningPathState.CachedList = new LearningPathListResponsePayload();
			LearningPathState.EditingLearningPathId = null;
		}

		/// <summary>
		/// Installs the subcontroller and publishes installed event.
		/// </summary>
		public static void Install()
		{
			EventBus.Publish(LearningPathEvents.Installed, null);
		}

		/// <summary>
		/// Uninstalls the subcontroller and publishes uninstalled event.
		/// </summary>
		public static void Uninstall()
		{
			EventBus.Publish(LearningPathEvents.Uninstalled, null);
		}

		/// <summary>
		/// Binds parent-provided signals for this subfeature.
		/// </summary>
		/// <param name="signals">Signals implemented by the parent feature.</param>
		public static void SetParentSignals(LearningPathParentSignals signals)
		{
			LearningPathState.ParentSignals = signals;
		}

		/// <summary>
		/// Sample request handler that echoes payload to a view event and parent signal.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(LearningPathRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(LearningPathEvents.Echoed, payload);
			LearningPathState.ParentSignals?.OnEchoed?.Invoke(payload);
		}

		/// <summary>
		/// Loads the list of learning paths from the server.
		/// </summary>
		/// <param name="payload">Unused.</param>
		[Request(LearningPathRequests.LoadList)]
		public static void HandleLoadList(object payload)
		{
			_ = LoadListInternalAsync();
		}

		/// <summary>
		/// Creates a new learning path.
		/// </summary>
		/// <param name="payload">Create request payload.</param>
		[Request(LearningPathRequests.Create)]
		public static void HandleCreate(LearningPathCreateRequestPayload payload)
		{
			if (payload == null || string.IsNullOrWhiteSpace(payload.Context))
			{
				PublishError("Context is required.");
				return;
			}

			if (string.IsNullOrWhiteSpace(payload.Vocabulary))
			{
				PublishError("Vocabulary is required.");
				return;
			}

			_ = CreateInternalAsync(payload);
		}

		/// <summary>
		/// Updates an existing learning path.
		/// </summary>
		/// <param name="payload">Update request payload.</param>
		[Request(LearningPathRequests.Update)]
		public static void HandleUpdate(LearningPathUpdateRequestPayload payload)
		{
			if (payload == null || payload.LearningPathId <= 0)
			{
				PublishError("Invalid learning path id for update.");
				return;
			}

			if (string.IsNullOrWhiteSpace(payload.Context))
			{
				PublishError("Context is required.");
				return;
			}

			if (string.IsNullOrWhiteSpace(payload.Vocabulary))
			{
				PublishError("Vocabulary is required.");
				return;
			}

			_ = UpdateInternalAsync(payload);
		}

		/// <summary>
		/// Selects a learning path for editing and publishes data to the view.
		/// </summary>
		/// <param name="payload">Edit request payload.</param>
		[Request(LearningPathRequests.Edit)]
		public static void HandleEdit(LearningPathEditRequestPayload payload)
		{
			if (payload == null || payload.LearningPathId <= 0)
			{
				PublishError("Invalid learning path id for edit.");
				return;
			}

			var item = FindById(payload.LearningPathId);
			if (item == null)
			{
				PublishError("Learning path not found for edit.");
				return;
			}

			LearningPathState.EditingLearningPathId = item.Id;
			EventBus.Publish(LearningPathEvents.EditLoaded, new LearningPathEditPayload
			{
				LearningPathId = item.Id,
				Context = item.Context,
				Vocabulary = item.Vocabulary
			});
		}

		private static async Task LoadListInternalAsync()
		{
			try
			{
				var responseJson = await HttpClient.GetTaskAsync(NetworkEndpoints.LearningPaths);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					PublishError("Empty learning paths response from server.");
					return;
				}

				var response = JsonConvert.DeserializeObject<LearningPathListResponsePayload>(responseJson)
					?? new LearningPathListResponsePayload();
				response.LearningPaths ??= new System.Collections.Generic.List<LearningPathPayload>();

				LearningPathState.CachedList = response;
				EventBus.Publish(LearningPathEvents.ListLoaded, response);
			}
			catch (Exception exception)
			{
				PublishError("Failed to load learning paths: " + exception.Message);
			}
		}

		private static async Task CreateInternalAsync(LearningPathCreateRequestPayload payload)
		{
			try
			{
				var responseJson = await HttpClient.PostJsonTaskAsync(NetworkEndpoints.LearningPaths, payload);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					PublishError("Empty create learning path response from server.");
					return;
				}

				var response = JsonConvert.DeserializeObject<LearningPathSingleResponsePayload>(responseJson);
				if (response == null || response.LearningPath == null)
				{
					PublishError("Server returned invalid learning path payload.");
					return;
				}

				LearningPathState.EditingLearningPathId = null;
				EventBus.Publish(LearningPathEvents.Saved, new LearningPathSavedPayload
				{
					LearningPathId = response.LearningPath.Id,
					IsUpdate = false
				});

				await LoadListInternalAsync();
			}
			catch (Exception exception)
			{
				PublishError("Failed to create learning path: " + exception.Message);
			}
		}

		private static async Task UpdateInternalAsync(LearningPathUpdateRequestPayload payload)
		{
			try
			{
				var endpoint = NetworkEndpoints.LearningPaths + "/" + payload.LearningPathId;
				var responseJson = await HttpClient.PutJsonTaskAsync(endpoint, payload);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					PublishError("Empty update learning path response from server.");
					return;
				}

				var response = JsonConvert.DeserializeObject<LearningPathSingleResponsePayload>(responseJson);
				if (response == null || response.LearningPath == null)
				{
					PublishError("Server returned invalid learning path payload.");
					return;
				}

				LearningPathState.EditingLearningPathId = null;
				EventBus.Publish(LearningPathEvents.Saved, new LearningPathSavedPayload
				{
					LearningPathId = response.LearningPath.Id,
					IsUpdate = true
				});

				await LoadListInternalAsync();
			}
			catch (Exception exception)
			{
				PublishError("Failed to update learning path: " + exception.Message);
			}
		}

		private static LearningPathPayload FindById(int id)
		{
			if (LearningPathState.CachedList?.LearningPaths == null)
			{
				return null;
			}

			for (var i = 0; i < LearningPathState.CachedList.LearningPaths.Count; i++)
			{
				var item = LearningPathState.CachedList.LearningPaths[i];
				if (item != null && item.Id == id)
				{
					return item;
				}
			}

			return null;
		}

		private static void PublishError(string message)
		{
			EventBus.Publish(LearningPathEvents.RequestFailed, new LearningPathErrorPayload
			{
				Message = message
			});
		}
	}
}
