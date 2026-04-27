using System;
using System.Collections;
using System.IO;
using Core.Infrastructure.Events;
using Core.Infrastructure.Views;
using Features.CreateCharater.Events;
using Features.CreateCharater.Infrastructure.Attributes;
using Features.CreateCharater.Model;
using Features.CreateCharater.Requests;
using Share.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Features.CreateCharater.View
{
	/// <summary>
	/// View for CreateCharater.
	/// Handles user input and submits a server-compatible character payload.
	/// </summary>
	public sealed class CreateCharaterView : BaseView
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
		private bool _isUploadingAvatar;
		private string _uploadedAvatarUrl;

		[Header("Show pitch number")]
		[SerializeField] private TMP_Text _pitchNumberText;

		/// <summary>
		/// Called after this view is enabled and scope is active.
		/// </summary>
		protected override void OnEnabled()
		{
			TryAutoBindOptionalControls();
			UpdateBackButtonVisibility();
			SendRequest(CreateCharaterRequests.FetchVoices);

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
				_closeButton.onClick.RemoveListener(OnBackButtonClicked);
				_closeButton.onClick.AddListener(OnBackButtonClicked);
			}
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
				_closeButton.onClick.RemoveListener(OnBackButtonClicked);
			}
		}

		private void UpdateBackButtonVisibility()
		{
			if (_closeButton == null)
			{
				return;
			}

			var gameplaySceneName = Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay.ToString();
			var gameplayScene = SceneManager.GetSceneByName(gameplaySceneName);
			_closeButton.gameObject.SetActive(gameplayScene.IsValid() && gameplayScene.isLoaded);
		}

		private void OnBackButtonClicked()
		{
			SendRequest(CreateCharaterRequests.CloseScope);
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

		private static string ToNullableString(string value)
		{
			return CharacterFormUtils.ToNullableString(value);
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

			if (_avatarUploadButton == null && _avatarPreviewImage != null)
			{
				_avatarUploadButton = _avatarPreviewImage.GetComponent<Button>();
				if (_avatarUploadButton == null)
				{
					_avatarUploadButton = _avatarPreviewImage.gameObject.AddComponent<Button>();
				}
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

		private void OnSubmitClicked()
		{
			if (_isUploadingAvatar)
			{
				EventBus.Publish(CreateCharaterEvents.CharacterCreationFailed, "Đang upload avatar, vui lòng đợi hoàn tất.");
				return;
			}

			var name = ToNullableString(_nameInput != null ? _nameInput.text : string.Empty);
			var personality = ToNullableString(_descriptionInput != null ? _descriptionInput.text : string.Empty);
			var gender = GetSelectedGender();

			if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(personality) || string.IsNullOrWhiteSpace(gender))
			{
				EventBus.Publish(CreateCharaterEvents.CharacterCreationFailed, "Vui lòng nhập tên, mô tả và giới tính hợp lệ.");
				return;
			}

			TryResolveSelectedVoice(out var voiceModel, out var voiceName);
			var avatar = ToNullableString(_uploadedAvatarUrl);

			var payload = new CreateCharacterPayload
			{
				name = name,
				age = ParseAge(),
				gender = gender,
				personality = personality,
				appearance = null,
				avatar = avatar,
				voiceModel = voiceModel,
				voiceName = voiceName,
				pitch = _pitchSlider != null ? _pitchSlider.value : null,
				speakingRate = _speakingRateSlider != null ? _speakingRateSlider.value : null
			};

			SendRequest(CreateCharaterRequests.SubmitCharacter, payload);
		}

		[OnEvent(CreateCharaterEvents.VoicesLoaded)]
		private void OnVoicesLoaded(object payload)
		{
			if (_voiceDropdown == null)
			{
				return;
			}

			var voices = payload as System.Collections.Generic.List<VoiceOptionData>;
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

			_voiceDropdown.value = 0;
			_voiceDropdown.RefreshShownValue();
		}

		private void OnAvatarUploadClicked()
		{
			if (_isUploadingAvatar)
			{
				return;
			}

			if (_avatarUploadButton == null)
			{
				EventBus.Publish(CreateCharaterEvents.AvatarUploadFailed, "Không tìm thấy nút upload avatar trong UI.");
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
				_isUploadingAvatar = false;
				if (_avatarUploadButton != null)
				{
					_avatarUploadButton.interactable = true;
				}
				yield break;
			}

			byte[] imageBytes;
			try
			{
				imageBytes = File.ReadAllBytes(selectedPath);
			}
			catch (Exception ex)
			{
				_isUploadingAvatar = false;
				if (_avatarUploadButton != null)
				{
					_avatarUploadButton.interactable = true;
				}
				EventBus.Publish(CreateCharaterEvents.AvatarUploadFailed, $"Không thể đọc ảnh: {ex.Message}");
				yield break;
			}

			if (imageBytes == null || imageBytes.Length == 0)
			{
				_isUploadingAvatar = false;
				if (_avatarUploadButton != null)
				{
					_avatarUploadButton.interactable = true;
				}
				EventBus.Publish(CreateCharaterEvents.AvatarUploadFailed, "Ảnh avatar không hợp lệ.");
				yield break;
			}

			imageBytes = Share.Utils.ImageCompressionUtils.CompressImageUnderSize(imageBytes, 1048576, out var changedToJpeg);
			if (changedToJpeg)
			{
				selectedPath = Path.ChangeExtension(selectedPath, ".jpg");
			}

			var dataUrl = DataUrlUtils.BuildImageDataUrl(selectedPath, imageBytes);
			if (string.IsNullOrWhiteSpace(dataUrl))
			{
				_isUploadingAvatar = false;
				if (_avatarUploadButton != null)
				{
					_avatarUploadButton.interactable = true;
				}
				EventBus.Publish(CreateCharaterEvents.AvatarUploadFailed, "Định dạng ảnh chưa được hỗ trợ. Dùng PNG, JPG hoặc WEBP.");
				yield break;
			}

			UpdateAvatarPreview(imageBytes);

			var uploadPayload = new AvatarUploadPayload
			{
				image = dataUrl,
				filename = Path.GetFileName(selectedPath)
			};

			SendRequest(CreateCharaterRequests.UploadAvatar, uploadPayload);
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

			EventBus.Publish(CreateCharaterEvents.AvatarUploadFailed,
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

		[OnEvent(CreateCharaterEvents.AvatarUploadSucceeded)]
		private void OnAvatarUploadSucceeded(object payload)
		{
			_isUploadingAvatar = false;
			if (_avatarUploadButton != null)
			{
				_avatarUploadButton.interactable = true;
			}

			var avatarUrl = ToNullableString(payload != null ? payload.ToString() : string.Empty);
			if (!string.IsNullOrWhiteSpace(avatarUrl))
			{
				_uploadedAvatarUrl = avatarUrl;
			}

			Debug.Log("[CreateCharaterView] Avatar uploaded successfully.");
		}

		[OnEvent(CreateCharaterEvents.AvatarUploadFailed)]
		private void OnAvatarUploadFailed(object payload)
		{
			_isUploadingAvatar = false;
			if (_avatarUploadButton != null)
			{
				_avatarUploadButton.interactable = true;
			}
			Debug.LogError($"[CreateCharaterView] Avatar upload failed: {payload}");
		}

		[OnEvent(CreateCharaterEvents.CharacterCreationSucceeded)]
		private void OnCreationSucceeded(object payload)
		{
			Debug.Log("[CreateCharaterView] Character created successfully!");
			// Potentially move to another screen or show success feedback
		}

		[OnEvent(CreateCharaterEvents.CharacterCreationFailed)]
		private void OnCreationFailed(object payload)
		{
			Debug.LogError($"[CreateCharaterView] Character creation failed: {payload}");
		}

		public void UpdatePitchNumberText()
		{
			if (_pitchNumberText != null)
			{
				_pitchNumberText.text = "Pitch: " + _pitchSlider.value.ToString("0");
			}
		}
	}
}
