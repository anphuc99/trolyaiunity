using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Infrastructure.Authentication;
using Core.Infrastructure.Events;
using Core.Infrastructure.Network;
using Features.GamePlay.SubFeatures.Setting.Controller;
using Features.GamePlay.SubFeatures.Setting.Events;
using Features.GamePlay.SubFeatures.Setting.Model;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Features.GamePlay.SubFeatures.Setting.Tests
{
	/// <summary>
	/// Tests for Setting controller request flow.
	/// </summary>
	public sealed class SettingControllerTests
	{
		[SetUp]
		public void SetUp()
		{
			EventBus.ClearAll();
			SettingState.Reset();
			FakeServer.ResetToDefaults();
			AuthTokenModel.AccessToken = null;
			AuthTokenModel.RefreshToken = null;
		}

		[TearDown]
		public void TearDown()
		{
			EventBus.ClearAll();
			SettingState.Reset();
			FakeServer.ResetToDefaults();
			AuthTokenModel.AccessToken = null;
			AuthTokenModel.RefreshToken = null;
		}

		[Test]
		public void HandleLogout_ClearsAuthTokens()
		{
			AuthTokenModel.AccessToken = "access-token";
			AuthTokenModel.RefreshToken = "refresh-token";

			try
			{
				SettingController.HandleLogout(null);
			}
			catch
			{
				// Scene loading may throw in test runner context; token assertions still apply.
			}

			Assert.IsNull(AuthTokenModel.AccessToken);
			Assert.IsNull(AuthTokenModel.RefreshToken);
		}

		[UnityTest]
		public System.Collections.IEnumerator HandleLoadProfile_WithValidResponses_PublishesProfileLoaded()
		{
			SettingProfilePayload loadedPayload = null;
			void Handler(object payload) => loadedPayload = payload as SettingProfilePayload;

			SettingController.SetParentSignals(new SettingParentSignals
			{
				GetCharacterNames = () => new List<string> { "Mimi" },
				GetCharacterVoiceName = _ => "alloy",
				GetCharacterPitch = _ => 1.1f
			});

			FakeServer.Register("GET", NetworkEndpoints.UserMe,
				_ => "{\"user\":{\"id\":1,\"username\":\"mimi\",\"name\":\"Test\",\"age\":20,\"description\":\"Desc\",\"levelId\":2,\"currentStoryId\":3,\"voiceName\":\"alloy\",\"pitch\":1.1}}"
			);

			EventBus.Subscribe(SettingEvents.ProfileLoaded, Handler);
			try
			{
				SettingController.HandleLoadProfile(null);
				yield return AwaitTask(Task.Delay(100));
			}
			finally
			{
				EventBus.Unsubscribe(SettingEvents.ProfileLoaded, Handler);
			}

			Assert.IsNotNull(loadedPayload);
			Assert.AreEqual(0, loadedPayload.Levels.Count);
			Assert.AreEqual(1, loadedPayload.Voices.Count);
			Assert.AreEqual("Mimi (alloy)", loadedPayload.Voices[0].Label);
		}

		[Test]
		public void HandleSaveProfile_InvalidPayload_PublishesSaveFailed()
		{
			SettingErrorPayload errorPayload = null;
			void Handler(object payload) => errorPayload = payload as SettingErrorPayload;

			EventBus.Subscribe(SettingEvents.ProfileSaveFailed, Handler);
			try
			{
				SettingController.HandleSaveProfile(new SettingProfileSaveRequestPayload { Name = "" });
			}
			finally
			{
				EventBus.Unsubscribe(SettingEvents.ProfileSaveFailed, Handler);
			}

			Assert.IsNotNull(errorPayload);
		}

		[UnityTest]
		public System.Collections.IEnumerator HandleSaveProfile_ValidPayload_PublishesSaveSucceeded()
		{
			SettingProfilePayload savedPayload = null;
			void Handler(object payload) => savedPayload = payload as SettingProfilePayload;

			FakeServer.Register("PUT", NetworkEndpoints.UserProfile,
				_ => "{\"user\":{\"id\":1,\"username\":\"mimi\",\"name\":\"Updated\",\"age\":25,\"description\":\"Desc\",\"levelId\":null,\"currentStoryId\":null,\"voiceName\":\"alloy\",\"pitch\":1.0}}"
			);

			SettingState.CachedLevels = new List<SettingLevelOptionPayload>();
			SettingState.CachedStories = new List<SettingStoryOptionPayload>();
			SettingState.CachedVoices = new List<SettingVoiceOptionPayload>();

			EventBus.Subscribe(SettingEvents.ProfileSaveSucceeded, Handler);
			try
			{
				SettingController.HandleSaveProfile(new SettingProfileSaveRequestPayload
				{
					Name = "Updated",
					Age = 25
				});
				yield return AwaitTask(Task.Delay(100));
			}
			finally
			{
				EventBus.Unsubscribe(SettingEvents.ProfileSaveSucceeded, Handler);
			}

			Assert.IsNotNull(savedPayload);
			Assert.AreEqual("Updated", savedPayload.Name);
			Assert.AreEqual(25, savedPayload.Age);
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
