using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.Home.Events;
using Features.GamePlay.SubFeatures.Home.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Home.Requests;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.Home.View
{
	/// <summary>
	/// View for Home.
	/// </summary>
	public sealed class HomeView : BaseView
	{
		[SerializeField]
		private string _message = "Hello";

		/// <summary>
		/// Example method to send a request.
		/// </summary>
		public void SendEcho()
		{
			SendRequest(HomeRequests.Echo, _message);
		}

		/// <summary>
		/// Example event handler (auto-bound).
		/// </summary>
		/// <param name="payload">Payload from controller.</param>
		[OnEvent(HomeEvents.Echoed)]
		private void OnEchoed(object payload)
		{
			Debug.Log("[HomeView] Echoed: " + payload, this);
		}

		/// <summary>
		/// Shows this subfeature view when its controller is installed.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(HomeEvents.Installed)]
		private void OnInstalled(object payload)
		{
			gameObject.SetActive(true);
		}

		/// <summary>
		/// Hides this subfeature view when its controller is uninstalled.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(HomeEvents.Uninstalled)]
		private void OnUninstalled(object payload)
		{
			gameObject.SetActive(false);
		}

		public void OpenJournal()
		{
			SendRequest(HomeRequests.OpenJournal);
		}

		public void OpenCreateCharacter()
		{
			SendRequest(HomeRequests.OpenCreateCharacter);
		}
	}
}
