using Core.Infrastructure.Attributes;
using Core.Infrastructure.Events;
using Core.Infrastructure.State;
using Features.CharacterInfo.Controller;
using Features.CharacterInfo.Events;
using NUnit.Framework;
using Share.Model;

namespace Features.CharacterInfo.Tests
{
	/// <summary>
	/// Basic tests for CharacterInfo controller.
	/// </summary>
	public sealed class CharacterInfoControllerTests
	{
		private const string SelectedCharacterGlobalKey = "global.character.selected.info";

		[ControllerScope(ControllerScopeKey.Global)]
		private static class GlobalVariablesMutationProxyController
		{
			public static void SetSelectedCharacter(SelectedCharacterInfo value)
			{
				GlobalVariables.Set(SelectedCharacterGlobalKey, value);
			}

			public static void ClearSelectedCharacter()
			{
				GlobalVariables.Remove(SelectedCharacterGlobalKey);
			}
		}

		[TearDown]
		public void TearDown()
		{
			GlobalVariablesMutationProxyController.ClearSelectedCharacter();
		}

		[Test]
		public void OnEnterScope_PublishesSelectedCharacter_WhenGlobalValueExists()
		{
			var expected = new SelectedCharacterInfo
			{
				Name = "Mimi"
			};
			GlobalVariablesMutationProxyController.SetSelectedCharacter(expected);

			SelectedCharacterInfo published = null;
			void Handler(object payload)
			{
				published = payload as SelectedCharacterInfo;
			}

			EventBus.Subscribe(CharacterInfoEvents.SelectedCharacterLoaded, Handler);
			try
			{
				CharacterInfoController.OnEnterScope();
			}
			finally
			{
				EventBus.Unsubscribe(CharacterInfoEvents.SelectedCharacterLoaded, Handler);
			}

			Assert.IsNotNull(published);
			Assert.AreEqual("Mimi", published.Name);
		}

		[Test]
		public void OnEnterScope_PublishesNull_WhenGlobalValueMissing()
		{
			SelectedCharacterInfo published = new SelectedCharacterInfo();
			void Handler(object payload)
			{
				published = payload as SelectedCharacterInfo;
			}

			EventBus.Subscribe(CharacterInfoEvents.SelectedCharacterLoaded, Handler);
			try
			{
				CharacterInfoController.OnEnterScope();
			}
			finally
			{
				EventBus.Unsubscribe(CharacterInfoEvents.SelectedCharacterLoaded, Handler);
			}

			Assert.IsNull(published);
		}
	}
}
