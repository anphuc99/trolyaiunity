using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.Chat.Events;
using Features.GamePlay.SubFeatures.Chat.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Chat.Requests;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.Chat.View
{
	/// <summary>
	/// View for Chat.
	/// </summary>
	public sealed class ChatView : BaseView
	{
		[SerializeField]
		private string _message = "Hello";

		/// <summary>
		/// Example method to send a request.
		/// </summary>
		public void SendEcho()
		{
			SendRequest(ChatRequests.Echo, _message);
		}

		/// <summary>
		/// Example event handler (auto-bound).
		/// </summary>
		/// <param name="payload">Payload from controller.</param>
		[OnEvent(ChatEvents.Echoed)]
		private void OnEchoed(object payload)
		{
			Debug.Log("[ChatView] Echoed: " + payload, this);
		}
	}
}
