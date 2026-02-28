using System.Collections.Generic;
using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.Story.Events;
using Features.GamePlay.SubFeatures.Story.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Story.Model;
using Features.GamePlay.SubFeatures.Story.Requests;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Features.GamePlay.SubFeatures.Story.View
{
	/// <summary>
	/// View for Story.
	/// </summary>
	public sealed class StoryView : BaseView
	{
		private const string CreateTitleText = "Tạo câu chuyện mới";
		private const string EditTitleText = "Chỉnh sửa câu chuyện";
		private const string CreateButtonText = "Tạo câu chuyện";
		private const string EditButtonText = "Chỉnh sửa câu chuyện";

		[Header("Tab tạo hoặc chỉnh sửa story")]
		[SerializeField]
		private TextMeshProUGUI _titleText;
		[SerializeField]
		private TMP_InputField _inputNameStory;
		[SerializeField]
		private TMP_InputField _inputContentStory;
		[SerializeField]
		private TMP_InputField _inputProcessStory;
		[SerializeField]
		private Button _buttonCreateStory;

		[SerializeField]
		private TMP_Text _buttonCreateStoryLabel;
		

		[Header("Tab xem story đã tạo")]
		[SerializeField]
		private GameObject _storyListContainer;
		[SerializeField]
		private StoryItemView _storyItemPrefab;
		private readonly List<StoryItemView> _spawnedItems = new List<StoryItemView>();
		private int? _editingStoryId;


		/// <summary>
		/// Shows this subfeature view when its controller is installed.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(StoryEvents.Installed)]
		private void OnInstalled(object payload)
		{
			gameObject.SetActive(true);
			EnsureBindings();
			BindCreateButton();
			SetCreateMode();
			SendRequest(StoryRequests.LoadStories);
		}

		/// <summary>
		/// Hides this subfeature view when its controller is uninstalled.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(StoryEvents.Uninstalled)]
		private void OnUninstalled(object payload)
		{
			ClearStoryItems();
			gameObject.SetActive(false);
		}

		/// <summary>
		/// Renders story list from controller.
		/// </summary>
		/// <param name="payload">Story list payload.</param>
		[OnEvent(StoryEvents.StoriesLoaded)]
		private void OnStoriesLoaded(object payload)
		{
			if (payload is not StoryListResponsePayload response)
			{
				return;
			}

			EnsureBindings();
			RenderStoryList(response.Stories);
		}

		/// <summary>
		/// Loads a story into the edit UI.
		/// </summary>
		/// <param name="payload">Edit payload.</param>
		[OnEvent(StoryEvents.StoryEditLoaded)]
		private void OnStoryEditLoaded(object payload)
		{
			if (payload is not StoryEditPayload editPayload)
			{
				return;
			}

			EnsureBindings();
			SetEditMode(editPayload);
		}

		/// <summary>
		/// Resets UI after story is saved.
		/// </summary>
		/// <param name="payload">Saved payload.</param>
		[OnEvent(StoryEvents.StorySaved)]
		private void OnStorySaved(object payload)
		{
			EnsureBindings();
			SetCreateMode();
		}

		/// <summary>
		/// Logs API failures for troubleshooting.
		/// </summary>
		/// <param name="payload">Error payload from controller.</param>
		[OnEvent(StoryEvents.RequestFailed)]
		private void OnRequestFailed(object payload)
		{
			var message = (payload as StoryErrorPayload)?.Message;
			if (string.IsNullOrWhiteSpace(message))
			{
				message = "Story request failed.";
			}

			Debug.LogError("[StoryView] " + message, this);
		}

		private void EnsureBindings()
		{
			if (_buttonCreateStoryLabel == null && _buttonCreateStory != null)
			{
				_buttonCreateStoryLabel = _buttonCreateStory.GetComponentInChildren<TMP_Text>(true);
			}
		}

		private void BindCreateButton()
		{
			if (_buttonCreateStory == null)
			{
				return;
			}

			_buttonCreateStory.onClick.RemoveAllListeners();
			_buttonCreateStory.onClick.AddListener(HandleCreateOrUpdateClicked);
		}

		private void HandleCreateOrUpdateClicked()
		{
			if (_editingStoryId.HasValue)
			{
				SendRequest(StoryRequests.UpdateStory, new StoryUpdateRequestPayload
				{
					StoryId = _editingStoryId.Value,
					Name = _inputNameStory != null ? _inputNameStory.text : string.Empty,
					Description = _inputContentStory != null ? _inputContentStory.text : string.Empty,
					CurrentProgress = _inputProcessStory != null ? _inputProcessStory.text : string.Empty
				});
			}
			else
			{
				SendRequest(StoryRequests.CreateStory, new StoryCreateRequestPayload
				{
					Name = _inputNameStory != null ? _inputNameStory.text : string.Empty,
					Description = _inputContentStory != null ? _inputContentStory.text : string.Empty,
					CurrentProgress = _inputProcessStory != null ? _inputProcessStory.text : string.Empty
				});
			}
		}

		private void RenderStoryList(List<StoryPayload> stories)
		{
			ClearStoryItems();

			if (_storyListContainer == null || _storyItemPrefab == null)
			{
				return;
			}

			_storyItemPrefab.gameObject.SetActive(false);
			if (stories == null || stories.Count == 0)
			{
				return;
			}

			for (var i = 0; i < stories.Count; i++)
			{
				var story = stories[i];
				if (story == null)
				{
					continue;
				}

				var instance = Instantiate(_storyItemPrefab, _storyListContainer.transform);
				instance.name = "StoryItem-" + story.Id;
				instance.gameObject.SetActive(true);
				instance.Bind(story.Id, story.Name, story.Description, story.CurrentProgress);
				instance.EditRequested += HandleStoryEditRequested;
				_spawnedItems.Add(instance);
			}
		}

		private void ClearStoryItems()
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

		private void HandleStoryEditRequested(StoryItemView item)
		{
			if (item == null || item.StoryId <= 0)
			{
				return;
			}

			SendRequest(StoryRequests.EditStory, new StoryEditRequestPayload
			{
				StoryId = item.StoryId
			});
		}

		private void SetCreateMode()
		{
			_editingStoryId = null;
			SetTitleAndButton(CreateTitleText, CreateButtonText);

			if (_inputNameStory != null)
			{
				_inputNameStory.text = string.Empty;
			}
			if (_inputContentStory != null)
			{
				_inputContentStory.text = string.Empty;
			}
			if (_inputProcessStory != null)
			{
				_inputProcessStory.text = string.Empty;
			}
		}

		private void SetEditMode(StoryEditPayload payload)
		{
			_editingStoryId = payload.StoryId;
			SetTitleAndButton(EditTitleText, EditButtonText);

			if (_inputNameStory != null)
			{
				_inputNameStory.text = payload.Name ?? string.Empty;
			}
			if (_inputContentStory != null)
			{
				_inputContentStory.text = payload.Description ?? string.Empty;
			}
			if (_inputProcessStory != null)
			{
				_inputProcessStory.text = payload.CurrentProgress ?? string.Empty;
			}
		}

		private void SetTitleAndButton(string title, string buttonLabel)
		{
			if (_titleText != null)
			{
				_titleText.text = title ?? string.Empty;
			}

			if (_buttonCreateStoryLabel != null)
			{
				_buttonCreateStoryLabel.text = buttonLabel ?? string.Empty;
			}
		}
	}
}
