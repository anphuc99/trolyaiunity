using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Infrastructure.Attributes;
using Core.Infrastructure.Events;
using Core.Infrastructure.Network;
using Core.Infrastructure.State;
using Features.GamePlay.SubFeatures.Chat.Controller;
using Features.GamePlay.SubFeatures.Chat.Events;
using Features.GamePlay.SubFeatures.Chat.Model;
using UnityEngine.TestTools;

namespace Features.GamePlay.SubFeatures.Chat.Tests
{
	/// <summary>
	/// Tests for Chat controller parent-signal integrations.
	/// </summary>
	public sealed class ChatControllerTests
	{
		[ControllerScope(ControllerScopeKey.Global)]
		private static class GlobalVariablesMutationProxyController
		{
			public static void Set(string key, object value)
			{
				GlobalVariables.Set(key, value);
			}

			public static void Remove(string key)
			{
				GlobalVariables.Remove(key);
			}
		}

		[SetUp]
		public void SetUp()
		{
			EventBus.ClearAll();
			FakeServer.ResetToDefaults();
			GlobalVariablesMutationProxyController.Remove(GlobalModes.ChatApiModeKey);
			ChatState.ParentSignals = null;
			ChatState.AddCharacterMenuId = null;
			ChatState.ContextMenuId = null;
			ChatState.EndConversationMenuId = null;
		}

		[TearDown]
		public void TearDown()
		{
			EventBus.ClearAll();
			FakeServer.ResetToDefaults();
			GlobalVariablesMutationProxyController.Remove(GlobalModes.ChatApiModeKey);
			ChatState.ParentSignals = null;
			ChatState.AddCharacterMenuId = null;
			ChatState.ContextMenuId = null;
			ChatState.EndConversationMenuId = null;
		}

		[Test]
		public void Install_ShouldAddCharacterMenuFromParentSignals()
		{
			var requestedMenuTexts = new List<string>();
			Action requestedCharacterMenuAction = null;

			ChatController.SetParentSignals(new ChatParentSignals
			{
				AddMenu = (text, onClick) =>
				{
					requestedMenuTexts.Add(text);
					if (string.Equals(text, "Thêm nhân vật", StringComparison.Ordinal))
					{
						requestedCharacterMenuAction = onClick;
						return "menu-chat-add-character";
					}

					if (string.Equals(text, "Nhập bối cảnh", StringComparison.Ordinal))
					{
						return "menu-chat-context";
					}

					if (string.Equals(text, "Kết thúc hội thoại", StringComparison.Ordinal))
					{
						return "menu-chat-end";
					}

					return null;
				}
			});

			ChatController.Install();

			CollectionAssert.Contains(requestedMenuTexts, "Thêm nhân vật");
			CollectionAssert.Contains(requestedMenuTexts, "Nhập bối cảnh");
			CollectionAssert.Contains(requestedMenuTexts, "Kết thúc hội thoại");
			Assert.IsNotNull(requestedCharacterMenuAction);
			Assert.AreEqual("menu-chat-add-character", ChatState.AddCharacterMenuId);
			Assert.AreEqual("menu-chat-context", ChatState.ContextMenuId);
			Assert.AreEqual("menu-chat-end", ChatState.EndConversationMenuId);
		}

		[Test]
		public void Uninstall_ShouldRemoveCharacterMenuFromParentSignals()
		{
			var removedMenuIds = new List<string>();

			ChatController.SetParentSignals(new ChatParentSignals
			{
				AddMenu = (text, _) =>
				{
					if (string.Equals(text, "Nhập bối cảnh", StringComparison.Ordinal))
					{
						return "menu-chat-context";
					}

					if (string.Equals(text, "Kết thúc hội thoại", StringComparison.Ordinal))
					{
						return "menu-chat-end";
					}

					return "menu-chat-add-character";
				},
				RemoveMenu = menuId => removedMenuIds.Add(menuId),
			});

			ChatController.Install();
			ChatController.Uninstall();

			CollectionAssert.Contains(removedMenuIds, "menu-chat-add-character");
			CollectionAssert.Contains(removedMenuIds, "menu-chat-context");
			CollectionAssert.Contains(removedMenuIds, "menu-chat-end");
			Assert.IsNull(ChatState.AddCharacterMenuId);
			Assert.IsNull(ChatState.ContextMenuId);
			Assert.IsNull(ChatState.EndConversationMenuId);
		}

