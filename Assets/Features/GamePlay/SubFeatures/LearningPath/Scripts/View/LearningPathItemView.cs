using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Features.GamePlay.SubFeatures.LearningPath.View
{
	/// <summary>
	/// View for one learning path item entry.
	/// </summary>
	public class LearningPathItemView : MonoBehaviour
	{
		[SerializeField]
		private TextMeshProUGUI _contextText;

		[SerializeField]
		private TextMeshProUGUI _vocabularyText;

		[SerializeField]
		private Button _editButton;

		public int LearningPathId { get; private set; }

		/// <summary>
		/// Raised when edit is requested for this learning path.
		/// </summary>
		public event Action<LearningPathItemView> EditRequested;

		private void Awake()
		{
			if (_editButton != null)
			{
				_editButton.onClick.RemoveAllListeners();
				_editButton.onClick.AddListener(HandleEditClicked);
			}
		}

		private void OnDestroy()
		{
			if (_editButton != null)
			{
				_editButton.onClick.RemoveListener(HandleEditClicked);
			}
		}

		/// <summary>
		/// Binds learning path data to this item view.
		/// </summary>
		/// <param name="id">Learning path id.</param>
		/// <param name="context">Learning path context description.</param>
		/// <param name="vocabulary">Comma-separated vocabulary.</param>
		public void Bind(int id, string context, string vocabulary)
		{
			LearningPathId = id;

			if (_contextText != null)
			{
				_contextText.text = context ?? string.Empty;
			}

			if (_vocabularyText != null)
			{
				_vocabularyText.text = vocabulary ?? string.Empty;
			}
		}

		private void HandleEditClicked()
		{
			EditRequested?.Invoke(this);
		}
	}
}