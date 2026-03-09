using NUnit.Framework;
using Features.GamePlay.Controller;
using Features.GamePlay.Model;
using Features.GamePlay.Events;
using Core.Infrastructure.Attributes;
using Core.Infrastructure.Events;
using Core.Infrastructure.State;

namespace Features.GamePlay.Tests
{
	/// <summary>
	/// Tests for GamePlay controller subcontroller switching.
	/// </summary>
	public sealed class GamePlayControllerTests
	{
		/// <summary>
		/// Proxy controller scoped to Global so tests can mutate GlobalVariables.
		/// </summary>
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
			GamePlayController.HandleCloseCurrentSubController();
			GlobalVariablesMutationProxyController.Remove(GlobalModes.ChatApiModeKey);
		}

		[TearDown]
		public void TearDown()
		{
			EventBus.ClearAll();
			GamePlayController.HandleCloseCurrentSubController();
			GlobalVariablesMutationProxyController.Remove(GlobalModes.ChatApiModeKey);
		}

		[Test]
		public void OpenSubController_ShouldInstallTarget()
		{
			var opened = GamePlayController.HandleOpenSubController(GamePlaySubControllerType.Character);

			Assert.IsTrue(opened);
			Assert.AreEqual(GamePlaySubControllerType.Character.ToString(), GamePlayController.HandleGetCurrentSubController());
		}

		[Test]
		public void OpenSubController_ShouldCloseCurrentBeforeOpeningNewOne()
		{
			GamePlayController.HandleOpenSubController(GamePlaySubControllerType.Character);
			var opened = GamePlayController.HandleOpenSubController(GamePlaySubControllerType.Chat);

			Assert.IsTrue(opened);
			Assert.AreEqual(GamePlaySubControllerType.Chat.ToString(), GamePlayController.HandleGetCurrentSubController());
		}

		[Test]
		public void CloseCurrentSubController_ShouldUninstallCurrent()
		{
			GamePlayController.HandleOpenSubController(GamePlaySubControllerType.Chat);
			GamePlayController.HandleCloseCurrentSubController();

			Assert.IsNull(GamePlayController.HandleGetCurrentSubController());
		}

		[Test]
		public void OpenSubController_ShouldFailForInvalidPayload()
		{
			var opened = GamePlayController.HandleOpenSubController("invalid-subcontroller");

			Assert.IsFalse(opened);
			Assert.IsNull(GamePlayController.HandleGetCurrentSubController());
		}

		[Test]
		public void AddMenu_ShouldPublishMenuItemAddRequestedEvent()
		{
			GamePlayMenuAddPayload receivedPayload = null;
			EventBus.Subscribe(GamePlayEvents.MenuItemAddRequested, payload => receivedPayload = payload as GamePlayMenuAddPayload);

			var clicked = false;
			var menuId = GamePlayController.AddMenu("Home", () => clicked = true);

			Assert.IsNotNull(menuId);
			Assert.IsNotNull(receivedPayload);
			Assert.AreEqual(menuId, receivedPayload.Id);
			Assert.AreEqual("Home", receivedPayload.Text);

			receivedPayload.OnClick?.Invoke();
			Assert.IsTrue(clicked);
		}

		[Test]
		public void RemoveMenu_ShouldPublishMenuItemRemoveRequestedEvent()
		{
			GamePlayMenuRemovePayload receivedPayload = null;
			EventBus.Subscribe(GamePlayEvents.MenuItemRemoveRequested, payload => receivedPayload = payload as GamePlayMenuRemovePayload);

			GamePlayController.RemoveMenu("menu-123");

			Assert.IsNotNull(receivedPayload);
			Assert.AreEqual("menu-123", receivedPayload.Id);
		}

		[Test]
		public void HandleOpenChat_ShouldResetChatApiModeToDefault()
		{
			GlobalVariablesMutationProxyController.Set(GlobalModes.ChatApiModeKey, GlobalModes.ModeMyLog);

			GamePlayController.HandleOpenChat(null);

			var mode = GlobalVariables.GetOrDefault(GlobalModes.ChatApiModeKey, GlobalModes.ModeDefault);
			Assert.AreEqual(GlobalModes.ModeDefault, mode);
			Assert.AreEqual(GamePlaySubControllerType.Chat.ToString(), GamePlayController.HandleGetCurrentSubController());
		}

		[Test]
		public void HandleOpenChat_ShouldOpenChatSubController()
		{
			GamePlayController.HandleOpenChat(null);

			Assert.AreEqual(GamePlaySubControllerType.Chat.ToString(), GamePlayController.HandleGetCurrentSubController());
		}
	}
}
