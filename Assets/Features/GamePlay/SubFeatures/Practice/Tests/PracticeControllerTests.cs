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

		[Test]
		public void PlayAudio_ShouldPublishError_WhenPayloadIsNull()
		{
			PracticeErrorPayload errorPayload = null;
			EventBus.Subscribe(PracticeEvents.RequestFailed, payload =>
			{
				errorPayload = payload as PracticeErrorPayload;
			});

			PracticeController.HandlePlayAudio(null);

			Assert.IsNotNull(errorPayload);
			Assert.AreEqual("Audio id is required for playback.", errorPayload.Message);
		}

		[Test]
		public void PlayAudio_ShouldPublishError_WhenAudioIdIsEmpty()
		{
			PracticeErrorPayload errorPayload = null;
			EventBus.Subscribe(PracticeEvents.RequestFailed, payload =>
			{
				errorPayload = payload as PracticeErrorPayload;
			});

			PracticeController.HandlePlayAudio(new PracticeAudioRequestPayload
			{
				AudioId = "  "
			});

			Assert.IsNotNull(errorPayload);
			Assert.AreEqual("Audio id is required for playback.", errorPayload.Message);
		}

		[Test]
		public void PlayAudio_ShouldPublishAudioUrlResolved_WhenAudioIdIsValid()
		{
			PracticeAudioUrlPayload audioPayload = null;
			EventBus.Subscribe(PracticeEvents.AudioUrlResolved, payload =>
			{
				audioPayload = payload as PracticeAudioUrlPayload;
			});

			PracticeController.HandlePlayAudio(new PracticeAudioRequestPayload
			{
				AudioId = "abc123"
			});

			Assert.IsNotNull(audioPayload);
			Assert.IsTrue(audioPayload.Url.EndsWith("/audio/abc123.mp3"));
		}

		[Test]
		public void PlayAudio_ShouldTrimAudioId()
		{
			PracticeAudioUrlPayload audioPayload = null;
			EventBus.Subscribe(PracticeEvents.AudioUrlResolved, payload =>
			{
				audioPayload = payload as PracticeAudioUrlPayload;
			});

			PracticeController.HandlePlayAudio(new PracticeAudioRequestPayload
			{
				AudioId = "  xyz789  "
			});

			Assert.IsNotNull(audioPayload);
			Assert.IsTrue(audioPayload.Url.EndsWith("/audio/xyz789.mp3"));
		}
	}
}
