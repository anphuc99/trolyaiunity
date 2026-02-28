using System;
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
	/// Represents the current operational state of the view.
	/// </summary>
	internal enum SettingViewState
	{
		Idle,
		Loading,
		Saving
	}

	/// <summary>
	/// Holds all editable form data for the Setting screen.
	/// </summary>
	internal struct SettingFormState
	{
		public int? Age;
		public int? LevelId;
		public int? StoryId;
		public float? Pitch;
		public string VoiceName;

		public static SettingFormState CreateDefault(string defaultVoiceName)
		{
			return new SettingFormState
			{
				Age = null,
				LevelId = null,
				StoryId = null,
				Pitch = null,
				VoiceName = defaultVoiceName
			};
		}

		public static SettingFormState FromProfile(SettingProfilePayload profile, string fallbackVoiceName)
		{
			return new SettingFormState
			{
				Age = profile.Age,
				LevelId = profile.LevelId,
				StoryId = profile.CurrentStoryId,
				Pitch = profile.Pitch,
				VoiceName = string.IsNullOrWhiteSpace(profile.VoiceName) ? fallbackVoiceName : profile.VoiceName.Trim()
			};
		}
	}

	/// <summary>
	/// View for Setting.
	/// </summary>
	public sealed class SettingView : BaseView
	{
		private const string NoneOptionLabel = "Không chọn";
		private const int MinAge = 0;
		private const int MaxAge = 120;

		[Header("Input Fields")]
		[SerializeField] private TMP_InputField _nameInputField;
		[SerializeField] private TMP_InputField _ageInputField;
		[SerializeField] private TMP_InputField _descriptionInputField;

		[Header("Buttons")]
		[SerializeField] private Button _increaseAgeButton;
		[SerializeField] private Button _decreaseAgeButton;
		[SerializeField] private Button _saveButton;

		[Header("Dropdowns")]
		[SerializeField] private TMP_Dropdown _levelDropdown;
		[SerializeField] private TMP_Dropdown _currentStoryDropdown;
		[SerializeField] private TMP_Dropdown _voiceNameDropdown;

		[Header("Voice Settings")]
		[SerializeField]
		[Tooltip("VoiceName value configured in the Inspector that should remain selected even after profile data loads.")]
		private string _defaultVoiceName;
		[SerializeField] private Slider _pitchSlider;
		[SerializeField] private TextMeshProUGUI _pitchValueLabel;

		private readonly List<SettingLevelOptionPayload> _levelOptions = new List<SettingLevelOptionPayload>();
		private readonly List<SettingStoryOptionPayload> _storyOptions = new List<SettingStoryOptionPayload>();

		private bool _uiBound;
		private bool _suppressUiEvents;
		private SettingViewState _viewState;
		private SettingFormState _formState;

		#region Event Handlers

		[OnEvent(SettingEvents.Installed)]
		private void OnInstalled(object payload)
		{
			gameObject.SetActive(true);
			BindUi();
			ResetEditingState();
			SendRequest(SettingRequests.LoadProfile);
		}

		[OnEvent(SettingEvents.Uninstalled)]
		private void OnUninstalled(object payload)
		{
			gameObject.SetActive(false);
			SetViewState(SettingViewState.Idle);
		}

		[OnEvent(SettingEvents.ProfileLoadStarted)]
		private void OnProfileLoadStarted(object payload) => SetViewState(SettingViewState.Loading);

		[OnEvent(SettingEvents.ProfileLoaded)]
		private void OnProfileLoaded(object payload)
		{
			SetViewState(SettingViewState.Idle);
			if (payload is SettingProfilePayload profile)
			{
				RenderProfile(profile);
			}
		}

		[OnEvent(SettingEvents.ProfileLoadFailed)]
		private void OnProfileLoadFailed(object payload)
		{
			SetViewState(SettingViewState.Idle);
			LogError(payload as SettingErrorPayload, "Không thể tải cài đặt người dùng.");
		}

		[OnEvent(SettingEvents.ProfileSaveStarted)]
		private void OnProfileSaveStarted(object payload) => SetViewState(SettingViewState.Saving);

		[OnEvent(SettingEvents.ProfileSaveSucceeded)]
		private void OnProfileSaveSucceeded(object payload) => SetViewState(SettingViewState.Idle);

		[OnEvent(SettingEvents.ProfileSaveFailed)]
		private void OnProfileSaveFailed(object payload)
		{
			SetViewState(SettingViewState.Idle);
			LogError(payload as SettingErrorPayload, "Không thể lưu cài đặt.");
		}

		#endregion

		#region UI Binding

		private void BindUi()
		{
			if (_uiBound) return;
			_uiBound = true;

			BindButton(_increaseAgeButton, () => AdjustAge(1));
			BindButton(_decreaseAgeButton, () => AdjustAge(-1));
			BindButton(_saveButton, HandleSaveClicked);

			_ageInputField?.onEndEdit.AddListener(HandleAgeInputChanged);
			_levelDropdown?.onValueChanged.AddListener(HandleLevelChanged);
			_currentStoryDropdown?.onValueChanged.AddListener(HandleStoryChanged);
			_pitchSlider?.onValueChanged.AddListener(HandlePitchChanged);
		}

		private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
		{
			if (button == null) return;
			button.onClick.RemoveAllListeners();
			button.onClick.AddListener(action);
		}

		#endregion

		#region State Management

		private void SetViewState(SettingViewState state)
		{
			_viewState = state;
			UpdateInteractableState();
		}

		private void ResetEditingState()
		{
			_levelOptions.Clear();
			_storyOptions.Clear();
			_formState = SettingFormState.CreateDefault(GetDefaultVoiceName());

			SetTextFieldValue(_nameInputField, string.Empty);
			SetTextFieldValue(_descriptionInputField, string.Empty);
			UpdateAgeDisplay();
			PopulateDropdown(_levelDropdown, _levelOptions, opt => opt.Name ?? $"Cấp {opt.Id}", _formState.LevelId);
			PopulateDropdown(_currentStoryDropdown, _storyOptions, opt => opt.Name ?? $"Story {opt.Id}", _formState.StoryId);
			ApplyPitchToSlider(_formState.Pitch);
		}

		private void RenderProfile(SettingProfilePayload profile)
		{
			SetTextFieldValue(_nameInputField, profile.Name);
			SetTextFieldValue(_descriptionInputField, profile.Description);

			_formState = SettingFormState.FromProfile(profile, GetDefaultVoiceName());

			_levelOptions.Clear();
			if (profile.Levels != null) _levelOptions.AddRange(profile.Levels);

			_storyOptions.Clear();
			if (profile.Stories != null) _storyOptions.AddRange(profile.Stories);

			UpdateAgeDisplay();
			PopulateDropdown(_levelDropdown, _levelOptions, opt => opt.Name ?? $"Cấp {opt.Id}", _formState.LevelId);
			PopulateDropdown(_currentStoryDropdown, _storyOptions, opt => opt.Name ?? $"Story {opt.Id}", _formState.StoryId);
			ApplyPitchToSlider(_formState.Pitch);
		}

		#endregion

		#region Dropdown Helpers

		/// <summary>
		/// Generic method to populate a dropdown with options and select the appropriate value.
		/// </summary>
		private void PopulateDropdown<T>(TMP_Dropdown dropdown, List<T> options, Func<T, string> labelSelector, int? selectedId)
			where T : class
		{
			if (dropdown == null) return;

			_suppressUiEvents = true;
			dropdown.options.Clear();
			dropdown.options.Add(new TMP_Dropdown.OptionData(NoneOptionLabel));

			foreach (var option in options)
			{
				var label = labelSelector(option);
				dropdown.options.Add(new TMP_Dropdown.OptionData(label));
			}

			dropdown.value = FindDropdownIndex(options, selectedId);
			dropdown.RefreshShownValue();
			_suppressUiEvents = false;
		}

		/// <summary>
		/// Finds the dropdown index for the given ID using duck-typing (assumes T has an Id property).
		/// </summary>
		private static int FindDropdownIndex<T>(List<T> options, int? selectedId) where T : class
		{
			if (!selectedId.HasValue) return 0;

			for (var i = 0; i < options.Count; i++)
			{
				var id = GetIdFromOption(options[i]);
				if (id == selectedId.Value) return i + 1;
			}
			return 0;
		}

		/// <summary>
		/// Extracts Id from option using reflection-like approach for supported types.
		/// </summary>
		private static int? GetIdFromOption<T>(T option) where T : class
		{
			return option switch
			{
				SettingLevelOptionPayload level => level.Id,
				SettingStoryOptionPayload story => story.Id,
				_ => null
			};
		}

		#endregion

		#region Voice Helpers

		/// <summary>
		/// Returns the trimmed default voice identifier configured via the Inspector.
		/// </summary>
		private string GetDefaultVoiceName()
			=> string.IsNullOrWhiteSpace(_defaultVoiceName) ? null : _defaultVoiceName.Trim();

		/// <summary>
		/// Reads the current voice dropdown selection.
		/// </summary>
		private string GetVoiceSelection()
		{
			if (_voiceNameDropdown?.options == null || _voiceNameDropdown.options.Count == 0)
			{
				return GetDefaultVoiceName();
			}

			var index = Mathf.Clamp(_voiceNameDropdown.value, 0, _voiceNameDropdown.options.Count - 1);
			var text = _voiceNameDropdown.options[index]?.text?.Trim() ?? string.Empty;

			if (string.IsNullOrWhiteSpace(text) || text.StartsWith("option", StringComparison.OrdinalIgnoreCase))
			{
				return GetDefaultVoiceName();
			}
			return text;
		}

		#endregion

		#region UI Change Handlers

		private void HandleLevelChanged(int index)
		{
			if (_suppressUiEvents) return;
			_formState.LevelId = GetSelectedId(_levelOptions, index);
		}

		private void HandleStoryChanged(int index)
		{
			if (_suppressUiEvents) return;
			_formState.StoryId = GetSelectedId(_storyOptions, index);
		}

		private void HandlePitchChanged(float value)
		{
			if (_suppressUiEvents) return;
			_formState.Pitch = value;
			UpdatePitchLabel(value);
		}

		private void HandleAgeInputChanged(string rawValue)
		{
			_formState.Age = ParseAge(rawValue);
			UpdateAgeDisplay();
		}

		private static int? GetSelectedId<T>(List<T> options, int index) where T : class
		{
			if (index <= 0 || index - 1 >= options.Count) return null;
			return GetIdFromOption(options[index - 1]);
		}

		#endregion

		#region Pitch Helpers

		private void ApplyPitchToSlider(float? pitch)
		{
			if (_pitchSlider == null) return;

			_suppressUiEvents = true;
			if (pitch.HasValue) _pitchSlider.value = pitch.Value;
			_suppressUiEvents = false;

			_formState.Pitch = pitch;
			UpdatePitchLabel(_pitchSlider.value);
		}

		private void UpdatePitchLabel(float value)
		{
			if (_pitchValueLabel != null)
			{
				_pitchValueLabel.text = $"Pitch: {value:0.00}";
			}
		}

		#endregion

		#region Age Helpers

		private void AdjustAge(int delta)
		{
			var current = _formState.Age ?? 0;
			_formState.Age = Mathf.Clamp(current + delta, MinAge, MaxAge);
			UpdateAgeDisplay();
		}

		private int? ParseAge(string rawValue)
		{
			var sanitized = rawValue?.Trim() ?? string.Empty;
			if (string.IsNullOrEmpty(sanitized) || sanitized == "--") return null;
			if (int.TryParse(sanitized, out var parsed)) return Mathf.Clamp(parsed, MinAge, MaxAge);
			return _formState.Age;
		}

		private void UpdateAgeDisplay()
		{
			_ageInputField?.SetTextWithoutNotify(_formState.Age?.ToString() ?? "--");
		}

		#endregion

		#region Text Field Helpers

		private static void SetTextFieldValue(TMP_InputField field, string value)
		{
			if (field != null) field.text = value ?? string.Empty;
		}

		private static string GetTextFieldValue(TMP_InputField field)
			=> field?.text ?? string.Empty;

		#endregion

		#region Save Handler

		private void HandleSaveClicked()
		{
			// Sync age from input before saving
			if (_ageInputField != null)
			{
				_formState.Age = ParseAge(_ageInputField.text);
			}

			// Resolve final pitch from slider if available
			if (_pitchSlider != null)
			{
				_formState.Pitch = _pitchSlider.value;
			}

			// Resolve voice name with fallback
			var voiceFromUi = GetVoiceSelection();
			var finalVoiceName = string.IsNullOrWhiteSpace(voiceFromUi) ? _formState.VoiceName : voiceFromUi;

			var request = new SettingProfileSaveRequestPayload
			{
				Name = GetTextFieldValue(_nameInputField),
				Age = _formState.Age,
				Description = GetTextFieldValue(_descriptionInputField),
				LevelId = _formState.LevelId,
				CurrentStoryId = _formState.StoryId,
				VoiceName = finalVoiceName,
				Pitch = _formState.Pitch
			};

			SendRequest(SettingRequests.SaveProfile, request);
		}

		#endregion

		#region Interactable State

		private void UpdateInteractableState()
		{
			var isInteractable = _viewState == SettingViewState.Idle;

			SetInteractable(_levelDropdown, isInteractable);
			SetInteractable(_currentStoryDropdown, isInteractable);
			SetInteractable(_voiceNameDropdown, isInteractable);
			SetInteractable(_pitchSlider, isInteractable);
			SetInteractable(_increaseAgeButton, isInteractable);
			SetInteractable(_decreaseAgeButton, isInteractable);
			SetInteractable(_saveButton, isInteractable);
		}

		private static void SetInteractable(Selectable selectable, bool interactable)
		{
			if (selectable != null) selectable.interactable = interactable;
		}

		#endregion

		#region Error Logging

		private void LogError(SettingErrorPayload payload, string fallback)
		{
			var message = string.IsNullOrWhiteSpace(payload?.Message) ? fallback : payload.Message;
			Debug.LogError($"[SettingView] {message}", this);
		}

		#endregion
	}
}
