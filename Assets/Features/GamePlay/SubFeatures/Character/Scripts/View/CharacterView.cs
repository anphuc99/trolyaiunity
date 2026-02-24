using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.Character.Events;
using Features.GamePlay.SubFeatures.Character.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Character.Requests;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.Character.View
{
	/// <summary>
	/// View for Character.
	/// </summary>
	public sealed class CharacterView : BaseView
	{
		[SerializeField]
		private string _message = "Hello";

		/// <summary>
		/// Example method to send a request.
		/// </summary>
		public void SendEcho()
		{
			SendRequest(CharacterRequests.Echo, _message);
		}

		/// <summary>
		/// Example event handler (auto-bound).
		/// </summary>
		/// <param name="payload">Payload from controller.</param>
		[OnEvent(CharacterEvents.Echoed)]
		private void OnEchoed(object payload)
		{
			Debug.Log("[CharacterView] Echoed: " + payload, this);
		}
	}
}
