using NUnit.Framework;
using Features.GamePlay.Controller;
using Features.GamePlay.Model;
using Features.GamePlay.Events;
using Core.Infrastructure.Events;

namespace Features.GamePlay.Tests
{
	/// <summary>
	/// Tests for GamePlay controller subcontroller switching.
	/// </summary>
	public sealed class GamePlayControllerTests
	{
		[SetUp]
		public void SetUp()
		{
			EventBus.ClearAll();
			GamePlayController.HandleCloseCurrentSubController();
		}

		[TearDown]
		public void TearDown()
		{
			EventBus.ClearAll();
			GamePlayController.HandleCloseCurrentSubController();
		}

		[Test]
		public void OpenSubController_ShouldInstallTarget()
		{
			var opened = GamePlayController.HandleOpenSubController(GamePlaySubControllerType.Home);

			Assert.IsTrue(opened);
			Assert.AreEqual(GamePlaySubControllerType.Home.ToString(), GamePlayController.HandleGetCurrentSubController());
		}

		[Test]
		public void OpenSubController_ShouldCloseCurrentBeforeOpeningNewOne()
		{
			GamePlayController.HandleOpenSubController(GamePlaySubControllerType.Home);
			var opened = GamePlayController.HandleOpenSubController(GamePlaySubControllerType.Chat);

			Assert.IsTrue(opened);
			Assert.AreEqual(GamePlaySubControllerType.Chat.ToString(), GamePlayController.HandleGetCurrentSubController());
		}

		[Test]
		public void OpenSubController_ShouldOpenMyLog()
		{
			var opened = GamePlayController.HandleOpenSubController(GamePlaySubControllerType.MyLog);

			Assert.IsTrue(opened);
			Assert.AreEqual(GamePlaySubControllerType.MyLog.ToString(), GamePlayController.HandleGetCurrentSubController());
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
	}
}
