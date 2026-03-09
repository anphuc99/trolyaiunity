using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.Knowledges.Events;
using Features.GamePlay.SubFeatures.Knowledges.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Knowledges.Requests;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.Knowledges.View
{
	/// <summary>
	/// View for Knowledges subfeature.
	/// </summary>
	public sealed class KnowledgesView : BaseView
	{
		/// <summary>
		/// Shows the Knowledges view when installed.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(KnowledgesEvents.Installed)]
		private void OnInstalled(object payload)
		{
			gameObject.SetActive(true);
		}

		/// <summary>
		/// Hides the Knowledges view when uninstalled.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(KnowledgesEvents.Uninstalled)]
		private void OnUninstalled(object payload)
		{
			gameObject.SetActive(false);
		}
	}
}
