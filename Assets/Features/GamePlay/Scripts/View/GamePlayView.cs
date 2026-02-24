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
		public void OpenHome() 
		{
			SendRequest(GamePlayRequests.OpenSubController, GamePlaySubControllerType.Home);	
		}
		
		public void OpenChat()
		{
			SendRequest(GamePlayRequests.OpenSubController, GamePlaySubControllerType.Chat);
		}
	}
}
