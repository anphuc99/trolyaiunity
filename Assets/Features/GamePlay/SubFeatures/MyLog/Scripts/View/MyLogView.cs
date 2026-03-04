using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.MyLog.Events;
using Features.GamePlay.SubFeatures.MyLog.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.MyLog.Requests;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.MyLog.View
{
	/// <summary>
	/// View for MyLog.
	/// </summary>
	public sealed class MyLogView : BaseView
	{
		[SerializeField]
		private string _message = "Hello";

		/// <summary>
		/// Example method to send a request.
		/// </summary>
		public void SendEcho()
		{
			SendRequest(MyLogRequests.Echo, _message);
		}

		/// <summary>
		/// Example event handler (auto-bound).
		/// </summary>
		/// <param name="payload">Payload from controller.</param>
		[OnEvent(MyLogEvents.Echoed)]
		private void OnEchoed(object payload)
		{
			Debug.Log("[MyLogView] Echoed: " + payload, this);
		}
	}
}
