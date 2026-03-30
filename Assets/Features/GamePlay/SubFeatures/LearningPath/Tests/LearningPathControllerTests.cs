using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Infrastructure.Network;
using Features.GamePlay.SubFeatures.LearningPath.Controller;
using Features.GamePlay.SubFeatures.LearningPath.Events;
using Features.GamePlay.SubFeatures.LearningPath.Model;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Features.GamePlay.SubFeatures.LearningPath.Tests
{
	/// <summary>
	/// Tests for LearningPath controller request and event flow.
	/// </summary>
	public sealed class LearningPathControllerTests
	{
		[TearDown]
		public void TearDown()
		{
			FakeServer.ResetToDefaults();
			LearningPathState.CachedList = new LearningPathListResponsePayload();
			LearningPathState.EditingLearningPathId = null;
		}

		[UnityTest]
		public System.Collections.IEnumerator HandleLoadList_ValidResponse_PublishesListLoaded()
		{
			LearningPathListResponsePayload listPayload = null;
			void Handler(object payload)
			{
				listPayload = payload as LearningPathListResponsePayload;
			}

			FakeServer.Register("GET", NetworkEndpoints.LearningPaths,
				_ => "{\"learningPaths\":[{\"id\":1,\"level\":\"HSK1\",\"vocabulary\":\"hello,coffee,sugar\"}]}"
			);

			Core.Infrastructure.Events.EventBus.Subscribe(LearningPathEvents.ListLoaded, Handler);
			try
			{
				LearningPathController.HandleLoadList(null);
				yield return AwaitTask(Task.Delay(100));
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(LearningPathEvents.ListLoaded, Handler);
			}

			Assert.IsNotNull(listPayload);
			Assert.AreEqual(1, listPayload.LearningPaths.Count);
			Assert.AreEqual(1, listPayload.LearningPaths[0].Id);
			Assert.AreEqual("HSK1", listPayload.LearningPaths[0].Level);
			Assert.AreEqual("hello,coffee,sugar", listPayload.LearningPaths[0].Vocabulary);
		}

		[Test]
		public void HandleEdit_WithCachedList_PublishesEditLoaded()
		{
			LearningPathState.CachedList = new LearningPathListResponsePayload
			{
				LearningPaths = new List<LearningPathPayload>
				{
					new LearningPathPayload { Id = 5, Level = "HSK2", Vocabulary = "tree,bench,walk" }
				}
			};

			LearningPathEditPayload editPayload = null;
			void Handler(object payload)
			{
				editPayload = payload as LearningPathEditPayload;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(LearningPathEvents.EditLoaded, Handler);
			try
			{
				LearningPathController.HandleEdit(new LearningPathEditRequestPayload { LearningPathId = 5 });
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(LearningPathEvents.EditLoaded, Handler);
			}

			Assert.IsNotNull(editPayload);
			Assert.AreEqual(5, editPayload.LearningPathId);
			Assert.AreEqual("HSK2", editPayload.Level);
			Assert.AreEqual("tree,bench,walk", editPayload.Vocabulary);
		}

		[Test]
		public void HandleEdit_InvalidId_PublishesError()
		{
			LearningPathErrorPayload errorPayload = null;
			void Handler(object payload)
			{
				errorPayload = payload as LearningPathErrorPayload;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(LearningPathEvents.RequestFailed, Handler);
			try
			{
				LearningPathController.HandleEdit(new LearningPathEditRequestPayload { LearningPathId = -1 });
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(LearningPathEvents.RequestFailed, Handler);
			}

			Assert.IsNotNull(errorPayload);
		}

		[Test]
		public void HandleCreate_NullPayload_PublishesError()
		{
			LearningPathErrorPayload errorPayload = null;
			void Handler(object payload)
			{
				errorPayload = payload as LearningPathErrorPayload;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(LearningPathEvents.RequestFailed, Handler);
			try
			{
				LearningPathController.HandleCreate(null);
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(LearningPathEvents.RequestFailed, Handler);
			}

			Assert.IsNotNull(errorPayload);
			Assert.IsTrue(errorPayload.Message.Contains("Level"));
		}

		[Test]
		public void HandleCreate_EmptyVocabulary_PublishesError()
		{
			LearningPathErrorPayload errorPayload = null;
			void Handler(object payload)
			{
				errorPayload = payload as LearningPathErrorPayload;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(LearningPathEvents.RequestFailed, Handler);
			try
			{
				LearningPathController.HandleCreate(new LearningPathCreateRequestPayload
				{
					Level = "Some context",
					Vocabulary = ""
				});
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(LearningPathEvents.RequestFailed, Handler);
			}

			Assert.IsNotNull(errorPayload);
			Assert.IsTrue(errorPayload.Message.Contains("Vocabulary"));
		}

		[Test]
		public void HandleUpdate_InvalidId_PublishesError()
		{
			LearningPathErrorPayload errorPayload = null;
			void Handler(object payload)
			{
				errorPayload = payload as LearningPathErrorPayload;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(LearningPathEvents.RequestFailed, Handler);
			try
			{
				LearningPathController.HandleUpdate(new LearningPathUpdateRequestPayload
				{
					LearningPathId = 0,
					Level = "Level",
					Vocabulary = "word"
				});
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(LearningPathEvents.RequestFailed, Handler);
			}

			Assert.IsNotNull(errorPayload);
		}

		[UnityTest]
		public System.Collections.IEnumerator HandleCreate_ValidResponse_PublishesListLoaded()
		{
			LearningPathListResponsePayload listPayload = null;
			void Handler(object payload)
			{
				listPayload = payload as LearningPathListResponsePayload;
			}

			FakeServer.Register("POST", NetworkEndpoints.LearningPaths,
				_ => "{\"learningPath\":{\"id\":2,\"level\":\"HSK1\",\"vocabulary\":\"book,pen,desk\"}}"
			);
			FakeServer.Register("GET", NetworkEndpoints.LearningPaths,
				_ => "{\"learningPaths\":[{\"id\":2,\"level\":\"HSK1\",\"vocabulary\":\"book,pen,desk\"}]}"
			);

			Core.Infrastructure.Events.EventBus.Subscribe(LearningPathEvents.ListLoaded, Handler);
			try
			{
				LearningPathController.HandleCreate(new LearningPathCreateRequestPayload
				{
					Level = "HSK1",
					Vocabulary = "book,pen,desk"
				});
				yield return AwaitTask(Task.Delay(100));
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(LearningPathEvents.ListLoaded, Handler);
			}

			Assert.IsNotNull(listPayload);
			Assert.AreEqual(1, listPayload.LearningPaths.Count);
			Assert.AreEqual(2, listPayload.LearningPaths[0].Id);
		}

		[Test]
		public void HandleEdit_NotFoundInCache_PublishesError()
		{
			LearningPathState.CachedList = new LearningPathListResponsePayload
			{
				LearningPaths = new List<LearningPathPayload>()
			};

			LearningPathErrorPayload errorPayload = null;
			void Handler(object payload)
			{
				errorPayload = payload as LearningPathErrorPayload;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(LearningPathEvents.RequestFailed, Handler);
			try
			{
				LearningPathController.HandleEdit(new LearningPathEditRequestPayload { LearningPathId = 999 });
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(LearningPathEvents.RequestFailed, Handler);
			}

			Assert.IsNotNull(errorPayload);
			Assert.IsTrue(errorPayload.Message.Contains("not found"));
		}

		[Test]
		public void OnExitScope_ClearsState()
		{
			LearningPathState.EditingLearningPathId = 42;
			LearningPathState.CachedList = new LearningPathListResponsePayload
			{
				LearningPaths = new List<LearningPathPayload>
				{
					new LearningPathPayload { Id = 1 }
				}
			};

			LearningPathController.OnExitScope();

			Assert.IsNull(LearningPathState.EditingLearningPathId);
			Assert.IsNotNull(LearningPathState.CachedList);
			Assert.AreEqual(0, LearningPathState.CachedList.LearningPaths.Count);
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
