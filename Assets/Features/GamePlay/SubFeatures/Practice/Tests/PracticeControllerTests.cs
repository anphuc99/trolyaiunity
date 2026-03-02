using Core.Infrastructure.Events;
using Core.Infrastructure.Network;
using Features.GamePlay.SubFeatures.Practice.Controller;
using Features.GamePlay.SubFeatures.Practice.Events;
using Features.GamePlay.SubFeatures.Practice.Model;
using NUnit.Framework;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine.TestTools;

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
			FakeServer.ResetToDefaults();
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
			Assert.AreEqual("Text is required for playback.", errorPayload.Message);
		}

		[Test]
		public void PlayAudio_ShouldPublishError_WhenTextIsEmpty()
		{
			PracticeErrorPayload errorPayload = null;
			EventBus.Subscribe(PracticeEvents.RequestFailed, payload =>
			{
				errorPayload = payload as PracticeErrorPayload;
			});

			PracticeController.HandlePlayAudio(new PracticeAudioRequestPayload
			{
				Text = "  "
			});

			Assert.IsNotNull(errorPayload);
			Assert.AreEqual("Text is required for playback.", errorPayload.Message);
		}

		[UnityTest]
		public IEnumerator PlayAudio_ShouldPublishAudioUrlResolved_WhenPayloadIsValid()
		{
			PracticeAudioUrlPayload audioPayload = null;
			EventBus.Subscribe(PracticeEvents.AudioUrlResolved, payload =>
			{
				audioPayload = payload as PracticeAudioUrlPayload;
			});

			FakeServer.Register("GET", NetworkEndpoints.TextToSpeech, _ =>
				"{\"url\":\"/audio/generated.mp3\"}"
			);

			PracticeController.HandlePlayAudio(new PracticeAudioRequestPayload
			{
				Text = "Xin chao",
				Tone = "neutral",
				CharacterName = "Mimi"
			});
			yield return AwaitTask(Task.Delay(100));

			Assert.IsNotNull(audioPayload);
			Assert.IsTrue(audioPayload.Url.EndsWith("/audio/generated.mp3"));
		}

		[UnityTest]
		public IEnumerator PlayAudio_ShouldUseDefaultToneAndCharacter_WhenMissing()
		{
			PracticeAudioUrlPayload audioPayload = null;
			string requestedPath = null;
			EventBus.Subscribe(PracticeEvents.AudioUrlResolved, payload =>
			{
				audioPayload = payload as PracticeAudioUrlPayload;
			});

			FakeServer.Register("GET", NetworkEndpoints.TextToSpeech, path =>
			{
				requestedPath = path;
				return "{\"url\":\"/audio/default.mp3\"}";
			});

			PracticeController.HandlePlayAudio(new PracticeAudioRequestPayload
			{
				Text = "  hello  "
			});
			yield return AwaitTask(Task.Delay(100));

			Assert.IsNotNull(audioPayload);
			Assert.IsTrue(audioPayload.Url.EndsWith("/audio/default.mp3"));
			Assert.IsNotNull(requestedPath);
			StringAssert.Contains("tone=neutral%2C%20medium%20pitch", requestedPath);
			StringAssert.Contains("characterName=Mimi", requestedPath);
		}

		private static IEnumerator AwaitTask(Task task)
		{
			while (!task.IsCompleted)
			{
				yield return null;
			}
		}
	}
}
