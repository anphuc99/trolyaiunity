using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.Chat.Events;
using Features.GamePlay.SubFeatures.Chat.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Chat.Requests;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.Chat.View
{
	/// <summary>
	/// View for Chat.
	/// </summary>
	public sealed class ChatView : BaseView
	{

		[SerializeField]
		private VirtualizedChatMessageContainer _messageContainer;

		/// <summary>
		/// Shows this subfeature view when its controller is installed.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(ChatEvents.Installed)]
		private void OnInstalled(object payload)
		{
			gameObject.SetActive(true);
		}

		/// <summary>
		/// Hides this subfeature view when its controller is uninstalled.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(ChatEvents.Uninstalled)]
		private void OnUninstalled(object payload)
		{
			gameObject.SetActive(false);
		}
	}
}
