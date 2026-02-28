using System.Collections.Generic;
using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.Setting.Events;
using Features.GamePlay.SubFeatures.Setting.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Setting.Model;
using Features.GamePlay.SubFeatures.Setting.Requests;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Features.GamePlay.SubFeatures.Setting.View
{
	/// <summary>
	/// View for Setting.
	/// </summary>
	public sealed class SettingView : BaseView
	{
		private const string NoneOptionLabel = "Không chọn";

		[SerializeField]
		private TextMeshProUGUI _nameText;
		[SerializeField]
		private TMP_InputField _nameInputField;
		[SerializeField]
		private TextMeshProUGUI _ageText;
		[SerializeField]
		private Button _increaseAgeButton;
		[SerializeField]
		private Button _decreaseAgeButton;
		[SerializeField]
		private TextMeshProUGUI _descriptionText;
		[SerializeField]
		private TMP_InputField _descriptionInputField;
		[SerializeField]
		private Dropdown _levelDropdown;
		[SerializeField]
		private Dropdown _currentStoryDropdown;
		[SerializeField]
		private Dropdown _voiceNameDropdown;
		[SerializeField]
		private Slider _pitchSlider;
		[SerializeField]
		private TextMeshProUGUI _pitchValueLabel;
		[SerializeField]
		private Button _saveButton;

		private readonly List<SettingLevelOptionPayload> _levelOptions = new List<SettingLevelOptionPayload>();
		private readonly List<SettingStoryOptionPayload> _storyOptions = new List<SettingStoryOptionPayload>();
		private readonly List<SettingVoiceOptionPayload> _voiceOptions = new List<SettingVoiceOptionPayload>();
		private bool _uiBound;
		private bool _isLoading;
		private bool _isSaving;
		private bool _suppressDropdownEvents;
		private bool _suppressPitchEvent;
		private int? _currentAge;
		private int? _selectedLevelId;
		private int? _selectedStoryId;
		private string _selectedVoiceName;
		private float? _selectedPitch;

		[OnEvent(SettingEvents.Installed)]
		private void OnInstalled(object payload)
		{
			gameObject.SetActive(true);
			ResolveInputFields();
			BindUi();
			ResetEditingState();
			SendRequest(SettingRequests.LoadProfile);
		}

		[OnEvent(SettingEvents.Uninstalled)]
		private void OnUninstalled(object payload)
		{
			gameObject.SetActive(false);
			_isLoading = false;
			_isSaving = false;
			UpdateInteractableState();
		}

		[OnEvent(SettingEvents.ProfileLoadStarted)]
		private void OnProfileLoadStarted(object payload)
		{
			_isLoading = true;
			UpdateInteractableState();
		}

		[OnEvent(SettingEvents.ProfileLoaded)]
		private void OnProfileLoaded(object payload)
		{
			_isLoading = false;
			UpdateInteractableState();

			if (payload is not SettingProfilePayload profile)
			{
				return;
			}

			RenderProfile(profile);
		}

		[OnEvent(SettingEvents.ProfileLoadFailed)]
		private void OnProfileLoadFailed(object payload)
		{
			_isLoading = false;
			UpdateInteractableState();
			LogError(payload as SettingErrorPayload, "Không thể tải cài đặt người dùng.");
		}

		[OnEvent(SettingEvents.ProfileSaveStarted)]
		private void OnProfileSaveStarted(object payload)
		{
			_isSaving = true;
			UpdateInteractableState();
		}

		[OnEvent(SettingEvents.ProfileSaveSucceeded)]
		private void OnProfileSaveSucceeded(object payload)
		{
			_isSaving = false;
			UpdateInteractableState();
		}

		[OnEvent(SettingEvents.ProfileSaveFailed)]
		private void OnProfileSaveFailed(object payload)
		{
			_isSaving = false;
			UpdateInteractableState();
			LogError(payload as SettingErrorPayload, "Không thể lưu cài đặt.");
		}

		private void BindUi()
		{
			if (_uiBound)
			{
				return;
			}

			_uiBound = true;

			if (_increaseAgeButton != null)
			{
				_increaseAgeButton.onClick.RemoveAllListeners();
				_increaseAgeButton.onClick.AddListener(() => AdjustAge(1));
			}

			if (_decreaseAgeButton != null)
			{
				_decreaseAgeButton.onClick.RemoveAllListeners();
				_decreaseAgeButton.onClick.AddListener(() => AdjustAge(-1));
			}

			if (_levelDropdown != null)
			{
				_levelDropdown.onValueChanged.RemoveListener(HandleLevelChanged);
				_levelDropdown.onValueChanged.AddListener(HandleLevelChanged);
			}

			if (_currentStoryDropdown != null)
			{
				_currentStoryDropdown.onValueChanged.RemoveListener(HandleStoryChanged);
				_currentStoryDropdown.onValueChanged.AddListener(HandleStoryChanged);
			}

			if (_voiceNameDropdown != null)
			{
				_voiceNameDropdown.onValueChanged.RemoveListener(HandleVoiceChanged);
				_voiceNameDropdown.onValueChanged.AddListener(HandleVoiceChanged);
			}

			if (_pitchSlider != null)
			{
				_pitchSlider.onValueChanged.RemoveListener(HandlePitchChanged);
				_pitchSlider.onValueChanged.AddListener(HandlePitchChanged);
			}

			if (_saveButton != null)
			{
				_saveButton.onClick.RemoveAllListeners();
				_saveButton.onClick.AddListener(HandleSaveClicked);
			}
		}

		private void ResolveInputFields()
		{
			if (_nameInputField == null && _nameText != null)
			{
				_nameInputField = _nameText.GetComponentInParent<TMP_InputField>();
			}

			if (_descriptionInputField == null && _descriptionText != null)
			{
				_descriptionInputField = _descriptionText.GetComponentInParent<TMP_InputField>();
			}
		}

		private void ResetEditingState()
		{
			_levelOptions.Clear();
			_storyOptions.Clear();
			_voiceOptions.Clear();
			_currentAge = null;
			_selectedLevelId = null;
			_selectedStoryId = null;
			_selectedVoiceName = null;
			_selectedPitch = null;
			SetNameValue(string.Empty);
			SetDescriptionValue(string.Empty);
			UpdateAgeText();
			PopulateLevelDropdown();
			PopulateStoryDropdown();
			PopulateVoiceDropdown();
			ApplyPitchToSlider(null);
		}

		private void RenderProfile(SettingProfilePayload profile)
		{
			SetNameValue(profile.Name ?? string.Empty);
			SetDescriptionValue(profile.Description ?? string.Empty);
			_currentAge = profile.Age;
			_selectedLevelId = profile.LevelId;
			_selectedStoryId = profile.CurrentStoryId;
			_selectedVoiceName = profile.VoiceName;
			_selectedPitch = profile.Pitch;

			_levelOptions.Clear();
			if (profile.Levels != null)
			{
				_levelOptions.AddRange(profile.Levels);
			}

			_storyOptions.Clear();
			if (profile.Stories != null)
			{
				_storyOptions.AddRange(profile.Stories);
			}

			_voiceOptions.Clear();
			if (profile.Voices != null)
			{
				_voiceOptions.AddRange(profile.Voices);
			}

			UpdateAgeText();
			PopulateLevelDropdown();
			PopulateStoryDropdown();
			PopulateVoiceDropdown();
			ApplyPitchToSlider(_selectedPitch);
		}

		private void PopulateLevelDropdown()
		{
			if (_levelDropdown == null)
			{
				return;
			}

			_suppressDropdownEvents = true;
			_levelDropdown.options.Clear();
			_levelDropdown.options.Add(new Dropdown.OptionData(NoneOptionLabel));
			for (var i = 0; i < _levelOptions.Count; i++)
			{
				var option = _levelOptions[i];
				var label = string.IsNullOrWhiteSpace(option?.Name) ? "Cấp " + option?.Id : option.Name;
				_levelDropdown.options.Add(new Dropdown.OptionData(label));
			}

			_levelDropdown.value = ResolveLevelIndex(_selectedLevelId, _levelOptions);
			_levelDropdown.RefreshShownValue();
			_suppressDropdownEvents = false;
		}

		private void PopulateStoryDropdown()
		{
			if (_currentStoryDropdown == null)
			{
				return;
			}

			_suppressDropdownEvents = true;
			_currentStoryDropdown.options.Clear();
			_currentStoryDropdown.options.Add(new Dropdown.OptionData(NoneOptionLabel));
			for (var i = 0; i < _storyOptions.Count; i++)
			{
				var option = _storyOptions[i];
				var label = string.IsNullOrWhiteSpace(option?.Name) ? "Story " + option?.Id : option.Name;
				_currentStoryDropdown.options.Add(new Dropdown.OptionData(label));
			}

			_currentStoryDropdown.value = ResolveStoryIndex(_selectedStoryId, _storyOptions);
			_currentStoryDropdown.RefreshShownValue();
			_suppressDropdownEvents = false;
		}

		private void PopulateVoiceDropdown()
		{
			if (_voiceNameDropdown == null)
			{
				return;
			}

			_suppressDropdownEvents = true;
			_voiceNameDropdown.options.Clear();
			_voiceNameDropdown.options.Add(new Dropdown.OptionData(NoneOptionLabel));
			for (var i = 0; i < _voiceOptions.Count; i++)
			{
				var option = _voiceOptions[i];
				var label = string.IsNullOrWhiteSpace(option?.Label) ? option?.VoiceName ?? "Voice" : option.Label;
				_voiceNameDropdown.options.Add(new Dropdown.OptionData(label));
			}

			_voiceNameDropdown.value = ResolveVoiceIndex(_selectedVoiceName);
			_voiceNameDropdown.RefreshShownValue();
			_suppressDropdownEvents = false;
		}

		private static int ResolveLevelIndex(int? selectedId, List<SettingLevelOptionPayload> options)
		{
			if (!selectedId.HasValue)
			{
				return 0;
			}

			for (var i = 0; i < options.Count; i++)
			{
				if (options[i]?.Id == selectedId.Value)
				{
					return i + 1;
				}
			}

			return 0;
		}

		private static int ResolveStoryIndex(int? selectedId, List<SettingStoryOptionPayload> options)
		{
			if (!selectedId.HasValue)
			{
				return 0;
			}

			for (var i = 0; i < options.Count; i++)
			{
				if (options[i]?.Id == selectedId.Value)
				{
					return i + 1;
				}
			}

			return 0;
		}

		private int ResolveVoiceIndex(string voiceName)
		{
			if (string.IsNullOrWhiteSpace(voiceName))
			{
				return 0;
			}

			for (var i = 0; i < _voiceOptions.Count; i++)
			{
				if (string.Equals(_voiceOptions[i]?.VoiceName, voiceName, System.StringComparison.OrdinalIgnoreCase))
				{
					return i + 1;
				}
			}

			return 0;
		}

		private void HandleLevelChanged(int index)
		{
			if (_suppressDropdownEvents)
			{
				return;
			}

			_selectedLevelId = index <= 0 || index - 1 >= _levelOptions.Count
				? null
				: _levelOptions[index - 1]?.Id;
		}

		private void HandleStoryChanged(int index)
		{
			if (_suppressDropdownEvents)
			{
				return;
			}

			_selectedStoryId = index <= 0 || index - 1 >= _storyOptions.Count
				? null
				: _storyOptions[index - 1]?.Id;
		}

		private void HandleVoiceChanged(int index)
		{
			if (_suppressDropdownEvents)
			{
				return;
			}

			if (index <= 0 || index - 1 >= _voiceOptions.Count)
			{
				_selectedVoiceName = null;
				return;
			}

			var option = _voiceOptions[index - 1];
			_selectedVoiceName = option?.VoiceName;
			if (option?.Pitch.HasValue == true)
			{
				ApplyPitchToSlider(option.Pitch);
			}
		}

		private void HandlePitchChanged(float value)
		{
			if (_suppressPitchEvent)
			{
				return;
			}

			_selectedPitch = value;
			UpdatePitchLabel(value);
		}

		private void ApplyPitchToSlider(float? pitch)
		{
			if (_pitchSlider == null)
			{
				return;
			}

			_suppressPitchEvent = true;
			_pitchSlider.value = pitch ?? _pitchSlider.value;
			_suppressPitchEvent = false;
			_selectedPitch = pitch ?? _pitchSlider.value;
			UpdatePitchLabel(_pitchSlider.value);
		}

		private void UpdatePitchLabel(float value)
		{
			if (_pitchValueLabel != null)
			{
				_pitchValueLabel.text = value.ToString("0.00");
			}
		}

		private void AdjustAge(int delta)
		{
			var value = _currentAge ?? 0;
			value = Mathf.Clamp(value + delta, 0, 120);
			_currentAge = value;
			UpdateAgeText();
		}

		private void UpdateAgeText()
		{
			if (_ageText != null)
			{
				_ageText.text = _currentAge.HasValue ? _currentAge.Value.ToString() : "--";
			}
		}

		private void SetNameValue(string value)
		{
			var finalValue = value ?? string.Empty;
			if (_nameInputField != null)
			{
				_nameInputField.text = finalValue;
			}

			if (_nameText != null)
			{
				_nameText.text = finalValue;
			}
		}

		private string GetNameInputValue()
		{
			if (_nameInputField != null)
			{
				return _nameInputField.text;
			}

			return _nameText != null ? _nameText.text : string.Empty;
		}

		private void SetDescriptionValue(string value)
		{
			var finalValue = value ?? string.Empty;
			if (_descriptionInputField != null)
			{
				_descriptionInputField.text = finalValue;
			}

			if (_descriptionText != null)
			{
				_descriptionText.text = finalValue;
			}
		}

		private string GetDescriptionInputValue()
		{
			if (_descriptionInputField != null)
			{
				return _descriptionInputField.text;
			}

			return _descriptionText != null ? _descriptionText.text : string.Empty;
		}

		private void HandleSaveClicked()
		{
			var request = new SettingProfileSaveRequestPayload
			{
				Name = GetNameInputValue(),
				Age = _currentAge,
				Description = GetDescriptionInputValue(),
				LevelId = _selectedLevelId,
				CurrentStoryId = _selectedStoryId,
				VoiceName = _selectedVoiceName,
				Pitch = _selectedPitch
			};

			SendRequest(SettingRequests.SaveProfile, request);
		}

		private void UpdateInteractableState()
		{
			var enabled = !_isLoading && !_isSaving;
			if (_levelDropdown != null)
			{
				_levelDropdown.interactable = enabled;
			}

			if (_currentStoryDropdown != null)
			{
				_currentStoryDropdown.interactable = enabled;
			}

			if (_voiceNameDropdown != null)
			{
				_voiceNameDropdown.interactable = enabled;
			}

			if (_pitchSlider != null)
			{
				_pitchSlider.interactable = enabled;
			}

			if (_increaseAgeButton != null)
			{
				_increaseAgeButton.interactable = enabled;
			}

			if (_decreaseAgeButton != null)
			{
				_decreaseAgeButton.interactable = enabled;
			}

			if (_saveButton != null)
			{
				_saveButton.interactable = !_isLoading && !_isSaving;
			}
		}

		private void LogError(SettingErrorPayload payload, string fallback)
		{
			var message = payload?.Message;
			if (string.IsNullOrWhiteSpace(message))
			{
				message = fallback;
			}

			Debug.LogError("[SettingView] " + message, this);
		}
	}
}
