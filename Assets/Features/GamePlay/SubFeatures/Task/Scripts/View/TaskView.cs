using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.Task.Events;
using Features.GamePlay.SubFeatures.Task.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Task.Requests;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.Task.View
{
	/// <summary>
	/// View for Task.
	/// </summary>
	public sealed class TaskView : BaseView
	{
		[SerializeField]
		private string _message = "Hello";

		/// <summary>
		/// Example method to send a request.
		/// </summary>
		public void SendEcho()
		{
			SendRequest(TaskRequests.Echo, _message);
		}

		/// <summary>
		/// Example event handler (auto-bound).
		/// </summary>
		/// <param name="payload">Payload from controller.</param>
		[OnEvent(TaskEvents.Echoed)]
		private void OnEchoed(object payload)
		{
			Debug.Log("[TaskView] Echoed: " + payload, this);
		}
	}
}
