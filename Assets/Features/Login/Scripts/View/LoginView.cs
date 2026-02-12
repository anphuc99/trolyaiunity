using Core.Infrastructure.Views;
using Features.Login.Events;
using Features.Login.Infrastructure.Attributes;
using Features.Login.Requests;
using UnityEngine;

namespace Features.Login.View
{
	/// <summary>
	/// View for Login.
	/// </summary>
	public sealed class LoginView : BaseView
	{
		[SerializeField]
		private string _message = "Hello";

		/// <summary>
		/// Example method to send a request.
		/// </summary>
		public void SendEcho()
		{
			SendRequest(LoginRequests.Echo, _message);
		}

		/// <summary>
		/// Example event handler (auto-bound).
		/// </summary>
		/// <param name="payload">Payload from controller.</param>
		[OnEvent(LoginEvents.Echoed)]
		private void OnEchoed(object payload)
		{
			Debug.Log("[LoginView] Echoed: " + payload, this);
		}
	}
}
