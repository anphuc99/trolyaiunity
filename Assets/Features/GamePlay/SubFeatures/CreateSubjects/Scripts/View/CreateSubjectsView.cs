using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.CreateSubjects.Events;
using Features.GamePlay.SubFeatures.CreateSubjects.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.CreateSubjects.Requests;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.CreateSubjects.View
{
	/// <summary>
	/// View for CreateSubjects.
	/// </summary>
	public sealed class CreateSubjectsView : BaseView
	{
		[SerializeField]
		private string _message = "Hello";

		/// <summary>
		/// Example method to send a request.
		/// </summary>
		public void SendEcho()
		{
			SendRequest(CreateSubjectsRequests.Echo, _message);
		}

		/// <summary>
		/// Example event handler (auto-bound).
		/// </summary>
		/// <param name="payload">Payload from controller.</param>
		[OnEvent(CreateSubjectsEvents.Echoed)]
		private void OnEchoed(object payload)
		{
			Debug.Log("[CreateSubjectsView] Echoed: " + payload, this);
		}
	}
}
