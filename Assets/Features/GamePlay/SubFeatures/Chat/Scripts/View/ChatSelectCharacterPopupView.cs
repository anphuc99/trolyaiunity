using System.Collections.Generic;
using DG.Tweening;
using Features.GamePlay.SubFeatures.Chat.Model;
using UnityEngine;
using UnityEngine.UI;

namespace Features.GamePlay.SubFeatures.Chat.View
{
	/// <summary>
	/// Manages add-character popup UI in chat feature.
	/// </summary>
	public sealed class ChatSelectCharacterPopupView : MonoBehaviour
	{
		[SerializeField]
		private RectTransform _popupRoot;

		[SerializeField]
		private RectTransform _itemsContainer;

		[SerializeField]
		private Button _closeButton;

		[SerializeField]
		private float _openedHeight = 1450f;

		[SerializeField]
		private float _animationDuration = 0.25f;

		private readonly List<ChatSelectCharacterPopupItemView> _spawnedItems = new List<ChatSelectCharacterPopupItemView>();
		private ChatSelectCharacterPopupItemView _itemTemplate;
		private Tween _heightTween;

		private void Awake()
		{
			EnsureReferences();
			BindCloseButton();
			PrepareItemTemplate();
			HideImmediate();
		}

		private void OnDestroy()
		{
			if (_heightTween != null)
			{
				_heightTween.Kill();
				_heightTween = null;
			}

			if (_closeButton != null)
			{
				_closeButton.onClick.RemoveListener(Hide);
			}
		}

		/// <summary>
		/// Shows popup and renders character list.
		/// </summary>
		/// <param name="characters">Character list to display.</param>
		public void Show(List<ChatSelectableCharacterPayload> characters)
		{
			EnsureReferences();
			PrepareItemTemplate();

			RenderItems(characters);

			if (!gameObject.activeSelf)
			{
				gameObject.SetActive(true);
			}

			AnimateHeight(_openedHeight);
		}

		/// <summary>
		/// Hides popup with close animation.
		/// </summary>
		public void Hide()
		{
			AnimateHeight(0f, () => gameObject.SetActive(false));
		}

		/// <summary>
		/// Hides popup immediately without animation.
		/// </summary>
		public void HideImmediate()
		{
			EnsureReferences();

			if (_heightTween != null)
			{
				_heightTween.Kill();
				_heightTween = null;
			}

			if (_popupRoot != null)
			{
				var size = _popupRoot.sizeDelta;
				size.y = 0f;
				_popupRoot.sizeDelta = size;
			}

			if (gameObject.activeSelf)
			{
				gameObject.SetActive(false);
			}
		}

		private void BindCloseButton()
		{
			if (_closeButton == null)
			{
				return;
			}

			_closeButton.onClick.RemoveListener(Hide);
			_closeButton.onClick.AddListener(Hide);
		}

		private void RenderItems(List<ChatSelectableCharacterPayload> characters)
		{
			ClearSpawnedItems();

			if (_itemTemplate == null || characters == null || characters.Count == 0)
			{
				return;
			}

			for (var i = 0; i < characters.Count; i++)
			{
				var character = characters[i];
				if (character == null)
				{
					continue;
				}

				var item = Instantiate(_itemTemplate, _itemsContainer);
				item.gameObject.SetActive(true);
				item.Bind(character);
				_spawnedItems.Add(item);
			}
		}

		private void ClearSpawnedItems()
		{
			for (var i = 0; i < _spawnedItems.Count; i++)
			{
				var item = _spawnedItems[i];
				if (item != null)
				{
					Destroy(item.gameObject);
				}
			}

			_spawnedItems.Clear();
		}

		private void AnimateHeight(float targetHeight, TweenCallback onComplete = null)
		{
			if (_popupRoot == null)
			{
				onComplete?.Invoke();
				return;
			}

			if (_heightTween != null)
			{
				_heightTween.Kill();
				_heightTween = null;
			}

			var currentSize = _popupRoot.sizeDelta;
			var endSize = new Vector2(currentSize.x, targetHeight);
			_heightTween = DOTween
				.To(() => _popupRoot.sizeDelta, value => _popupRoot.sizeDelta = value, endSize, _animationDuration)
				.SetEase(Ease.OutCubic)
				.OnComplete(onComplete);
		}

		private void PrepareItemTemplate()
		{
			if (_itemsContainer == null)
			{
				_itemTemplate = null;
				return;
			}

			if (_itemTemplate != null)
			{
				_itemTemplate.gameObject.SetActive(false);
				return;
			}

			for (var i = 0; i < _itemsContainer.childCount; i++)
			{
				var child = _itemsContainer.GetChild(i);
				if (child == null)
				{
					continue;
				}

				var itemView = child.GetComponent<ChatSelectCharacterPopupItemView>();
				if (itemView == null)
				{
					itemView = child.gameObject.AddComponent<ChatSelectCharacterPopupItemView>();
				}

				_itemTemplate = itemView;
				_itemTemplate.gameObject.SetActive(false);
				break;
			}
		}

		private void EnsureReferences()
		{
			if (_popupRoot == null)
			{
				_popupRoot = transform as RectTransform;
			}

			if (_itemsContainer == null)
			{
				var items = transform.Find("Scroll View/Viewport/GrirdCharacterItem");
				if (items != null)
				{
					_itemsContainer = items as RectTransform;
				}
			}

			if (_closeButton == null)
			{
				var close = transform.Find("btnClose");
				if (close != null)
				{
					_closeButton = close.GetComponent<Button>();
				}
			}
		}

		private void OnValidate()
		{
			EnsureReferences();
		}
	}
}