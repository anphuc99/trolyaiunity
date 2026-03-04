using Core.Infrastructure.Views;
using Features.MimiLearn.Events;
using Features.MimiLearn.Infrastructure.Attributes;
using Features.MimiLearn.Requests;
using UnityEngine;

namespace Features.MimiLearn.View
{
	/// <summary>
	/// View for MimiLearn.
	/// </summary>
	public sealed class MimiLearnView : BaseView
	{
		[SerializeField]
		private string _message = "Hello";

		/// <summary>
		/// Example method to send a request.
		/// </summary>
		public void SendEcho()
		{
			SendRequest(MimiLearnRequests.Echo, _message);
		}

		/// <summary>
		/// Example event handler (auto-bound).
		/// </summary>
		/// <param name="payload">Payload from controller.</param>
		[OnEvent(MimiLearnEvents.Echoed)]
		private void OnEchoed(object payload)
		{
			Debug.Log("[MimiLearnView] Echoed: " + payload, this);
		}
	}
}
