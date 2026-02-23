using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Core.Infrastructure.Events;
using Core.Infrastructure.Views;
using Features.CreateCharater.Events;
using Features.CreateCharater.Infrastructure.Attributes;
using Features.CreateCharater.Model;
using Features.CreateCharater.Requests;
using TMPro;
using UnityEngine;
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

			if (_submitButton != null)
			{
				_submitButton.onClick.AddListener(OnSubmitClicked);
			}

			if (_avatarUploadButton != null)
			{
				_avatarUploadButton.onClick.RemoveListener(OnAvatarUploadClicked);
				_avatarUploadButton.onClick.AddListener(OnAvatarUploadClicked);
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

				var value = NormalizeGender(toggle.name);
				if (!string.IsNullOrWhiteSpace(value))
				{
					return value;
				}

				var label = toggle.GetComponentInChildren<TMP_Text>();
				if (label != null)
				{
					value = NormalizeGender(label.text);
					if (!string.IsNullOrWhiteSpace(value))
					{
						return value;
					}
				}
			}

			return null;
		}

		private static string NormalizeGender(string raw)
		{
			var normalized = (raw ?? string.Empty).Trim().ToLowerInvariant();

			if (normalized.Contains("female") || normalized.Contains("nữ") || normalized == "nu")
			{
				return "female";
			}

			if (normalized.Contains("male") || normalized.Contains("nam"))
			{
				return "male";
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

		private string ResolveVoiceName()
		{
			if (_voiceDropdown == null || _voiceDropdown.options == null || _voiceDropdown.options.Count == 0)
			{
				return null;
			}

			var index = Mathf.Clamp(_voiceDropdown.value, 0, _voiceDropdown.options.Count - 1);
			var text = (_voiceDropdown.options[index]?.text ?? string.Empty).Trim();
			if (string.IsNullOrWhiteSpace(text))
			{
				return null;
			}

			if (text.StartsWith("option", StringComparison.OrdinalIgnoreCase))
			{
				return null;
			}

			return text;
		}

		private static string ToNullableString(string value)
		{
			var trimmed = (value ?? string.Empty).Trim();
			return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
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

			var voiceName = ResolveVoiceName();
			var avatar = ToNullableString(_uploadedAvatarUrl);

			var payload = new CreateCharacterPayload
			{
				name = name,
				age = ParseAge(),
				gender = gender,
				personality = personality,
				appearance = null,
				avatar = avatar,
				voiceModel = string.IsNullOrWhiteSpace(voiceName) ? null : "openai",
				voiceName = voiceName,
				pitch = _pitchSlider != null ? _pitchSlider.value : null,
				speakingRate = _speakingRateSlider != null ? _speakingRateSlider.value : null
			};

			SendRequest(CreateCharaterRequests.SubmitCharacter, payload);
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

			var dataUrl = BuildDataUrl(selectedPath, imageBytes);
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

			var fileBrowserType = FindSimpleFileBrowserType();
			if (fileBrowserType != null)
			{
				yield return OpenViaSimpleFileBrowserCoroutine(fileBrowserType, path => selectedPath = path);
				onPicked?.Invoke(selectedPath);
				yield break;
			}

#if UNITY_EDITOR
			selectedPath = UnityEditor.EditorUtility.OpenFilePanel("Chọn avatar", string.Empty, "png,jpg,jpeg,webp");
			onPicked?.Invoke(selectedPath);
#else
			EventBus.Publish(CreateCharaterEvents.AvatarUploadFailed,
				"Thiết bị chưa có file picker runtime. Vui lòng bật package SimpleFileBrowser cho Android/iOS/PC.");
			onPicked?.Invoke(string.Empty);
#endif
		}

		private IEnumerator OpenViaSimpleFileBrowserCoroutine(Type fileBrowserType, Action<string> onPicked)
		{
			var pickModeType = fileBrowserType.GetNestedType("PickMode", BindingFlags.Public);
			if (pickModeType == null)
			{
				onPicked?.Invoke(string.Empty);
				yield break;
			}

			var waitMethod = fileBrowserType.GetMethod(
				"WaitForLoadDialog",
				BindingFlags.Public | BindingFlags.Static,
				null,
				new[] { pickModeType, typeof(bool), typeof(string), typeof(string), typeof(string), typeof(string) },
				null);

			if (waitMethod == null)
			{
				onPicked?.Invoke(string.Empty);
				yield break;
			}

			var pickMode = Enum.Parse(pickModeType, "Files");
			var routine = waitMethod.Invoke(null, new object[] { pickMode, false, null, null, "Chọn avatar", "Chọn" }) as IEnumerator;
			if (routine != null)
			{
				yield return routine;
			}

			var successProp = fileBrowserType.GetProperty("Success", BindingFlags.Public | BindingFlags.Static);
			var resultProp = fileBrowserType.GetProperty("Result", BindingFlags.Public | BindingFlags.Static);

			var isSuccess = successProp != null && successProp.GetValue(null) is bool value && value;
			if (!isSuccess)
			{
				onPicked?.Invoke(string.Empty);
				yield break;
			}

			var result = resultProp != null ? resultProp.GetValue(null) as string[] : null;
			onPicked?.Invoke(result != null && result.Length > 0 ? result[0] : string.Empty);
		}

		private static Type FindSimpleFileBrowserType()
		{
			var assemblies = AppDomain.CurrentDomain.GetAssemblies();
			for (var i = 0; i < assemblies.Length; i += 1)
			{
				var assembly = assemblies[i];
				if (assembly == null)
				{
					continue;
				}

				var type = assembly.GetType("SimpleFileBrowser.FileBrowser", false);
				if (type != null)
				{
					return type;
				}
			}

			return null;
		}

		private static string BuildDataUrl(string filePath, byte[] bytes)
		{
			var extension = Path.GetExtension(filePath)?.ToLowerInvariant();
			var mime = extension == ".png"
				? "image/png"
				: extension == ".webp"
					? "image/webp"
					: extension == ".jpg" || extension == ".jpeg"
						? "image/jpeg"
						: null;

			if (string.IsNullOrWhiteSpace(mime))
			{
				return null;
			}

			return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
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
