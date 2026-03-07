using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Features.GamePlay.SubFeatures.Chat.Model;

namespace Features.GamePlay.SubFeatures.Chat.View
{
	/// <summary>
	/// Renders one character item inside chat add-character popup.
	/// </summary>
	public sealed class ChatSelectCharacterPopupItemView : MonoBehaviour
	{
		[SerializeField]
		private Image _avatarImage;

		[SerializeField]
		private TMP_Text _nameText;

		[SerializeField]
		private Toggle _toggle;

		private Action<ChatSelectableCharacterPayload, bool> _onSelectedChanged;
		private ChatSelectableCharacterPayload _payload;

		private void Awake()
		{
			if (_toggle != null)
			{
				_toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
				_toggle.onValueChanged.AddListener(OnToggleValueChanged);
			}
		}

		private void OnDestroy()
		{
			if (_toggle != null)
			{
				_toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
			}
		}

		/// <summary>
		/// Binds character payload to this item.
		/// </summary>
		/// <param name="payload">Character payload.</param>
		/// <param name="onSelectedChanged">Optional selection callback.</param>
		public void Bind(ChatSelectableCharacterPayload payload, Action<ChatSelectableCharacterPayload, bool> onSelectedChanged = null)
		{
			_payload = payload;
			_onSelectedChanged = onSelectedChanged;

			if (_nameText != null)
			{
				_nameText.text = payload?.Name ?? string.Empty;
			}

			if (_avatarImage != null)
			{
				_avatarImage.sprite = payload != null ? payload.Avatar : null;
			}

			if (_toggle != null)
			{
				_toggle.SetIsOnWithoutNotify(payload != null && payload.IsActive);
			}
		}

		private void OnToggleValueChanged(bool isOn)
		{
			_onSelectedChanged?.Invoke(_payload, isOn);
		}

		private void OnValidate()
		{
			if (_avatarImage == null)
			{
				var avatarTransform = transform.Find("Mask/Avatar");
				if (avatarTransform != null)
				{
					_avatarImage = avatarTransform.GetComponent<Image>();
				}
			}

			if (_nameText == null)
			{
				var nameTransform = transform.Find("Name");
				if (nameTransform != null)
				{
					_nameText = nameTransform.GetComponent<TMP_Text>();
				}
			}

			if (_toggle == null)
			{
				_toggle = GetComponentInChildren<Toggle>(true);
			}
		}

		public void OnSelect()
		{
			if (_toggle != null)
			{
				_toggle.isOn = !_toggle.isOn;
			}
		}
	}
}