		[Test]
		public void Uninstall_ShouldClearChatApiMode()
		{
			ChatController.SetParentSignals(new ChatParentSignals
			{
				AddMenu = (_, __) => "test-menu-id",
				RemoveMenu = _ => { },
			});

			GlobalVariablesMutationProxyController.Set(GlobalModes.ChatApiModeKey, GlobalModes.ModeMyLog);
			ChatController.Install();
			ChatController.Uninstall();

			var mode = GlobalVariables.GetOrDefault(GlobalModes.ChatApiModeKey, GlobalModes.ModeDefault);
			Assert.AreEqual(GlobalModes.ModeDefault, mode);
		}

		[Test]
		public void ContextMenuClick_ShouldPublishContextInputRequestedEvent()
		{
			Action contextMenuAction = null;
			var isEventPublished = false;

			EventBus.Subscribe(ChatEvents.ContextInputRequested, _ =>
			{
				isEventPublished = true;
			});

			ChatController.SetParentSignals(new ChatParentSignals
			{
				AddMenu = (text, onClick) =>
				{
					if (!string.Equals(text, "Nhập bối cảnh", StringComparison.Ordinal))
					{
						return "menu-chat-add-character";
					}

					contextMenuAction = onClick;
					return "menu-chat-context";
				},
			});

			ChatController.Install();
			contextMenuAction?.Invoke();

			Assert.IsTrue(isEventPublished);
		}

		[Test]
		public void AddCharacterMenuClick_ShouldPublishCharactersLoadedPayload()
		{
			Action requestedMenuAction = null;
			List<ChatSelectableCharacterPayload> publishedPayload = null;

			EventBus.Subscribe(ChatEvents.CharactersLoaded, payload =>
			{
				publishedPayload = payload as List<ChatSelectableCharacterPayload>;
			});

			ChatController.SetParentSignals(new ChatParentSignals
			{
				AddMenu = (_, onClick) =>
				{
					requestedMenuAction = onClick;
					return "menu-chat-add-character";
				},
				GetCharacterNames = () => new List<string> { "Mimi", "Luna" },
			});

			ChatController.Install();
			requestedMenuAction?.Invoke();

			Assert.IsNotNull(publishedPayload);
			Assert.AreEqual(2, publishedPayload.Count);
			Assert.AreEqual("Mimi", publishedPayload[0].Name);
			Assert.AreEqual("Luna", publishedPayload[1].Name);
		}

		[Test]
		public void SetCharacterActive_ShouldPublishError_WhenCharacterNameMissing()
		{
			ChatErrorPayload errorPayload = null;
			EventBus.Subscribe(ChatEvents.RequestFailed, payload =>
			{
				errorPayload = payload as ChatErrorPayload;
			});

			ChatController.HandleSetCharacterActive(new ChatSetCharacterActiveRequestPayload
			{
				SessionId = "default",
				CharacterName = " ",
				IsActive = true,
			});

			Assert.IsNotNull(errorPayload);
			Assert.AreEqual("Character name is required when changing active state.", errorPayload.Message);
		}

		[Test]
		public void SaveContext_ShouldPublishError_WhenContextIsMissing()
		{
			ChatErrorPayload errorPayload = null;
			EventBus.Subscribe(ChatEvents.RequestFailed, payload =>
			{
				errorPayload = payload as ChatErrorPayload;
			});

			ChatController.HandleSaveContext(new ChatSaveContextRequestPayload
			{
				SessionId = "default",
				Context = " ",
			});

			Assert.IsNotNull(errorPayload);
			Assert.AreEqual("Context is required when saving developer context.", errorPayload.Message);
		}

		[UnityTest]
		public System.Collections.IEnumerator HandleLoadHistory_WhenMyLogMode_UsesMyLogHistoryEndpoint()
		{
			GlobalVariablesMutationProxyController.Set(GlobalModes.ChatApiModeKey, GlobalModes.ModeMyLog);

			ChatHistoryResponsePayload historyPayload = null;
			void Handler(object payload)
			{
				historyPayload = payload as ChatHistoryResponsePayload;
			}

			FakeServer.Register("GET", NetworkEndpoints.MyLogChatHistory,
				_ => "{\"messages\":[{\"id\":\"m1\",\"role\":\"assistant\",\"content\":\"hello\"}],\"sessionId\":\"s1\"}"
			);

			EventBus.Subscribe(ChatEvents.HistoryLoaded, Handler);
			try
			{
				ChatController.HandleLoadHistory(new ChatHistoryRequestPayload());
				yield return AwaitTask(Task.Delay(100));
			}
			finally
			{
				EventBus.Unsubscribe(ChatEvents.HistoryLoaded, Handler);
			}

			Assert.IsNotNull(historyPayload);
			Assert.IsNotNull(historyPayload.Messages);
			Assert.AreEqual(1, historyPayload.Messages.Count);
		}

