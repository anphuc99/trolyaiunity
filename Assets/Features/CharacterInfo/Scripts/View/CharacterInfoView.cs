using Core.Infrastructure.Views;
using Features.CharacterInfo.Events;
using Features.CharacterInfo.Infrastructure.Attributes;
using Features.CharacterInfo.Requests;
using Share.Components;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Features.CharacterInfo.View
{
	/// <summary>
	/// View for CharacterInfo.
	/// </summary>
	public sealed class CharacterInfoView : BaseView
	{
		[Header("Thông tin cơ bản")]
		[SerializeField] 
		private TextMeshProUGUI _name;
		[SerializeField]
		private Image _avatar;
		[SerializeField]
		private TextMeshProUGUI _age;
		[SerializeField]
		private TextMeshProUGUI _description;

		[Header("Giới tính")]
		[SerializeField]
		private TextMeshProUGUI _gender;
		[SerializeField]
		private Image _genderIcon;
		[SerializeField]
		private Sprite _maleIcon;
		[SerializeField]
		private Sprite _femaleIcon;
		
		[Header("Giọng nói")]
		[SerializeField]
		private TextMeshProUGUI _voiceName;
		[SerializeField]
		private TextMeshProUGUI _pitch;
		[SerializeField]
		private Slider _pitchSlider;

		[Header("Chỉnh sửa")]
		[SerializeField]
		private Button _editButton;
		[SerializeField]
		private Button _removeButton;
		[SerializeField]
		private Button _backButton;

		private const string NotAvailableText = "-";

		[OnEvent(CharacterInfoEvents.SelectedCharacterLoaded)]
		private void OnSelectedCharacterLoaded(object payload)
		{
			var info = payload as SelectedCharacterInfo;
			Render(info);
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			SendRequest(CharacterInfoRequests.LoadSelectedCharacter);
		}

		private void Render(SelectedCharacterInfo info)
		{
			if (_name != null)
			{
				_name.text = string.IsNullOrWhiteSpace(info?.Name) ? NotAvailableText : info.Name.Trim();
			}

			if (_avatar != null)
			{
				_avatar.sprite = info?.Avatar;
				_avatar.enabled = info?.Avatar != null;
			}

			if (_age != null)
			{
				_age.text = info?.Age.HasValue == true ? info.Age.Value.ToString() : NotAvailableText;
			}

			if (_description != null)
			{
				_description.text = string.IsNullOrWhiteSpace(info?.Description) ? NotAvailableText : info.Description.Trim();
			}

			if (_gender != null)
			{
				_gender.text = string.IsNullOrWhiteSpace(info?.Gender) ? NotAvailableText : info.Gender.Trim();
			}

			if (_genderIcon != null)
			{
				var normalizedGender = info?.Gender?.Trim().ToLowerInvariant();
				if (normalizedGender == "male")
				{
					_genderIcon.sprite = _maleIcon;
					_genderIcon.enabled = _maleIcon != null;
				}
				else if (normalizedGender == "female")
				{
					_genderIcon.sprite = _femaleIcon;
					_genderIcon.enabled = _femaleIcon != null;
				}
				else
				{
					_genderIcon.sprite = null;
					_genderIcon.enabled = false;
				}
			}

			if (_voiceName != null)
			{
				_voiceName.text = string.IsNullOrWhiteSpace(info?.VoiceName) ? NotAvailableText : info.VoiceName.Trim();
			}

			if (_pitch != null)
			{
				_pitch.text = info?.Pitch.HasValue == true ? info.Pitch.Value.ToString("0.##") : NotAvailableText;
			}

			if (_pitchSlider != null)
			{
				_pitchSlider.value = info?.Pitch ?? 0f;
			}
		}
	}
}
