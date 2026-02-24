using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.Journal.Events;
using Features.GamePlay.SubFeatures.Journal.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Journal.Requests;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.Journal.View
{
	/// <summary>
	/// View for Journal.
	/// </summary>
	public sealed class JournalView : BaseView
	{
		[SerializeField]
		private string _message = "Hello";

		/// <summary>
		/// Example method to send a request.
		/// </summary>
		public void SendEcho()
		{
			SendRequest(JournalRequests.Echo, _message);
		}

		/// <summary>
		/// Example event handler (auto-bound).
		/// </summary>
		/// <param name="payload">Payload from controller.</param>
		[OnEvent(JournalEvents.Echoed)]
		private void OnEchoed(object payload)
		{
			Debug.Log("[JournalView] Echoed: " + payload, this);
		}
	}
}