		[Test]
		public void LookupVocabulary_ShouldPublishError_WhenWordIsMissing()
		{
			ChatErrorPayload errorPayload = null;
			EventBus.Subscribe(ChatEvents.RequestFailed, payload =>
			{
				errorPayload = payload as ChatErrorPayload;
			});

			ChatController.HandleLookupVocabulary(new ChatVocabLookupRequestPayload
			{
				Word = " ",
			});

			Assert.IsNotNull(errorPayload);
			Assert.AreEqual("Missing word for vocabulary lookup.", errorPayload.Message);
		}

		[Test]
		public void LookupVocabulary_ShouldPublishError_WhenPayloadIsNull()
		{
			ChatErrorPayload errorPayload = null;
			EventBus.Subscribe(ChatEvents.RequestFailed, payload =>
			{
				errorPayload = payload as ChatErrorPayload;
			});

			ChatController.HandleLookupVocabulary(null);

			Assert.IsNotNull(errorPayload);
			Assert.AreEqual("Missing word for vocabulary lookup.", errorPayload.Message);
		}

		[Test]
		public void ReviewVocabulary_ShouldPublishError_WhenVocabularyIdIsMissing()
		{
			ChatErrorPayload errorPayload = null;
			EventBus.Subscribe(ChatEvents.RequestFailed, payload =>
			{
				errorPayload = payload as ChatErrorPayload;
			});

			ChatController.HandleReviewVocabulary(new ChatVocabReviewRequestPayload
			{
				VocabularyId = " ",
				Rating = 1,
			});

			Assert.IsNotNull(errorPayload);
			Assert.AreEqual("Missing vocabulary id for review.", errorPayload.Message);
		}

		[Test]
		public void ReviewVocabulary_ShouldPublishError_WhenPayloadIsNull()
		{
			ChatErrorPayload errorPayload = null;
			EventBus.Subscribe(ChatEvents.RequestFailed, payload =>
			{
				errorPayload = payload as ChatErrorPayload;
			});

			ChatController.HandleReviewVocabulary(null);

			Assert.IsNotNull(errorPayload);
			Assert.AreEqual("Missing vocabulary id for review.", errorPayload.Message);
		}

		[UnityTest]
		public System.Collections.IEnumerator LookupVocabulary_ShouldPublishResult_WhenServerReturnsData()
		{
			ChatVocabLookupResultPayload resultPayload = null;
			void Handler(object payload)
			{
				resultPayload = payload as ChatVocabLookupResultPayload;
			}

			FakeServer.Register("GET", NetworkEndpoints.VocabularyLookup,
				_ => "{\"id\":\"v1\",\"korean\":\"爱\",\"vietnamese\":\"yêu\",\"pinyin\":\"ài\",\"isNew\":false}"
			);

			EventBus.Subscribe(ChatEvents.VocabLookupCompleted, Handler);
			try
			{
				ChatController.HandleLookupVocabulary(new ChatVocabLookupRequestPayload { Word = "爱" });
				yield return AwaitTask(Task.Delay(100));
			}
			finally
			{
				EventBus.Unsubscribe(ChatEvents.VocabLookupCompleted, Handler);
			}

			Assert.IsNotNull(resultPayload);
			Assert.AreEqual("v1", resultPayload.Id);
			Assert.AreEqual("爱", resultPayload.Word);
			Assert.AreEqual("yêu", resultPayload.Vietnamese);
			Assert.AreEqual("ài", resultPayload.Pinyin);
			Assert.IsFalse(resultPayload.IsNew);
		}

		[UnityTest]
		public System.Collections.IEnumerator LoadVocabularyLearnedCount_ShouldPublishCount_WhenServerReturnsData()
		{
			ChatVocabCountPayload countPayload = null;
			void Handler(object payload)
			{
				countPayload = payload as ChatVocabCountPayload;
			}

			FakeServer.Register("GET", NetworkEndpoints.VocabularyLearnedCount, _ => "{\"count\":7}");

			EventBus.Subscribe(ChatEvents.VocabularyLearnedCountLoaded, Handler);
			try
			{
				ChatController.HandleLoadVocabularyLearnedCount(null);
				yield return AwaitTask(Task.Delay(100));
			}
			finally
			{
				EventBus.Unsubscribe(ChatEvents.VocabularyLearnedCountLoaded, Handler);
			}

			Assert.IsNotNull(countPayload);
			Assert.AreEqual(7, countPayload.Count);
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
