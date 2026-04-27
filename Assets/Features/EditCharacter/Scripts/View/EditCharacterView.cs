using System;
using System.Collections;
using System.IO;
using Core.Infrastructure.Views;
using Features.EditCharacter.Events;
using Features.EditCharacter.Infrastructure;
using Features.EditCharacter.Infrastructure.Attributes;
using Features.EditCharacter.Model;
using Features.EditCharacter.Requests;
using Share.Model;
using Share.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Features.EditCharacter.View
{
	/// <summary>
	/// View for EditCharacter.
	/// </summary>
	public sealed class EditCharacterView : BaseView
	{
		[Header("Input Fields")]
		[SerializeField] private TMP_InputField _nameInput;
		[SerializeField] private TMP_InputField _ageInput;
		[SerializeField] private ToggleGroup _genderToggleGroup;
		[SerializeField] private TMP_InputField _descriptionInput;

		[Header("Voice")]
		[SerializeField] private TMP_Dropdown _voiceDropdown;
		[SerializeField] private Slider _pitchSlider;
		[SerializeField] private Slider _speakingRateSlider;
		[SerializeField] private Button _avatarUploadButton;
		[SerializeField] private Image _avatarPreviewImage;
		[SerializeField] private Button _closeButton;

		[Header("Actions")]
		[SerializeField] private Button _submitButton;
		private int _currentCharacterId;
		private bool _isUploadingAvatar;
		private string _uploadedAvatarUrl;

		[Header("Show pitch number")]
		[SerializeField] private TMP_Text _pitchNumberText;
		private string _pendingVoiceModel;
		private string _pendingVoiceName;

		protected override void OnEnabled()
		{
			TryAutoBindOptionalControls();
			SendRequest(EditCharacterRequests.FetchVoices);

			if (_submitButton != null)
			{
				_submitButton.onClick.RemoveListener(OnSubmitClicked);
				_submitButton.onClick.AddListener(OnSubmitClicked);
			}

			if (_avatarUploadButton != null)
			{
				_avatarUploadButton.onClick.RemoveListener(OnAvatarUploadClicked);
				_avatarUploadButton.onClick.AddListener(OnAvatarUploadClicked);
			}

			if (_closeButton != null)
			{
				_closeButton.onClick.RemoveListener(OnCloseClicked);
				_closeButton.onClick.AddListener(OnCloseClicked);
			}

			SendRequest(EditCharacterRequests.LoadSelectedCharacter);
		}

		protected override void OnDisabled()
		{
			if (_submitButton != null)
			{
				_submitButton.onClick.RemoveListener(OnSubmitClicked);
			}

			if (_avatarUploadButton != null)
			{
				_avatarUploadButton.onClick.RemoveListener(OnAvatarUploadClicked);
			}

			if (_closeButton != null)
			{
				_closeButton.onClick.RemoveListener(OnCloseClicked);
			}
		}

		private void TryAutoBindOptionalControls()
		{
			if (_avatarUploadButton == null)
			{
				_avatarUploadButton = FindChildComponentByName<Button>("Avatar", "avatar");
			}

			if (_avatarPreviewImage == null)
			{
				_avatarPreviewImage = _avatarUploadButton != null
					? _avatarUploadButton.GetComponent<Image>()
					: FindChildComponentByName<Image>("Avatar", "avatar");
			}

			if (_voiceDropdown == null)
			{
				_voiceDropdown = FindChildComponentByName<TMP_Dropdown>("DropdownGiọng", "Dropdown");
			}

			if (_pitchSlider == null)
			{
				_pitchSlider = FindChildComponentByName<Slider>("Slider", "Pitch");
			}

			if (_speakingRateSlider == null)
			{
				_speakingRateSlider = FindChildComponentByName<Slider>("SpeakingRate", "Rate", "Speed");
			}
		}

		private T FindChildComponentByName<T>(params string[] preferredNames) where T : Component
		{
			var components = GetComponentsInChildren<T>(true);
			for (var i = 0; i < components.Length; i += 1)
			{
				var component = components[i];
				if (component == null)
				{
					continue;
				}

				for (var j = 0; j < preferredNames.Length; j += 1)
				{
					var keyword = preferredNames[j] ?? string.Empty;
					if (!string.IsNullOrWhiteSpace(keyword) && component.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
					{
						return component;
					}
				}
			}

			return null;
		}

		[OnEvent(EditCharacterEvents.SelectedCharacterLoaded)]
		private void OnSelectedCharacterLoaded(object payload)
		{
			if (payload is not SelectedCharacterInfo selected)
			{
				return;
			}

			_currentCharacterId = selected.Id;
			_uploadedAvatarUrl = selected.AvatarUrl;

			if (_nameInput != null)
			{
				_nameInput.text = selected.Name ?? string.Empty;
			}

			if (_ageInput != null)
			{
				_ageInput.text = selected.Age.HasValue ? selected.Age.Value.ToString() : string.Empty;
			}

			if (_descriptionInput != null)
			{
				_descriptionInput.text = selected.Description ?? string.Empty;
			}

			_pendingVoiceModel = selected.VoiceModel;
			_pendingVoiceName = selected.VoiceName;
			TrySelectPendingVoiceOption();

			if (_pitchSlider != null)
			{
				_pitchSlider.value = selected.Pitch ?? 0f;
			}

			if (_speakingRateSlider != null)
			{
				_speakingRateSlider.value = selected.SpeakingRate ?? _speakingRateSlider.value;
			}

			ApplyGenderToggle(selected.Gender);
			UpdateAvatarPreviewFromSprite(selected.Avatar);
		}

		private void ApplyGenderToggle(string gender)
		{
			if (_genderToggleGroup == null)
			{
				return;
			}

			var normalizedGender = CharacterFormUtils.NormalizeGender(gender);
			var toggles = _genderToggleGroup.GetComponentsInChildren<Toggle>(true);
			for (var i = 0; i < toggles.Length; i++)
			{
				var toggle = toggles[i];
				if (toggle == null)
				{
					continue;
				}

				var toggleGender = CharacterFormUtils.NormalizeGender(toggle.name);
				if (string.IsNullOrWhiteSpace(toggleGender))
				{
					var label = toggle.GetComponentInChildren<TMP_Text>();
					toggleGender = CharacterFormUtils.NormalizeGender(label != null ? label.text : string.Empty);
				}

				toggle.isOn = !string.IsNullOrWhiteSpace(normalizedGender)
					&& !string.IsNullOrWhiteSpace(toggleGender)
					&& string.Equals(normalizedGender, toggleGender, StringComparison.Ordinal);
			}
		}

		private void OnCloseClicked()
		{
			SendRequest(EditCharacterRequests.CloseScope);
		}

		private void OnSubmitClicked()
		{
			if (_isUploadingAvatar)
			{
				EventBus.Publish(EditCharacterEvents.SubmitFailed, "Đang upload avatar, vui lòng đợi hoàn tất.");
				return;
			}

			var name = CharacterFormUtils.ToNullableString(_nameInput != null ? _nameInput.text : string.Empty);
			var personality = CharacterFormUtils.ToNullableString(_descriptionInput != null ? _descriptionInput.text : string.Empty);
			var gender = GetSelectedGender();

			if (_currentCharacterId <= 0 || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(personality) || string.IsNullOrWhiteSpace(gender))
			{
				EventBus.Publish(EditCharacterEvents.SubmitFailed, "Vui lòng nhập thông tin chỉnh sửa hợp lệ.");
				return;
			}

			TryResolveSelectedVoice(out var voiceModel, out var voiceName);
			var avatar = CharacterFormUtils.ToNullableString(_uploadedAvatarUrl);

			var payload = new EditCharacterPayload
			{
				id = _currentCharacterId,
				name = name,
				age = ParseAge(),
				gender = gender,
				personality = personality,
				appearance = null,
				avatar = avatar,
				voiceModel = voiceModel,
				voiceName = voiceName,
				pitch = _pitchSlider != null ? _pitchSlider.value : null,
				speakingRate = _speakingRateSlider != null ? _speakingRateSlider.value : null,
			};

			SendRequest(EditCharacterRequests.SubmitCharacter, payload);
		}

		private void OnAvatarUploadClicked()
		{
			if (_isUploadingAvatar)
			{
				return;
			}

			StartCoroutine(PickAndUploadAvatarCoroutine());
		}

		private IEnumerator PickAndUploadAvatarCoroutine()
		{
			_isUploadingAvatar = true;
			if (_avatarUploadButton != null)
			{
				_avatarUploadButton.interactable = false;
			}

			var selectedPath = string.Empty;
			yield return OpenImagePickerCoroutine(path => selectedPath = path);

			if (string.IsNullOrWhiteSpace(selectedPath))
			{
				ResetUploadingState();
				yield break;
			}

			byte[] imageBytes;
			try
			{
				imageBytes = File.ReadAllBytes(selectedPath);
			}
			catch (Exception ex)
			{
				ResetUploadingState();
				EventBus.Publish(EditCharacterEvents.AvatarUploadFailed, $"Không thể đọc ảnh: {ex.Message}");
				yield break;
			}

			if (imageBytes == null || imageBytes.Length == 0)
			{
				ResetUploadingState();
				EventBus.Publish(EditCharacterEvents.AvatarUploadFailed, "Ảnh avatar không hợp lệ.");
				yield break;
			}

			var dataUrl = DataUrlUtils.BuildImageDataUrl(selectedPath, imageBytes);
			if (string.IsNullOrWhiteSpace(dataUrl))
			{
				ResetUploadingState();
				EventBus.Publish(EditCharacterEvents.AvatarUploadFailed, "Định dạng ảnh chưa được hỗ trợ. Dùng PNG, JPG hoặc WEBP.");
				yield break;
			}

			UpdateAvatarPreview(imageBytes);

			var uploadPayload = new EditCharacterAvatarUploadPayload
			{
				image = dataUrl,
				filename = Path.GetFileName(selectedPath)
			};

			SendRequest(EditCharacterRequests.UploadAvatar, uploadPayload);
		}

		private IEnumerator OpenImagePickerCoroutine(Action<string> onPicked)
		{
			var selectedPath = string.Empty;

#if UNITY_EDITOR
			selectedPath = UnityEditor.EditorUtility.OpenFilePanel("Chọn avatar", string.Empty, "png,jpg,jpeg,webp");
			onPicked?.Invoke(selectedPath);
			yield break;
#elif UNITY_STANDALONE_WIN
			selectedPath = FileBrowserUtils.OpenWindowsFileExplorer("Chọn avatar");
			onPicked?.Invoke(selectedPath);
			yield break;
#else
			var fileBrowserType = FileBrowserUtils.FindSimpleFileBrowserType();
			if (fileBrowserType != null)
			{
				yield return FileBrowserUtils.OpenViaSimpleFileBrowserCoroutine(fileBrowserType, "Chọn avatar", "Chọn", path => selectedPath = path);
				onPicked?.Invoke(selectedPath);
				yield break;
			}

			EventBus.Publish(EditCharacterEvents.AvatarUploadFailed,
				"Thiết bị chưa có file picker runtime. Vui lòng bật package SimpleFileBrowser cho Android/iOS/PC.");
			onPicked?.Invoke(string.Empty);
#endif
		}

		private void UpdateAvatarPreview(byte[] bytes)
		{
			if (_avatarPreviewImage == null)
			{
				return;
			}

			var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
			if (!texture.LoadImage(bytes))
			{
				Destroy(texture);
				return;
			}

			var sprite = Sprite.Create(
				texture,
				new Rect(0, 0, texture.width, texture.height),
				new Vector2(0.5f, 0.5f));

			_avatarPreviewImage.sprite = sprite;
			_avatarPreviewImage.preserveAspect = true;
		}

		private void UpdateAvatarPreviewFromSprite(Sprite sprite)
		{
			if (_avatarPreviewImage == null || sprite == null)
			{
				return;
			}

			_avatarPreviewImage.sprite = sprite;
			_avatarPreviewImage.preserveAspect = true;
		}

		[OnEvent(EditCharacterEvents.AvatarUploadSucceeded)]
		private void OnAvatarUploadSucceeded(object payload)
		{
			ResetUploadingState();

			var avatarUrl = CharacterFormUtils.ToNullableString(payload != null ? payload.ToString() : string.Empty);
			if (!string.IsNullOrWhiteSpace(avatarUrl))
			{
				_uploadedAvatarUrl = avatarUrl;
			}
		}

		[OnEvent(EditCharacterEvents.AvatarUploadFailed)]
		private void OnAvatarUploadFailed(object payload)
		{
			ResetUploadingState();
			Debug.LogError($"[EditCharacterView] Avatar upload failed: {payload}");
		}

		private void ResetUploadingState()
		{
			_isUploadingAvatar = false;
			if (_avatarUploadButton != null)
			{
				_avatarUploadButton.interactable = true;
			}
		}

		private string GetSelectedGender()
		{
			if (_genderToggleGroup == null)
			{
				return null;
			}

			foreach (var toggle in _genderToggleGroup.ActiveToggles())
			{
				if (!toggle.isOn)
				{
					continue;
				}

				var value = CharacterFormUtils.NormalizeGender(toggle.name);
				if (!string.IsNullOrWhiteSpace(value))
				{
					return value;
				}

				var label = toggle.GetComponentInChildren<TMP_Text>();
				if (label != null)
				{
					value = CharacterFormUtils.NormalizeGender(label.text);
					if (!string.IsNullOrWhiteSpace(value))
					{
						return value;
					}
				}
			}

			return null;
		}

		private int? ParseAge()
		{
			var raw = _ageInput != null ? _ageInput.text : string.Empty;
			if (!int.TryParse(raw, out var age))
			{
				return null;
			}

			if (age < 0 || age > 150)
			{
				return null;
			}

			return age;
		}

		private bool TryResolveSelectedVoice(out string voiceModel, out string voiceName)
		{
			voiceModel = null;
			voiceName = null;

			if (_voiceDropdown == null || _voiceDropdown.options == null || _voiceDropdown.options.Count == 0)
			{
				return false;
			}

			var index = Mathf.Clamp(_voiceDropdown.value, 0, _voiceDropdown.options.Count - 1);
			var text = (_voiceDropdown.options[index]?.text ?? string.Empty).Trim();
			if (string.IsNullOrWhiteSpace(text))
			{
				return false;
			}

			if (text.StartsWith("option", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			var separatorIndex = text.IndexOf(" - ", StringComparison.Ordinal);
			if (separatorIndex <= 0 || separatorIndex >= text.Length - 3)
			{
				return false;
			}

			voiceModel = text.Substring(0, separatorIndex).Trim().ToLowerInvariant();
			voiceName = text.Substring(separatorIndex + 3).Trim();

			if (string.IsNullOrWhiteSpace(voiceModel) || string.IsNullOrWhiteSpace(voiceName))
			{
				voiceModel = null;
				voiceName = null;
				return false;
			}

			return true;
		}

		[OnEvent(EditCharacterEvents.VoicesLoaded)]
		private void OnVoicesLoaded(object payload)
		{
			if (_voiceDropdown == null)
			{
				return;
			}

			var voices = payload as System.Collections.Generic.List<EditVoiceOptionData>;
			_voiceDropdown.options.Clear();

			if (voices != null)
			{
				for (var i = 0; i < voices.Count; i++)
				{
					var item = voices[i];
					if (item == null || string.IsNullOrWhiteSpace(item.model) || string.IsNullOrWhiteSpace(item.voice))
					{
						continue;
					}

					_voiceDropdown.options.Add(new TMP_Dropdown.OptionData($"{item.model} - {item.voice}"));
				}
			}

			if (_voiceDropdown.options.Count == 0)
			{
				_voiceDropdown.options.Add(new TMP_Dropdown.OptionData("openai - alloy"));
			}

			TrySelectPendingVoiceOption();
			_voiceDropdown.RefreshShownValue();
		}

		private void TrySelectPendingVoiceOption()
		{
			if (_voiceDropdown == null || _voiceDropdown.options == null || _voiceDropdown.options.Count == 0)
			{
				return;
			}

			var targetModel = (_pendingVoiceModel ?? string.Empty).Trim();
			var targetVoice = (_pendingVoiceName ?? string.Empty).Trim();
			if (string.IsNullOrWhiteSpace(targetVoice))
			{
				_voiceDropdown.value = 0;
				return;
			}

			var bestMatchIndex = -1;
			for (var i = 0; i < _voiceDropdown.options.Count; i++)
			{
				var optionText = (_voiceDropdown.options[i]?.text ?? string.Empty).Trim();
				var separatorIndex = optionText.IndexOf(" - ", StringComparison.Ordinal);
				if (separatorIndex <= 0 || separatorIndex >= optionText.Length - 3)
				{
					continue;
				}

				var optionModel = optionText.Substring(0, separatorIndex).Trim();
				var optionVoice = optionText.Substring(separatorIndex + 3).Trim();

				if (!string.Equals(optionVoice, targetVoice, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				if (bestMatchIndex < 0)
				{
					bestMatchIndex = i;
				}

				if (!string.IsNullOrWhiteSpace(targetModel)
					&& string.Equals(optionModel, targetModel, StringComparison.OrdinalIgnoreCase))
				{
					bestMatchIndex = i;
					break;
				}
			}

			_voiceDropdown.value = bestMatchIndex >= 0 ? bestMatchIndex : 0;
		}

		public void UpdatePitchNumberText()
		{
			if (_pitchNumberText != null && _pitchSlider != null)
			{
				_pitchNumberText.text = "Pitch: " + _pitchSlider.value.ToString("0");
			}
		}
	}
}
