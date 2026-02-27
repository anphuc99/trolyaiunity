using Core.Infrastructure.Events;
using Features.GamePlay.SubFeatures.Task.Controller;
using Features.GamePlay.SubFeatures.Task.Events;
using NUnit.Framework;

namespace Features.GamePlay.SubFeatures.Task.Tests
{
	/// <summary>
	/// Basic tests for Task controller.
	/// </summary>
	public sealed class TaskControllerTests
	{
		[SetUp]
		public void SetUp()
		{
			EventBus.ClearAll();
		}

		[TearDown]
		public void TearDown()
		{
			EventBus.ClearAll();
		}

		[Test]
		public void HandleEcho_ShouldPublishEchoedEvent()
		{
			object echoedPayload = null;
			var payload = new { Value = "task" };

			EventBus.Subscribe(TaskEvents.Echoed, eventPayload =>
			{
				echoedPayload = eventPayload;
			});

			TaskController.HandleEcho(payload);

			Assert.IsNotNull(echoedPayload);
			Assert.AreEqual(payload, echoedPayload);
		}

		[Test]
		public void HandleLoadToday_ShouldNotThrow_WhenCalled()
		{
			Assert.DoesNotThrow(() => TaskController.HandleLoadToday(null));
		}
	}
}
