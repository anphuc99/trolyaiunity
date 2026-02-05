using Core.Infrastructure.Views;
using Features.UserPlayer6.Events;
using Features.UserPlayer6.Infrastructure.Attributes;
using Features.UserPlayer6.Requests;
using UnityEngine;

namespace Features.UserPlayer6.View
{
	/// <summary>
	/// View for UserPlayer6.
	/// </summary>
	public sealed class UserPlayer6View : BaseView
	{
		[SerializeField]
		private string _message = "Hello";

		/// <summary>
		/// Example method to send a request.
		/// </summary>
		public void SendEcho()
		{
			SendRequest(UserPlayer6Requests.Echo, _message);
		}

		/// <summary>
		/// Example event handler (auto-bound).
		/// </summary>
		/// <param name="payload">Payload from controller.</param>
		[OnEvent(UserPlayer6Events.Echoed)]
		private void OnEchoed(object payload)
		{
			Debug.Log("[UserPlayer6View] Echoed: " + payload, this);
		}
	}
}
