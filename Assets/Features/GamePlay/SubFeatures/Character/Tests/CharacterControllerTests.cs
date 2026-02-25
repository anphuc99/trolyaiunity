using System.Collections.Generic;
using Features.GamePlay.SubFeatures.Character.Controller;
using Features.GamePlay.SubFeatures.Character.Events;
using Features.GamePlay.SubFeatures.Character.Model;
using NUnit.Framework;

namespace Features.GamePlay.SubFeatures.Character.Tests
{
	/// <summary>
	/// Tests for Character controller request and event flow.
	/// </summary>
	public sealed class CharacterControllerTests
	{
		[TearDown]
		public void TearDown()
		{
			CharacterController.SetParentSignals(null);
			CharacterState.CachedCharacters = new List<CharacterListItemPayload>();
		}

		[Test]
		public void Install_LoadsCharactersFromParentSignals()
		{
			List<CharacterListItemPayload> publishedPayload = null;
			void Handler(object payload)
			{
				publishedPayload = payload as List<CharacterListItemPayload>;
			}

			CharacterController.SetParentSignals(new CharacterParentSignals
			{
				GetCharacterNames = () => new List<string> { "Mimi", "Lan" }
			});

			Core.Infrastructure.Events.EventBus.Subscribe(CharacterEvents.CharactersLoaded, Handler);
			try
			{
				CharacterController.Install();
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(CharacterEvents.CharactersLoaded, Handler);
			}

			Assert.IsNotNull(publishedPayload);
			Assert.AreEqual(2, publishedPayload.Count);
			Assert.AreEqual("Mimi", publishedPayload[0].Name);
			Assert.AreEqual("Lan", publishedPayload[1].Name);
		}

		[Test]
		public void Uninstall_ClearsCachedCharacters()
		{
			CharacterState.CachedCharacters = new List<CharacterListItemPayload>
			{
				new CharacterListItemPayload { Name = "Mimi" }
			};

			CharacterController.Uninstall();

			Assert.IsNotNull(CharacterState.CachedCharacters);
			Assert.AreEqual(0, CharacterState.CachedCharacters.Count);
		}
	}
}
