using System.Collections.Generic;
using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.LearningPath.Events;
using Features.GamePlay.SubFeatures.LearningPath.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.LearningPath.Model;
using Features.GamePlay.SubFeatures.LearningPath.Requests;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Features.GamePlay.SubFeatures.LearningPath.View
{
	/// <summary>
	/// View for LearningPath.
	/// Displays the create/edit form and the list of existing learning paths.
	/// </summary>
	public sealed class LearningPathView : BaseView
	{
		private const string CreateTitleText = "Tạo lộ trình mới";
		private const string EditTitleText = "Chỉnh sửa lộ trình";
		private const string CreateButtonText = "Tạo lộ trình";
		private const string EditButtonText = "Cập nhật lộ trình";

		[Header("Tab tạo hoặc chỉnh sửa lộ trình")]
		[SerializeField] private TextMeshProUGUI _titleText;
		[SerializeField] private TMP_InputField _inputContext;
		[SerializeField] private TMP_InputField _inputVocabulary;
		[SerializeField] private Button _buttonSubmit;
		[SerializeField] private TMP_Text _buttonSubmitLabel;

		[Header("Tab xem lộ trình đã tạo")]
		[SerializeField] private LearningPathItemView _itemPrefab;
		[SerializeField] private Transform _learningPathListRoot;

		private readonly List<LearningPathItemView> _spawnedItems = new List<LearningPathItemView>();
		private int? _editingId;

		/// <summary>
		/// Shows this subfeature view when its controller is installed.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(LearningPathEvents.Installed)]
		private void OnInstalled(object payload)
		{
			gameObject.SetActive(true);
			EnsureBindings();
			BindSubmitButton();
			SetCreateMode();
			SendRequest(LearningPathRequests.LoadList);
		}

		/// <summary>
		/// Hides this subfeature view when its controller is uninstalled.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(LearningPathEvents.Uninstalled)]
		private void OnUninstalled(object payload)
		{
			ClearItems();
			gameObject.SetActive(false);
		}

		/// <summary>
		/// Renders learning path list from controller.
		/// </summary>
		/// <param name="payload">Learning path list payload.</param>
		[OnEvent(LearningPathEvents.ListLoaded)]
		private void OnListLoaded(object payload)
		{
			if (payload is not LearningPathListResponsePayload response)
			{
				return;
			}

			EnsureBindings();
			RenderList(response.LearningPaths);
		}

		/// <summary>
		/// Loads a learning path into the edit UI.
		/// </summary>
		/// <param name="payload">Edit payload.</param>
		[OnEvent(LearningPathEvents.EditLoaded)]
		private void OnEditLoaded(object payload)
		{
			if (payload is not LearningPathEditPayload editPayload)
			{
				return;
			}

			EnsureBindings();
			SetEditMode(editPayload);
		}

		/// <summary>
		/// Resets UI after learning path is saved.
		/// </summary>
		/// <param name="payload">Saved payload.</param>
		[OnEvent(LearningPathEvents.Saved)]
		private void OnSaved(object payload)
		{
			EnsureBindings();
			SetCreateMode();
		}

		/// <summary>
		/// Logs API failures for troubleshooting.
		/// </summary>
		/// <param name="payload">Error payload from controller.</param>
		[OnEvent(LearningPathEvents.RequestFailed)]
		private void OnRequestFailed(object payload)
		{
			var message = (payload as LearningPathErrorPayload)?.Message;
			if (string.IsNullOrWhiteSpace(message))
			{
				message = "Learning path request failed.";
			}

			Debug.LogError("[LearningPathView] " + message, this);
		}

		private void EnsureBindings()
		{
			if (_buttonSubmitLabel == null && _buttonSubmit != null)
			{
				_buttonSubmitLabel = _buttonSubmit.GetComponentInChildren<TMP_Text>(true);
			}
		}

		private void BindSubmitButton()
		{
			if (_buttonSubmit == null)
			{
				return;
			}

			_buttonSubmit.onClick.RemoveAllListeners();
			_buttonSubmit.onClick.AddListener(HandleSubmitClicked);
		}

		private void HandleSubmitClicked()
		{
			if (_editingId.HasValue)
			{
				SendRequest(LearningPathRequests.Update, new LearningPathUpdateRequestPayload
				{
					LearningPathId = _editingId.Value,
					Context = _inputContext != null ? _inputContext.text : string.Empty,
					Vocabulary = _inputVocabulary != null ? _inputVocabulary.text : string.Empty
				});
			}
			else
			{
				SendRequest(LearningPathRequests.Create, new LearningPathCreateRequestPayload
				{
					Context = _inputContext != null ? _inputContext.text : string.Empty,
					Vocabulary = _inputVocabulary != null ? _inputVocabulary.text : string.Empty
				});
			}
		}

		private void RenderList(List<LearningPathPayload> items)
		{
			ClearItems();

			if (_learningPathListRoot == null || _itemPrefab == null)
			{
				return;
			}

			_itemPrefab.gameObject.SetActive(false);
			if (items == null || items.Count == 0)
			{
				return;
			}

			for (var i = 0; i < items.Count; i++)
			{
				var item = items[i];
				if (item == null)
				{
					continue;
				}

				var instance = Instantiate(_itemPrefab, _learningPathListRoot);
				instance.name = "LearningPathItem-" + item.Id;
				instance.gameObject.SetActive(true);
				instance.Bind(item.Id, item.Context, item.Vocabulary);
				instance.EditRequested += HandleEditRequested;
				_spawnedItems.Add(instance);
			}
		}

		private void ClearItems()
		{
			for (var i = 0; i < _spawnedItems.Count; i++)
			{
				if (_spawnedItems[i] != null)
				{
					Destroy(_spawnedItems[i].gameObject);
				}
			}

			_spawnedItems.Clear();
		}

		private void HandleEditRequested(LearningPathItemView item)
		{
			if (item == null || item.LearningPathId <= 0)
			{
				return;
			}

			SendRequest(LearningPathRequests.Edit, new LearningPathEditRequestPayload
			{
				LearningPathId = item.LearningPathId
			});
		}

		private void SetCreateMode()
		{
			_editingId = null;
			SetTitleAndButton(CreateTitleText, CreateButtonText);

			if (_inputContext != null)
			{
				_inputContext.text = string.Empty;
			}

			if (_inputVocabulary != null)
			{
				_inputVocabulary.text = string.Empty;
			}
		}

		private void SetEditMode(LearningPathEditPayload payload)
		{
			_editingId = payload.LearningPathId;
			SetTitleAndButton(EditTitleText, EditButtonText);

			if (_inputContext != null)
			{
				_inputContext.text = payload.Context ?? string.Empty;
			}

			if (_inputVocabulary != null)
			{
				_inputVocabulary.text = payload.Vocabulary ?? string.Empty;
			}
		}

		private void SetTitleAndButton(string title, string buttonLabel)
		{
			if (_titleText != null)
			{
				_titleText.text = title ?? string.Empty;
			}

			if (_buttonSubmitLabel != null)
			{
				_buttonSubmitLabel.text = buttonLabel ?? string.Empty;
			}
		}
	}
}
