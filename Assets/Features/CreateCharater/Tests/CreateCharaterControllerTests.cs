using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Core.Infrastructure.Events;
using Core.Infrastructure.Network;
using Cysharp.Threading.Tasks;
using Features.CreateCharater.Controller;
using Features.CreateCharater.Events;
using Features.CreateCharater.Model;
using NUnit.Framework;

namespace Features.CreateCharater.Tests
{
	/// <summary>
	/// Tests for CreateCharater controller.
	/// </summary>
	public sealed class CreateCharaterControllerTests
	{
		[SetUp]
		public void SetUp()
		{
			EventBus.ClearAll();
			FakeServer.ResetToDefaults();
			SetHttpClientSettings(new NetworkSettings
			{
				UseFakeUrl = true,
				BaseUrl = "http://localhost:5000"
			});
		}

		[TearDown]
		public void TearDown()
		{
			EventBus.ClearAll();
			FakeServer.ResetToDefaults();
			SetHttpClientSettings(null);
		}

		[Test]
		public async UniTask FetchPersonalities_PublishesLoadedEvent()
		{
			List<PersonalityData> loadedData = null;
			EventBus.Subscribe(CreateCharaterEvents.PersonalitiesLoaded, payload => loadedData = payload as List<PersonalityData>);

			CreateCharaterController.FetchPersonalities();

			// Wait for async task to complete in fake server
			await UniTask.Delay(100);

			Assert.IsNotNull(loadedData);
			Assert.GreaterOrEqual(loadedData.Count, 1);
			Assert.AreEqual("Dũng cảm", loadedData[0].name);
		}

		[Test]
		public async UniTask SubmitCharacter_PublishesSuccessEvent()
		{
			bool success = false;
			EventBus.Subscribe(CreateCharaterEvents.CharacterCreationSucceeded, _ => success = true);

			var payload = new CreateCharacterPayload
			{
				name = "Test Hero",
				age = 25,
				gender = "Male",
				personality = new List<string> { "Dũng cảm" },
				description = "A brave tester."
			};

			CreateCharaterController.SubmitCharacter(payload);

			await UniTask.Delay(100);

			Assert.IsTrue(success);
		}

		private static void SetHttpClientSettings(NetworkSettings settings)
		{
			var field = typeof(HttpClient).GetField("_settings", BindingFlags.NonPublic | BindingFlags.Static);
			field?.SetValue(null, settings);
		}
	}
}
