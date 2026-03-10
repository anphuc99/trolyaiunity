using Features.GamePlay.SubFeatures.Knowledges.Model;
using TMPro;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.Knowledges.View
{
	/// <summary>
	/// View for a single knowledge list item.
	/// Displays the knowledge name and description (read-only, no interaction).
	/// </summary>
	public class KnowledgesItemView : MonoBehaviour
	{
		[SerializeField]
		private TextMeshProUGUI _nameText;

		[SerializeField]
		private TextMeshProUGUI _descriptionText;

		/// <summary>
		/// Initializes this item with knowledge data.
		/// </summary>
		/// <param name="knowledge">Knowledge data to display.</param>
		public void Initialize(KnowledgeItemPayload knowledge)
		{
			if (_nameText != null)
			{
				_nameText.text = knowledge?.Name ?? string.Empty;
			}

			if (_descriptionText != null)
			{
				_descriptionText.text = knowledge?.Description ?? string.Empty;
			}
		}
	}
}