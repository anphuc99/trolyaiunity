using NUnit.Framework;
using Features.GamePlay.Controller;
using Features.GamePlay.Model;

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
	}
}
