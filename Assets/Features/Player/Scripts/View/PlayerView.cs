using Core.Infrastructure.Views;
using Features.Player.Events;
using Features.Player.Infrastructure.Attributes;
using Features.Player.Requests;
using UnityEngine;

namespace Features.Player.View
{
	/// <summary>
	/// View for Player.
	/// </summary>
	public sealed class PlayerView : BaseView
	{
		[SerializeField]
		private string _message = "Hello";

		/// <summary>
		/// Example method to send a request.
		/// </summary>
		public void SendEcho()
		{
			SendRequest(PlayerRequests.Echo, _message);
		}

		/// <summary>
		/// Example event handler (auto-bound).
		/// </summary>
		/// <param name="payload">Payload from controller.</param>
		[OnEvent(PlayerEvents.Echoed)]
		private void OnEchoed(object payload)
		{
			Debug.Log("[PlayerView] Echoed: " + payload, this);
		}
	}
}
