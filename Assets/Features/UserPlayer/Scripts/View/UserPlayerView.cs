using Core.Infrastructure.Views;
using Features.UserPlayer.Events;
using Features.UserPlayer.Infrastructure.Attributes;
using Features.UserPlayer.Requests;
using UnityEngine;

namespace Features.UserPlayer.View
{
	/// <summary>
	/// View for UserPlayer.
	/// </summary>
	public sealed class UserPlayerView : BaseView
	{
		[SerializeField]
		private string _message = "Hello";

		/// <summary>
		/// Example method to send a request.
		/// </summary>
		public void SendEcho()
		{
			SendRequest(UserPlayerRequests.Echo, _message);
		}

		/// <summary>
		/// Example event handler (auto-bound).
		/// </summary>
		/// <param name="payload">Payload from controller.</param>
		[OnEvent(UserPlayerEvents.Echoed)]
		private void OnEchoed(object payload)
		{
			Debug.Log("[UserPlayerView] Echoed: " + payload, this);
		}
	}
}
