using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.Character.Events;
using Features.GamePlay.SubFeatures.Character.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Character.Requests;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.Character.View
{
	/// <summary>
	/// View for Character.
	/// </summary>
	public sealed class CharacterView : BaseView
	{
		[SerializeField]
		private string _message = "Hello";

		/// <summary>
		/// Example method to send a request.
		/// </summary>
		public void SendEcho()
		{
			SendRequest(CharacterRequests.Echo, _message);
		}

		/// <summary>
		/// Example event handler (auto-bound).
		/// </summary>
		/// <param name="payload">Payload from controller.</param>
		[OnEvent(CharacterEvents.Echoed)]
		private void OnEchoed(object payload)
		{
			Debug.Log("[CharacterView] Echoed: " + payload, this);
		}

		/// <summary>
		/// Shows this subfeature view when its controller is installed.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(CharacterEvents.Installed)]
		private void OnInstalled(object payload)
		{
			gameObject.SetActive(true);
		}

		/// <summary>
		/// Hides this subfeature view when its controller is uninstalled.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(CharacterEvents.Uninstalled)]
		private void OnUninstalled(object payload)
		{
			gameObject.SetActive(false);
		}
	}
}
