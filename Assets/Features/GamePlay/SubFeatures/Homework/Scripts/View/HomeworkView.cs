using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.Homework.Events;
using Features.GamePlay.SubFeatures.Homework.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Homework.Requests;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.Homework.View
{
	/// <summary>
	/// View for Homework.
	/// </summary>
	public sealed class HomeworkView : BaseView
	{
		[SerializeField]
		private string _message = "Hello";

		/// <summary>
		/// Example method to send a request.
		/// </summary>
		public void SendEcho()
		{
			SendRequest(HomeworkRequests.Echo, _message);
		}

		/// <summary>
		/// Example event handler (auto-bound).
		/// </summary>
		/// <param name="payload">Payload from controller.</param>
		[OnEvent(HomeworkEvents.Echoed)]
		private void OnEchoed(object payload)
		{
			Debug.Log("[HomeworkView] Echoed: " + payload, this);
		}
	}
}
