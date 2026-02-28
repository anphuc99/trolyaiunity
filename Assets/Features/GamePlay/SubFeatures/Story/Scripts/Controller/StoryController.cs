using System;
using System.Threading.Tasks;
using Core.Infrastructure.Network;
using Features.GamePlay.SubFeatures.Story.Events;
using Features.GamePlay.SubFeatures.Story.Infrastructure;
using Features.GamePlay.SubFeatures.Story.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Story.Model;
using Features.GamePlay.SubFeatures.Story.Requests;
using Newtonsoft.Json;

namespace Features.GamePlay.SubFeatures.Story.Controller
{
	/// <summary>
	/// Controller for Story.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay)]
	public static class StoryController
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
			StoryState.CachedList = new StoryListResponsePayload();
			StoryState.EditingStoryId = null;
		}

		/// <summary>
		/// Installs the subcontroller.
		/// </summary>
		public static void Install()
		{
			EventBus.Publish(StoryEvents.Installed, null);
		}

		/// <summary>
		/// Uninstalls the subcontroller and clears bound parent signals.
		/// </summary>
		public static void Uninstall()
		{
			EventBus.Publish(StoryEvents.Uninstalled, null);
		}

		/// <summary>
		/// Binds parent-provided signals for this subfeature.
		/// </summary>
		/// <param name="signals">Signals implemented by the parent feature.</param>
		public static void SetParentSignals(StoryParentSignals signals)
		{
			StoryState.ParentSignals = signals;
		}

		/// <summary>
		/// Sample request handler that echoes payload to a view event and parent signal.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(StoryRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(StoryEvents.Echoed, payload);
			StoryState.ParentSignals?.OnEchoed?.Invoke(payload);
		}

		/// <summary>
		/// Loads story list from server.
		/// </summary>
		[Request(StoryRequests.LoadStories)]
		public static void HandleLoadStories(object payload)
		{
			_ = LoadStoriesInternalAsync();
		}

		/// <summary>
		/// Creates a new story.
		/// </summary>
		/// <param name="payload">Create story payload.</param>
		[Request(StoryRequests.CreateStory)]
		public static void HandleCreateStory(StoryCreateRequestPayload payload)
		{
			if (payload == null || string.IsNullOrWhiteSpace(payload.Name))
			{
				PublishError("Story name is required.");
				return;
			}

			_ = CreateStoryInternalAsync(payload);
		}

		/// <summary>
		/// Updates an existing story.
		/// </summary>
		/// <param name="payload">Update story payload.</param>
		[Request(StoryRequests.UpdateStory)]
		public static void HandleUpdateStory(StoryUpdateRequestPayload payload)
		{
			if (payload == null || payload.StoryId <= 0)
			{
				PublishError("Invalid story id for update.");
				return;
			}

			if (string.IsNullOrWhiteSpace(payload.Name))
			{
				PublishError("Story name is required.");
				return;
			}

			_ = UpdateStoryInternalAsync(payload);
		}

		/// <summary>
		/// Selects a story for editing and publishes data to the view.
		/// </summary>
		/// <param name="payload">Edit request payload.</param>
		[Request(StoryRequests.EditStory)]
		public static void HandleEditStory(StoryEditRequestPayload payload)
		{
			if (payload == null || payload.StoryId <= 0)
			{
				PublishError("Invalid story id for edit.");
				return;
			}

			var story = FindStoryById(payload.StoryId);
			if (story == null)
			{
				PublishError("Story not found for edit.");
				return;
			}

			StoryState.EditingStoryId = story.Id;
			EventBus.Publish(StoryEvents.StoryEditLoaded, new StoryEditPayload
			{
				StoryId = story.Id,
				Name = story.Name,
				Description = story.Description,
				CurrentProgress = story.CurrentProgress
			});
		}

		private static async Task LoadStoriesInternalAsync()
		{
			try
			{
				var responseJson = await HttpClient.GetTaskAsync(NetworkEndpoints.Stories);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					PublishError("Empty stories response from server.");
					return;
				}

				var response = JsonConvert.DeserializeObject<StoryListResponsePayload>(responseJson) ?? new StoryListResponsePayload();
				response.Stories ??= new System.Collections.Generic.List<StoryPayload>();

				StoryState.CachedList = response;
				EventBus.Publish(StoryEvents.StoriesLoaded, response);
			}
			catch (Exception exception)
			{
				PublishError("Failed to load stories: " + exception.Message);
			}
		}

		private static async Task CreateStoryInternalAsync(StoryCreateRequestPayload payload)
		{
			try
			{
				var responseJson = await HttpClient.PostJsonTaskAsync(NetworkEndpoints.Stories, payload);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					PublishError("Empty create story response from server.");
					return;
				}

				var response = JsonConvert.DeserializeObject<StorySingleResponsePayload>(responseJson);
				if (response == null || response.Story == null)
				{
					PublishError("Server returned invalid story payload.");
					return;
				}

				StoryState.EditingStoryId = null;
				EventBus.Publish(StoryEvents.StorySaved, new StorySavedPayload
				{
					StoryId = response.Story.Id,
					IsUpdate = false
				});

				await LoadStoriesInternalAsync();
			}
			catch (Exception exception)
			{
				PublishError("Failed to create story: " + exception.Message);
			}
		}

		private static async Task UpdateStoryInternalAsync(StoryUpdateRequestPayload payload)
		{
			try
			{
				var responseJson = await HttpClient.PutJsonTaskAsync(BuildStoryEndpoint(payload.StoryId), payload);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					PublishError("Empty update story response from server.");
					return;
				}

				var response = JsonConvert.DeserializeObject<StorySingleResponsePayload>(responseJson);
				if (response == null || response.Story == null)
				{
					PublishError("Server returned invalid story payload.");
					return;
				}

				StoryState.EditingStoryId = null;
				EventBus.Publish(StoryEvents.StorySaved, new StorySavedPayload
				{
					StoryId = response.Story.Id,
					IsUpdate = true
				});

				await LoadStoriesInternalAsync();
			}
			catch (Exception exception)
			{
				PublishError("Failed to update story: " + exception.Message);
			}
		}

		private static StoryPayload FindStoryById(int storyId)
		{
			if (StoryState.CachedList?.Stories == null)
			{
				return null;
			}

			for (var i = 0; i < StoryState.CachedList.Stories.Count; i++)
			{
				var story = StoryState.CachedList.Stories[i];
				if (story != null && story.Id == storyId)
				{
					return story;
				}
			}

			return null;
		}

		private static string BuildStoryEndpoint(int storyId)
		{
			return NetworkEndpoints.Stories + "/" + storyId;
		}

		private static void PublishError(string message)
		{
			EventBus.Publish(StoryEvents.RequestFailed, new StoryErrorPayload
			{
				Message = message
			});
		}
	}
}
