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
	/// View for Setting.
	/// </summary>
	public sealed class SettingView : BaseView
	{
		private const string NoneOptionLabel = "Không chọn";
		private const int MinAge = 0;
		private const int MaxAge = 120;
		private const string SelectedModelKey = "SelectedModel";

		private static readonly (string label, string id)[] ModelOptions = new[]
		{
			("Gemini 3 Flash Preview", "gemini-3-flash-preview"),
			("Gemini 3 Pro Preview", "gemini-3.1-pro-preview"),
			("Gemini 3 Flash Lite Preview", "gemini-3.1-flash-lite-preview"),
			("Gemini Flash Lite Lastest", "gemini-flash-lite-latest"),
			("Ollama Local", "ollama")
		};

		[Header("Input Fields")]
		[SerializeField] private TMP_InputField _nameInputField;
		[SerializeField] private TMP_InputField _ageInputField;
		[SerializeField] private TMP_InputField _descriptionInputField;

		[Header("Buttons")]
		[SerializeField] private Button _increaseAgeButton;
		[SerializeField] private Button _decreaseAgeButton;
		[SerializeField] private Button _saveButton;
		[SerializeField] private Button _logoutButton;

		[Header("Dropdowns")]
		[SerializeField] private TMP_Dropdown _levelDropdown;
		[SerializeField] private TMP_Dropdown _currentStoryDropdown;
		[SerializeField] private TMP_Dropdown _voiceNameDropdown;
		[SerializeField] private TMP_Dropdown _modelDropdown;

		[Header("Voice Settings")]
		[SerializeField] private Slider _pitchSlider;
		[SerializeField] private TextMeshProUGUI _pitchValueLabel;

		private readonly List<SettingLevelOptionPayload> _levelOptions = new List<SettingLevelOptionPayload>();
		private readonly List<SettingStoryOptionPayload> _storyOptions = new List<SettingStoryOptionPayload>();

		private bool _uiBound;
		private bool _suppressUiEvents;
		private SettingViewState _viewState;

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
			BindButton(_logoutButton, HandleLogoutClicked);

			_ageInputField?.onEndEdit.AddListener(HandleAgeInputChanged);
			_pitchSlider?.onValueChanged.AddListener(HandlePitchChanged);
			_modelDropdown?.onValueChanged.AddListener(HandleModelChanged);
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

			SetTextFieldValue(_nameInputField, string.Empty);
			SetTextFieldValue(_descriptionInputField, string.Empty);
			SetAgeValue(null);
			PopulateDropdown(_levelDropdown, _levelOptions, opt => opt.Name ?? $"Cấp {opt.Id}", null);
			PopulateDropdown(_currentStoryDropdown, _storyOptions, opt => opt.Name ?? $"Story {opt.Id}", null);
			ApplyPitchToSlider(null);
			InitializeModelDropdown();
		}

		private void RenderProfile(SettingProfilePayload profile)
		{
			SetTextFieldValue(_nameInputField, profile.Name);
			SetTextFieldValue(_descriptionInputField, profile.Description);

			_levelOptions.Clear();
			if (profile.Levels != null) _levelOptions.AddRange(profile.Levels);

			_storyOptions.Clear();
			if (profile.Stories != null) _storyOptions.AddRange(profile.Stories);

			SetAgeValue(profile.Age);
			PopulateDropdown(_levelDropdown, _levelOptions, opt => opt.Name ?? $"Cấp {opt.Id}", profile.LevelId);
			PopulateDropdown(_currentStoryDropdown, _storyOptions, opt => opt.Name ?? $"Story {opt.Id}", profile.CurrentStoryId);
			ApplyPitchToSlider(profile.Pitch);
			RenderModelSelection();
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
		/// Reads the current voice dropdown selection.
		/// </summary>
		private string GetVoiceSelection()
		{
			if (_voiceNameDropdown?.options == null || _voiceNameDropdown.options.Count == 0)
			{
				return null;
			}

			var index = Mathf.Clamp(_voiceNameDropdown.value, 0, _voiceNameDropdown.options.Count - 1);
			var text = _voiceNameDropdown.options[index]?.text?.Trim() ?? string.Empty;

			if (string.IsNullOrWhiteSpace(text) || text.StartsWith("option", StringComparison.OrdinalIgnoreCase))
			{
				return null;
			}
			return text;
		}

		#endregion

		#region UI Change Handlers

		private void HandlePitchChanged(float value)
		{
			if (_suppressUiEvents) return;
			UpdatePitchLabel(value);
		}

		private void HandleAgeInputChanged(string rawValue)
		{
			SetAgeValue(ParseAge(rawValue));
		}

		private void HandleModelChanged(int index)
		{
			if (_suppressUiEvents) return;
			if (index < 0 || index >= ModelOptions.Length) return;

			var modelId = ModelOptions[index].id;
			PlayerPrefs.SetString(SelectedModelKey, modelId);
			PlayerPrefs.Save();
			Debug.Log($"[SettingView] Saved model: {modelId}");
		}

		#endregion

		#region Model Helpers

		private void InitializeModelDropdown()
		{
			if (_modelDropdown == null) return;

			_suppressUiEvents = true;
			_modelDropdown.options.Clear();
			foreach (var option in ModelOptions)
			{
				_modelDropdown.options.Add(new TMP_Dropdown.OptionData(option.label));
			}
			_suppressUiEvents = false;

			RenderModelSelection();
		}

		private void RenderModelSelection()
		{
			if (_modelDropdown == null) return;

			var savedModel = PlayerPrefs.GetString(SelectedModelKey, "gemini-flash-lite-latest");
			var index = 0;
			for (var i = 0; i < ModelOptions.Length; i++)
			{
				if (ModelOptions[i].id == savedModel)
				{
					index = i;
					break;
				}
			}

			_suppressUiEvents = true;
			_modelDropdown.value = index;
			_modelDropdown.RefreshShownValue();
			_suppressUiEvents = false;
		}

		#endregion

		#region Pitch Helpers

		private void ApplyPitchToSlider(float? pitch)
		{
			if (_pitchSlider == null) return;

			_suppressUiEvents = true;
			if (pitch.HasValue) _pitchSlider.value = pitch.Value;
			_suppressUiEvents = false;
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
			var current = GetAgeValue() ?? 0;
			SetAgeValue(Mathf.Clamp(current + delta, MinAge, MaxAge));
		}

		private int? GetAgeValue()
		{
			if (_ageInputField == null) return null;
			var text = _ageInputField.text?.Trim() ?? string.Empty;
			if (string.IsNullOrEmpty(text) || text == "--") return null;
			return int.TryParse(text, out var parsed) ? Mathf.Clamp(parsed, MinAge, MaxAge) : (int?)null;
		}

		private void SetAgeValue(int? age)
		{
			_ageInputField?.SetTextWithoutNotify(age?.ToString() ?? "--");
		}

		private static int? ParseAge(string rawValue)
		{
			var sanitized = rawValue?.Trim() ?? string.Empty;
			if (string.IsNullOrEmpty(sanitized) || sanitized == "--") return null;
			return int.TryParse(sanitized, out var parsed) ? Mathf.Clamp(parsed, MinAge, MaxAge) : (int?)null;
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
			var request = new SettingProfileSaveRequestPayload
			{
				Name = GetTextFieldValue(_nameInputField),
				Age = GetAgeValue(),
				Description = GetTextFieldValue(_descriptionInputField),
				LevelId = GetSelectedDropdownId(_levelDropdown, _levelOptions),
				CurrentStoryId = GetSelectedDropdownId(_currentStoryDropdown, _storyOptions),
				VoiceName = GetVoiceSelection(),
				Pitch = _pitchSlider.value
			};

			SendRequest(SettingRequests.SaveProfile, request);
		}

		private void HandleLogoutClicked()
		{
			SendRequest(SettingRequests.Logout);
		}

		private static int? GetSelectedDropdownId<T>(TMP_Dropdown dropdown, List<T> options) where T : class
		{
			if (dropdown == null) return null;
			var index = dropdown.value;
			if (index <= 0 || index - 1 >= options.Count) return null;
			return GetIdFromOption(options[index - 1]);
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
			SetInteractable(_logoutButton, isInteractable);
			SetInteractable(_modelDropdown, isInteractable);
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
