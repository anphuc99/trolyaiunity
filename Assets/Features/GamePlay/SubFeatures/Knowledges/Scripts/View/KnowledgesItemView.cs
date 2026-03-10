using System;
using Features.GamePlay.SubFeatures.Knowledges.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Features.GamePlay.SubFeatures.Knowledges.View
{
	/// <summary>
	/// View for a single knowledge list item.
	/// Displays the knowledge name and handles click events.
	/// </summary>
	public class KnowledgesItemView : MonoBehaviour
	{
		[SerializeField]
		private Button _buttonOpenKnowledge;

		[SerializeField]
		private TextMeshProUGUI _nameText;

		private KnowledgeItemPayload _knowledge;
		private Action<KnowledgeItemPayload> _onClicked;

		/// <summary>
		/// Initializes this item with knowledge data and a click callback.
		/// </summary>
		/// <param name="knowledge">Knowledge data to display.</param>
		/// <param name="onClicked">Callback invoked when the item is clicked.</param>
		public void Initialize(KnowledgeItemPayload knowledge, Action<KnowledgeItemPayload> onClicked)
		{
			_knowledge = knowledge;
			_onClicked = onClicked;

			if (_nameText != null)
			{
				_nameText.text = knowledge?.Name ?? string.Empty;
			}

			if (_buttonOpenKnowledge != null)
			{
				_buttonOpenKnowledge.onClick.RemoveAllListeners();
				_buttonOpenKnowledge.onClick.AddListener(OnButtonClicked);
			}
		}

		/// <summary>
		/// Handles button click by invoking the registered callback.
		/// </summary>
		private void OnButtonClicked()
		{
			_onClicked?.Invoke(_knowledge);
		}
	}
}