using System;
using Features.GamePlay.SubFeatures.Subjects.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Features.GamePlay.SubFeatures.Subjects.View
{
	/// <summary>
	/// View for a single subject list item.
	/// Displays the subject name and handles click events.
	/// </summary>
	public class SubjectsItemView : MonoBehaviour
	{
		[SerializeField]
		private Button _buttonOpenSubject;

		[SerializeField]
		private TextMeshProUGUI _nameText;

		private SubjectItemPayload _subject;
		private Action<SubjectItemPayload> _onClicked;

		/// <summary>
		/// Initializes this item with subject data and a click callback.
		/// </summary>
		/// <param name="subject">Subject data to display.</param>
		/// <param name="onClicked">Callback invoked when the item is clicked.</param>
		public void Initialize(SubjectItemPayload subject, Action<SubjectItemPayload> onClicked)
		{
			_subject = subject;
			_onClicked = onClicked;

			if (_nameText != null)
			{
				_nameText.text = subject?.Name ?? string.Empty;
			}

			if (_buttonOpenSubject != null)
			{
				_buttonOpenSubject.onClick.RemoveAllListeners();
				_buttonOpenSubject.onClick.AddListener(OnButtonClicked);
			}
		}

		/// <summary>
		/// Handles button click by invoking the registered callback.
		/// </summary>
		private void OnButtonClicked()
		{
			_onClicked?.Invoke(_subject);
		}
	}
}