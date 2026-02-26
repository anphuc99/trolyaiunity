using NUnit.Framework;
using System;
using System.Collections.Generic;
using Core.Infrastructure.Events;
using Features.GamePlay.SubFeatures.Chat.Controller;
using Features.GamePlay.SubFeatures.Chat.Events;
using Features.GamePlay.SubFeatures.Chat.Model;

namespace Features.GamePlay.SubFeatures.Chat.Tests
{
	/// <summary>
	/// Tests for Chat controller parent-signal integrations.
	/// </summary>
	public sealed class ChatControllerTests
	{
		[SetUp]
		public void SetUp()
		{
			EventBus.ClearAll();
			ChatState.ParentSignals = null;
			ChatState.AddCharacterMenuId = null;
			ChatState.ContextMenuId = null;
		}

		[TearDown]
		public void TearDown()
		{
			EventBus.ClearAll();
			ChatState.ParentSignals = null;
			ChatState.AddCharacterMenuId = null;
			ChatState.ContextMenuId = null;
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

					return null;
				}
			});

			ChatController.Install();

			CollectionAssert.Contains(requestedMenuTexts, "Thêm nhân vật");
			CollectionAssert.Contains(requestedMenuTexts, "Nhập bối cảnh");
			Assert.IsNotNull(requestedCharacterMenuAction);
			Assert.AreEqual("menu-chat-add-character", ChatState.AddCharacterMenuId);
			Assert.AreEqual("menu-chat-context", ChatState.ContextMenuId);
		}

		[Test]
		public void Uninstall_ShouldRemoveCharacterMenuFromParentSignals()
		{
			var removedMenuIds = new List<string>();

			ChatController.SetParentSignals(new ChatParentSignals
			{
				AddMenu = (text, _) => string.Equals(text, "Nhập bối cảnh", StringComparison.Ordinal)
					? "menu-chat-context"
					: "menu-chat-add-character",
				RemoveMenu = menuId => removedMenuIds.Add(menuId),
			});

			ChatController.Install();
			ChatController.Uninstall();

			CollectionAssert.Contains(removedMenuIds, "menu-chat-add-character");
			CollectionAssert.Contains(removedMenuIds, "menu-chat-context");
			Assert.IsNull(ChatState.AddCharacterMenuId);
			Assert.IsNull(ChatState.ContextMenuId);
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
	}
}
