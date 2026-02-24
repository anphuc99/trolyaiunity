using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.Story.Events;
using Features.GamePlay.SubFeatures.Story.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Story.Requests;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.Story.View
{
	/// <summary>
	/// View for Story.
	/// </summary>
	public sealed class StoryView : BaseView
	{
		[SerializeField]
		private string _message = "Hello";

		/// <summary>
		/// Example method to send a request.
		/// </summary>
		public void SendEcho()
		{
			SendRequest(StoryRequests.Echo, _message);
		}

		/// <summary>
		/// Example event handler (auto-bound).
		/// </summary>
		/// <param name="payload">Payload from controller.</param>
		[OnEvent(StoryEvents.Echoed)]
		private void OnEchoed(object payload)
		{
			Debug.Log("[StoryView] Echoed: " + payload, this);
		}

		/// <summary>
		/// Shows this subfeature view when its controller is installed.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(StoryEvents.Installed)]
		private void OnInstalled(object payload)
		{
			gameObject.SetActive(true);
		}

		/// <summary>
		/// Hides this subfeature view when its controller is uninstalled.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(StoryEvents.Uninstalled)]
		private void OnUninstalled(object payload)
		{
			gameObject.SetActive(false);
		}
	}
}
