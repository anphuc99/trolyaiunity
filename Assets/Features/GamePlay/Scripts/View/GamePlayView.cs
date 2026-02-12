using Core.Infrastructure.Views;
using Features.GamePlay.Events;
using Features.GamePlay.Infrastructure.Attributes;
using Features.GamePlay.Requests;
using UnityEngine;

namespace Features.GamePlay.View
{
	/// <summary>
	/// View for GamePlay.
	/// </summary>
	public sealed class GamePlayView : BaseView
	{
		[SerializeField]
		private string _message = "Hello";

		/// <summary>
		/// Example method to send a request.
		/// </summary>
		public void SendEcho()
		{
			SendRequest(GamePlayRequests.Echo, _message);
		}

		/// <summary>
		/// Example event handler (auto-bound).
		/// </summary>
		/// <param name="payload">Payload from controller.</param>
		[OnEvent(GamePlayEvents.Echoed)]
		private void OnEchoed(object payload)
		{
			Debug.Log("[GamePlayView] Echoed: " + payload, this);
		}
	}
}
