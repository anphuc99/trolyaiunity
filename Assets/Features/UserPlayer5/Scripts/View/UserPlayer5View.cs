using Core.Infrastructure.Views;
using Features.UserPlayer5.Events;
using Features.UserPlayer5.Infrastructure.Attributes;
using Features.UserPlayer5.Requests;
using UnityEngine;

namespace Features.UserPlayer5.View
{
	/// <summary>
	/// View for UserPlayer5.
	/// </summary>
	public sealed class UserPlayer5View : BaseView
	{
		[SerializeField]
		private string _message = "Hello";

		/// <summary>
		/// Example method to send a request.
		/// </summary>
		public void SendEcho()
		{
			SendRequest(UserPlayer5Requests.Echo, _message);
		}

		/// <summary>
		/// Example event handler (auto-bound).
		/// </summary>
		/// <param name="payload">Payload from controller.</param>
		[OnEvent(UserPlayer5Events.Echoed)]
		private void OnEchoed(object payload)
		{
			Debug.Log("[UserPlayer5View] Echoed: " + payload, this);
		}
	}
}
