using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.Practice.Events;
using Features.GamePlay.SubFeatures.Practice.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Practice.Requests;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.Practice.View
{
	/// <summary>
	/// View for Practice.
	/// </summary>
	public sealed class PracticeView : BaseView
	{
		[SerializeField]
		private string _message = "Hello";

		/// <summary>
		/// Example method to send a request.
		/// </summary>
		public void SendEcho()
		{
			SendRequest(PracticeRequests.Echo, _message);
		}

		/// <summary>
		/// Example event handler (auto-bound).
		/// </summary>
		/// <param name="payload">Payload from controller.</param>
		[OnEvent(PracticeEvents.Echoed)]
		private void OnEchoed(object payload)
		{
			Debug.Log("[PracticeView] Echoed: " + payload, this);
		}
	}
}
