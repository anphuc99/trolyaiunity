using Core.Infrastructure.Views;
using Features.CharacterInfo.Events;
using Features.CharacterInfo.Infrastructure.Attributes;
using Features.CharacterInfo.Requests;
using UnityEngine;

namespace Features.CharacterInfo.View
{
	/// <summary>
	/// View for CharacterInfo.
	/// </summary>
	public sealed class CharacterInfoView : BaseView
	{
		[SerializeField]
		private string _message = "Hello";

		/// <summary>
		/// Example method to send a request.
		/// </summary>
		public void SendEcho()
		{
			SendRequest(CharacterInfoRequests.Echo, _message);
		}

		/// <summary>
		/// Example event handler (auto-bound).
		/// </summary>
		/// <param name="payload">Payload from controller.</param>
		[OnEvent(CharacterInfoEvents.Echoed)]
		private void OnEchoed(object payload)
		{
			Debug.Log("[CharacterInfoView] Echoed: " + payload, this);
		}
	}
}
