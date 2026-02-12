using Core.Infrastructure.Views;
using Features.CreateCharater.Events;
using Features.CreateCharater.Infrastructure.Attributes;
using Features.CreateCharater.Requests;
using UnityEngine;

namespace Features.CreateCharater.View
{
	/// <summary>
	/// View for CreateCharater.
	/// </summary>
	public sealed class CreateCharaterView : BaseView
	{
		[SerializeField]
		private string _message = "Hello";

		/// <summary>
		/// Example method to send a request.
		/// </summary>
		public void SendEcho()
		{
			SendRequest(CreateCharaterRequests.Echo, _message);
		}

		/// <summary>
		/// Example event handler (auto-bound).
		/// </summary>
		/// <param name="payload">Payload from controller.</param>
		[OnEvent(CreateCharaterEvents.Echoed)]
		private void OnEchoed(object payload)
		{
			Debug.Log("[CreateCharaterView] Echoed: " + payload, this);
		}
	}
}
