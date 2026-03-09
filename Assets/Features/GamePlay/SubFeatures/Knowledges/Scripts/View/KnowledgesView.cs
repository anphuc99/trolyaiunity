using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.Knowledges.Events;
using Features.GamePlay.SubFeatures.Knowledges.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Knowledges.Requests;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.Knowledges.View
{
	/// <summary>
	/// View for Knowledges.
	/// </summary>
	public sealed class KnowledgesView : BaseView
	{
		[SerializeField]
		private string _message = "Hello";

		/// <summary>
		/// Example method to send a request.
		/// </summary>
		public void SendEcho()
		{
			SendRequest(KnowledgesRequests.Echo, _message);
		}

		/// <summary>
		/// Example event handler (auto-bound).
		/// </summary>
		/// <param name="payload">Payload from controller.</param>
		[OnEvent(KnowledgesEvents.Echoed)]
		private void OnEchoed(object payload)
		{
			Debug.Log("[KnowledgesView] Echoed: " + payload, this);
		}
	}
}
