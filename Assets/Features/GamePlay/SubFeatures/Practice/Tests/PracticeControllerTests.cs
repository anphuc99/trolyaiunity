using Core.Infrastructure.Events;
using Features.GamePlay.SubFeatures.Practice.Controller;
using Features.GamePlay.SubFeatures.Practice.Events;
using Features.GamePlay.SubFeatures.Practice.Model;
using NUnit.Framework;

namespace Features.GamePlay.SubFeatures.Practice.Tests
{
	/// <summary>
	/// Basic tests for Practice controller.
	/// </summary>
	public sealed class PracticeControllerTests
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
		public void LoadTab_ShouldPublishError_WhenPayloadMissing()
		{
			PracticeErrorPayload errorPayload = null;
			EventBus.Subscribe(PracticeEvents.RequestFailed, payload =>
			{
				errorPayload = payload as PracticeErrorPayload;
			});

			PracticeController.HandleLoadTab(null);

			Assert.IsNotNull(errorPayload);
			Assert.AreEqual("Missing practice tab request payload.", errorPayload.Message);
		}

		[Test]
		public void LoadContext_ShouldPublishError_WhenMessageIdMissing()
		{
			PracticeErrorPayload errorPayload = null;
			EventBus.Subscribe(PracticeEvents.RequestFailed, payload =>
			{
				errorPayload = payload as PracticeErrorPayload;
			});

			PracticeController.HandleLoadContext(new PracticeContextRequestPayload
			{
				MessageId = " "
			});

			Assert.IsNotNull(errorPayload);
			Assert.AreEqual("Message id is required to load context.", errorPayload.Message);
		}

		[Test]
		public void SubmitReview_ShouldPublishError_WhenRatingInvalid()
		{
			PracticeErrorPayload errorPayload = null;
			EventBus.Subscribe(PracticeEvents.RequestFailed, payload =>
			{
				errorPayload = payload as PracticeErrorPayload;
			});

			PracticeController.HandleSubmitReview(new PracticeReviewRequestPayload
			{
				Rating = 0,
				MessageId = "msg"
			});

			Assert.IsNotNull(errorPayload);
			Assert.AreEqual("Rating must be between 1 and 4.", errorPayload.Message);
		}
	}
}
