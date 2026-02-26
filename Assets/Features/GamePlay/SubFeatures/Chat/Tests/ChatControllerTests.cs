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
		}

		[TearDown]
		public void TearDown()
		{
			EventBus.ClearAll();
			ChatState.ParentSignals = null;
			ChatState.AddCharacterMenuId = null;
		}

		[Test]
		public void Install_ShouldAddCharacterMenuFromParentSignals()
		{
			string requestedMenuText = null;
			Action requestedMenuAction = null;

			ChatController.SetParentSignals(new ChatParentSignals
			{
				AddMenu = (text, onClick) =>
				{
					requestedMenuText = text;
					requestedMenuAction = onClick;
					return "menu-chat-add-character";
				}
			});

			ChatController.Install();

			Assert.AreEqual("Thêm nhân vật", requestedMenuText);
			Assert.IsNotNull(requestedMenuAction);
			Assert.AreEqual("menu-chat-add-character", ChatState.AddCharacterMenuId);
		}

		[Test]
		public void Uninstall_ShouldRemoveCharacterMenuFromParentSignals()
		{
			string removedMenuId = null;

			ChatController.SetParentSignals(new ChatParentSignals
			{
				AddMenu = (_, _) => "menu-chat-add-character",
				RemoveMenu = menuId => removedMenuId = menuId,
			});

			ChatController.Install();
			ChatController.Uninstall();

			Assert.AreEqual("menu-chat-add-character", removedMenuId);
			Assert.IsNull(ChatState.AddCharacterMenuId);
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
	}
}
