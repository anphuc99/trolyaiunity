using Core.Infrastructure.Views;
using Features.UserPlayer2.Events;
using Features.UserPlayer2.Infrastructure.Attributes;
using Features.UserPlayer2.Requests;
using UnityEngine;

namespace Features.UserPlayer2.View
{
	/// <summary>
	/// View for UserPlayer2.
	/// </summary>
	public sealed class UserPlayer2View : BaseView
	{
		[SerializeField]
		private string _message = "Hello";

		/// <summary>
		/// Example method to send a request.
		/// </summary>
		public void SendEcho()
		{
			SendRequest(UserPlayer2Requests.Echo, _message);
		}

		/// <summary>
		/// Example event handler (auto-bound).
		/// </summary>
		/// <param name="payload">Payload from controller.</param>
		[OnEvent(UserPlayer2Events.Echoed)]
		private void OnEchoed(object payload)
		{
			Debug.Log("[UserPlayer2View] Echoed: " + payload, this);
		}
	}
}
