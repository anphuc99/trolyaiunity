using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Infrastructure.Network;
using Features.GamePlay.SubFeatures.Story.Controller;
using Features.GamePlay.SubFeatures.Story.Events;
using Features.GamePlay.SubFeatures.Story.Model;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Features.GamePlay.SubFeatures.Story.Tests
{
	/// <summary>
	/// Tests for Story controller request and event flow.
	/// </summary>
	public sealed class StoryControllerTests
	{
		[TearDown]
		public void TearDown()
		{
			FakeServer.ResetToDefaults();
			StoryState.CachedList = new StoryListResponsePayload();
			StoryState.EditingStoryId = null;
		}

		[UnityTest]
		public System.Collections.IEnumerator HandleLoadStories_ValidResponse_PublishesStoriesLoaded()
		{
			StoryListResponsePayload listPayload = null;
			void Handler(object payload)
			{
				listPayload = payload as StoryListResponsePayload;
			}

			FakeServer.Register("GET", NetworkEndpoints.Stories,
				_ => "{\"stories\":[{\"id\":1,\"name\":\"Story\",\"description\":\"Desc\",\"currentProgress\":\"Step\"}]}"
			);

			Core.Infrastructure.Events.EventBus.Subscribe(StoryEvents.StoriesLoaded, Handler);
			try
			{
				StoryController.HandleLoadStories(null);
				yield return AwaitTask(Task.Delay(100));
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(StoryEvents.StoriesLoaded, Handler);
			}

			Assert.IsNotNull(listPayload);
			Assert.AreEqual(1, listPayload.Stories.Count);
			Assert.AreEqual(1, listPayload.Stories[0].Id);
		}

		[Test]
		public void HandleEditStory_WithCachedList_PublishesEditLoaded()
		{
			StoryState.CachedList = new StoryListResponsePayload
			{
				Stories = new List<StoryPayload>
				{
					new StoryPayload { Id = 7, Name = "Story", Description = "Desc", CurrentProgress = "Step" }
				}
			};

			StoryEditPayload editPayload = null;
			void Handler(object payload)
			{
				editPayload = payload as StoryEditPayload;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(StoryEvents.StoryEditLoaded, Handler);
			try
			{
				StoryController.HandleEditStory(new StoryEditRequestPayload { StoryId = 7 });
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(StoryEvents.StoryEditLoaded, Handler);
			}

			Assert.IsNotNull(editPayload);
			Assert.AreEqual(7, editPayload.StoryId);
			Assert.AreEqual("Story", editPayload.Name);
		}

		[Test]
		public void HandleEditStory_InvalidId_PublishesError()
		{
			StoryErrorPayload errorPayload = null;
			void Handler(object payload)
			{
				errorPayload = payload as StoryErrorPayload;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(StoryEvents.RequestFailed, Handler);
			try
			{
				StoryController.HandleEditStory(new StoryEditRequestPayload { StoryId = -1 });
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(StoryEvents.RequestFailed, Handler);
			}

			Assert.IsNotNull(errorPayload);
		}

		[UnityTest]
		public System.Collections.IEnumerator HandleCreateStory_ValidResponse_PublishesStoriesLoaded()
		{
			StoryListResponsePayload listPayload = null;
			void Handler(object payload)
			{
				listPayload = payload as StoryListResponsePayload;
			}

			FakeServer.Register("POST", NetworkEndpoints.Stories,
				_ => "{\"story\":{\"id\":2,\"name\":\"New\",\"description\":\"Desc\",\"currentProgress\":\"Step\"}}"
			);
			FakeServer.Register("GET", NetworkEndpoints.Stories,
				_ => "{\"stories\":[{\"id\":2,\"name\":\"New\",\"description\":\"Desc\",\"currentProgress\":\"Step\"}]}"
			);

			Core.Infrastructure.Events.EventBus.Subscribe(StoryEvents.StoriesLoaded, Handler);
			try
			{
				StoryController.HandleCreateStory(new StoryCreateRequestPayload
				{
					Name = "New",
					Description = "Desc",
					CurrentProgress = "Step"
				});
				yield return AwaitTask(Task.Delay(100));
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(StoryEvents.StoriesLoaded, Handler);
			}

			Assert.IsNotNull(listPayload);
			Assert.AreEqual(1, listPayload.Stories.Count);
			Assert.AreEqual(2, listPayload.Stories[0].Id);
		}

		private static System.Collections.IEnumerator AwaitTask(Task task)
		{
			while (!task.IsCompleted)
			{
				yield return null;
			}
		}
	}
}
