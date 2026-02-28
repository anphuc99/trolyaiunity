using Core.Infrastructure.Views;
using Features.GamePlay.Events;
using Features.GamePlay.Infrastructure.Attributes;
using Features.GamePlay.Model;
using Features.GamePlay.Requests;
using UnityEngine;

namespace Features.GamePlay.View
{
	/// <summary>
	/// View for GamePlay.
	/// </summary>
	public sealed class GamePlayView : BaseView
	{
		public GamePlayMenuButton MenuButton;

		[OnEvent(GamePlayEvents.MenuItemAddRequested)]
		private void HandleMenuItemAddRequested(object payload)
		{
			if (MenuButton == null || payload is not GamePlayMenuAddPayload addPayload)
			{
				return;
			}

			MenuButton.AddMenuItem(addPayload.Id, addPayload.Text, addPayload.OnClick);
		}

		[OnEvent(GamePlayEvents.MenuItemRemoveRequested)]
		private void HandleMenuItemRemoveRequested(object payload)
		{
			if (MenuButton == null || payload is not GamePlayMenuRemovePayload removePayload)
			{
				return;
			}

			MenuButton.RemoveMenuItem(removePayload.Id);
		}

		public void OpenHome() 
		{
			SendRequest(GamePlayRequests.OpenSubController, GamePlaySubControllerType.Home);	
		}
		
		public void OpenChat()
		{
			SendRequest(GamePlayRequests.OpenSubController, GamePlaySubControllerType.Chat);
		}

		public void OpenSetting()
		{
			SendRequest(GamePlayRequests.OpenSubController, GamePlaySubControllerType.Setting);
		}
	}
}
