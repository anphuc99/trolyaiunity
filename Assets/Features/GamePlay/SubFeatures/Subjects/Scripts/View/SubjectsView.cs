using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.Subjects.Events;
using Features.GamePlay.SubFeatures.Subjects.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Subjects.Requests;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.Subjects.View
{
	/// <summary>
	/// View for Subjects.
	/// </summary>
	public sealed class SubjectsView : BaseView
	{
		[SerializeField]
		private string _message = "Hello";

		/// <summary>
		/// Example method to send a request.
		/// </summary>
		public void SendEcho()
		{
			SendRequest(SubjectsRequests.Echo, _message);
		}

		/// <summary>
		/// Example event handler (auto-bound).
		/// </summary>
		/// <param name="payload">Payload from controller.</param>
		[OnEvent(SubjectsEvents.Echoed)]
		private void OnEchoed(object payload)
		{
			Debug.Log("[SubjectsView] Echoed: " + payload, this);
		}
	}
}
