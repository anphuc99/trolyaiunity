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

		/// <summary>
		/// Shows this subfeature view when its controller is installed.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(TaskEvents.Installed)]
		private void OnInstalled(object payload)
		{
			gameObject.SetActive(true);
		}

		/// <summary>
		/// Hides this subfeature view when its controller is uninstalled.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(TaskEvents.Uninstalled)]
		private void OnUninstalled(object payload)
		{
			gameObject.SetActive(false);
		}
	}
}
