using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Features.GamePlay.SubFeatures.Chat.View
{
	/// <summary>
	/// Virtualized message container that supports drag and mouse-wheel scrolling without using Unity ScrollView.
	/// Only a small pool of message items is rendered and recycled while scrolling.
	/// </summary>
	public sealed class VirtualizedChatMessageContainer : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
	{
		[SerializeField]
		private RectTransform _viewport;

		[SerializeField]
		private RectTransform _itemsRoot;

		[SerializeField]
		private MessageBubble _characterMessagePrefab;

		[SerializeField]
		private MessageBubble _userMessagePrefab;

		[SerializeField]
		[Min(0)]
		private int _extraBufferedItems = 2;

		[SerializeField]
		[Min(0.01f)]
		private float _dragSensitivity = 1f;

		[SerializeField]
		[Min(1f)]
		private float _mouseWheelStep = 48f;

		[SerializeField]
		[Min(1)]
		private int _maxSpawnedObjects = 24;

		[Header("Cheat Settings")]
		[SerializeField]
		private MessageBubbleType _cheatBubbleType = MessageBubbleType.User;

		[SerializeField]
		private string _cheatSenderName = "Sender";

		[SerializeField]
		private Sprite _cheatAvatar;

		[SerializeField]
		[TextArea(1, 4)]
		private string _cheatMessageText = "Hello, this is a test message.";

		[SerializeField]
		private List<MessageBubbleData> _messages = new();

		private readonly List<MessageBubble> _characterPool = new();
		private readonly List<MessageBubble> _userPool = new();
		private readonly List<float> _messageHeights = new();
		private readonly List<float> _prefixHeights = new();

		private MessageBubble _characterMeasureBubble;
		private MessageBubble _userMeasureBubble;
		private float _scrollOffset;
		private bool _isDragging;
		private float _totalHeight;
		private int _cheatSequence;

		/// <summary>
		/// Returns current total message count.
		/// </summary>
		public int MessageCount => _messages.Count;

		/// <summary>
		/// Maximum pooled object count that can be spawned per bubble type.
		/// </summary>
		public int MaxSpawnedObjects => _maxSpawnedObjects;

		/// <summary>
		/// Adds one cheat message for quick testing from inspector button.
		/// </summary>
		public void CheatAddMessage()
		{
			_cheatSequence += 1;

			var payload = new MessageBubbleData
			{
				MessageId = "cheat-" + _cheatSequence,
				Type = _cheatBubbleType,
				SenderName = _cheatSenderName,
				Message = string.IsNullOrWhiteSpace(_cheatMessageText)
					? "Cheat message #" + _cheatSequence
					: _cheatMessageText,
				Avatar = _cheatAvatar,
			};

			AddNewMessage(payload);
		}

		private void Awake()
		{
			EnsureReferences();
			EnsureMeasureBubbles();
			RebuildMetrics();
			RefreshVisible();
		}

		private void OnEnable()
		{
			RefreshVisible();
		}

		private void OnValidate()
		{
			if (_dragSensitivity < 0.01f)
			{
				_dragSensitivity = 0.01f;
			}

			if (_mouseWheelStep < 1f)
			{
				_mouseWheelStep = 1f;
			}

			if (_maxSpawnedObjects < 1)
			{
				_maxSpawnedObjects = 1;
			}

			EnsureReferences();
			EnsureMeasureBubbles();
			RebuildMetrics();
			RefreshVisible();
		}

		/// <summary>
		/// Replaces all messages in container.
		/// </summary>
		/// <param name="messages">New message collection.</param>
		public void SetMessages(IReadOnlyList<MessageBubbleData> messages)
		{
			_messages.Clear();
			if (messages != null)
			{
				for (var i = 0; i < messages.Count; i++)
				{
					_messages.Add(messages[i] ?? new MessageBubbleData());
				}
			}

			_scrollOffset = 0f;
			EnsureMeasureBubbles();
			RebuildMetrics();
			RefreshVisible();
		}

		/// <summary>
		/// Appends one message at the end (newer message).
		/// </summary>
		/// <param name="message">Message text.</param>
		public void AddNewMessage(MessageBubbleData message)
		{
			_messages.Add(message ?? new MessageBubbleData());
			RebuildMetrics();
			RefreshVisible();
		}

		/// <summary>
		/// Inserts one message at the beginning (older message).
		/// </summary>
		/// <param name="message">Message text.</param>
		public void AddOldMessage(MessageBubbleData message)
		{
			_messages.Insert(0, message ?? new MessageBubbleData());
			RebuildMetrics();
			if (_messageHeights.Count > 0)
			{
				_scrollOffset += _messageHeights[0];
			}

			RefreshVisible();
		}

		/// <summary>
		/// Handles drag start state.
		/// </summary>
		/// <param name="eventData">Pointer event data.</param>
		public void OnBeginDrag(PointerEventData eventData)
		{
			_isDragging = true;
		}

		/// <summary>
		/// Handles drag scrolling.
		/// </summary>
		/// <param name="eventData">Pointer event data.</param>
		public void OnDrag(PointerEventData eventData)
		{
			if (!_isDragging)
			{
				return;
			}

			ScrollBy(eventData.delta.y * _dragSensitivity);
		}

		/// <summary>
		/// Handles drag end state.
		/// </summary>
		/// <param name="eventData">Pointer event data.</param>
		public void OnEndDrag(PointerEventData eventData)
		{
			_isDragging = false;
		}

		/// <summary>
		/// Handles mouse wheel scrolling.
		/// </summary>
		/// <param name="eventData">Pointer event data.</param>
		public void OnScroll(PointerEventData eventData)
		{
			ScrollBy(-eventData.scrollDelta.y * _mouseWheelStep);
		}

		/// <summary>
		/// Scrolls by pixel delta and refreshes visible messages.
		/// </summary>
		/// <param name="deltaPixels">Positive value scrolls down; negative value scrolls up.</param>
		public void ScrollBy(float deltaPixels)
		{
			_scrollOffset = Mathf.Clamp(_scrollOffset + deltaPixels, 0f, GetMaxScrollOffset());
			RefreshVisible();
		}

		private void EnsureReferences()
		{
			if (_viewport == null)
			{
				_viewport = transform as RectTransform;
			}

			if (_itemsRoot == null)
			{
				_itemsRoot = _viewport;
			}
		}

		private void EnsurePoolCapacity(int requiredCount)
		{
			if (_viewport == null || _itemsRoot == null || _characterMessagePrefab == null || _userMessagePrefab == null)
			{
				return;
			}

			var safeRequiredCount = Mathf.Clamp(requiredCount, 0, Mathf.Max(1, _maxSpawnedObjects));
			while (_characterPool.Count < safeRequiredCount)
			{
				_characterPool.Add(CreatePooledBubble(_characterMessagePrefab, "Character"));
				_userPool.Add(CreatePooledBubble(_userMessagePrefab, "User"));
			}
		}

		private int GetDesiredPoolCount()
		{
			if (_viewport == null)
			{
				return 0;
			}

			var viewportHeight = Mathf.Max(1f, _viewport.rect.height);
			var visibleCount = Mathf.CeilToInt(viewportHeight / 72f);
			var desiredCount = Mathf.Max(0, visibleCount + _extraBufferedItems);
			return Mathf.Min(Mathf.Max(1, _maxSpawnedObjects), desiredCount);
		}

		private float GetMaxScrollOffset()
		{
			if (_viewport == null)
			{
				return 0f;
			}

			var viewportHeight = Mathf.Max(1f, _viewport.rect.height);
			return Mathf.Max(0f, _totalHeight - viewportHeight);
		}

		private void RefreshVisible()
		{
			if (_viewport == null || _itemsRoot == null)
			{
				return;
			}

			if (_messages.Count == 0)
			{
				SetAllPooledInactive();
				return;
			}

			_scrollOffset = Mathf.Clamp(_scrollOffset, 0f, GetMaxScrollOffset());
			var startIndex = FindStartIndex(_scrollOffset);
			var requiredSlotCount = Mathf.Min(GetDesiredPoolCount(), Mathf.Max(0, _messages.Count - startIndex));
			EnsurePoolCapacity(requiredSlotCount);

			if (_characterPool.Count == 0 || _userPool.Count == 0)
			{
				return;
			}

			for (var slot = 0; slot < _characterPool.Count; slot++)
			{
				var dataIndex = startIndex + slot;
				var userBubble = _userPool[slot];
				var characterBubble = _characterPool[slot];

				if (dataIndex < 0 || dataIndex >= _messages.Count)
				{
					if (userBubble != null)
					{
						userBubble.gameObject.SetActive(false);
					}

					if (characterBubble != null)
					{
						characterBubble.gameObject.SetActive(false);
					}

					continue;
				}

				var data = _messages[dataIndex];
				var targetBubble = data.Type == MessageBubbleType.User ? userBubble : characterBubble;
				var otherBubble = data.Type == MessageBubbleType.User ? characterBubble : userBubble;

				if (otherBubble != null)
				{
					otherBubble.gameObject.SetActive(false);
				}

				if (targetBubble == null)
				{
					continue;
				}

				targetBubble.gameObject.SetActive(true);
				targetBubble.ApplyData(data, _viewport.rect.width);

				var bubbleRect = targetBubble.RootRect;
				if (bubbleRect == null)
				{
					continue;
				}

				var y = -(_prefixHeights[dataIndex] - _scrollOffset);
				bubbleRect.anchoredPosition = new Vector2(0f, y);
			}
		}

		private void SetAllPooledInactive()
		{
			for (var i = 0; i < _characterPool.Count; i++)
			{
				if (_characterPool[i] != null)
				{
					_characterPool[i].gameObject.SetActive(false);
				}
			}

			for (var i = 0; i < _userPool.Count; i++)
			{
				if (_userPool[i] != null)
				{
					_userPool[i].gameObject.SetActive(false);
				}
			}
		}

		private MessageBubble CreatePooledBubble(MessageBubble prefab, string suffix)
		{
			var bubble = Instantiate(prefab, _itemsRoot);
			bubble.name = prefab.name + "_Virtualized_" + suffix;

			bubble.gameObject.SetActive(false);
			return bubble;
		}

		private void EnsureMeasureBubbles()
		{
			if (_characterMessagePrefab == null || _userMessagePrefab == null || _itemsRoot == null)
			{
				return;
			}

			if (_characterMeasureBubble == null)
			{
				_characterMeasureBubble = Instantiate(_characterMessagePrefab, _itemsRoot);
				PrepareMeasureBubble(_characterMeasureBubble, "Character");
			}

			if (_userMeasureBubble == null)
			{
				_userMeasureBubble = Instantiate(_userMessagePrefab, _itemsRoot);
				PrepareMeasureBubble(_userMeasureBubble, "User");
			}
		}

		private void PrepareMeasureBubble(MessageBubble bubble, string suffix)
		{
			if (bubble == null)
			{
				return;
			}

			bubble.name = bubble.name + "_Measure_" + suffix;
			bubble.gameObject.SetActive(true);
			var rect = bubble.RootRect;
			if (rect != null)
			{
				rect.anchoredPosition = new Vector2(-100000f, -100000f);
			}

			var canvasGroup = bubble.GetComponent<CanvasGroup>();
			if (canvasGroup == null)
			{
				canvasGroup = bubble.gameObject.AddComponent<CanvasGroup>();
			}

			canvasGroup.alpha = 0f;
			canvasGroup.interactable = false;
			canvasGroup.blocksRaycasts = false;
		}

		private void RebuildMetrics()
		{
			_messageHeights.Clear();
			_prefixHeights.Clear();
			_prefixHeights.Add(0f);
			_totalHeight = 0f;

			if (_viewport == null)
			{
				return;
			}

			EnsureMeasureBubbles();

			for (var i = 0; i < _messages.Count; i++)
			{
				var itemHeight = MeasureMessageHeight(_messages[i]);
				_messageHeights.Add(itemHeight);
				_totalHeight += itemHeight;
				_prefixHeights.Add(_totalHeight);
			}
		}

		private float MeasureMessageHeight(MessageBubbleData data)
		{
			if (data == null || _viewport == null)
			{
				return 1f;
			}

			var measureBubble = data.Type == MessageBubbleType.User ? _userMeasureBubble : _characterMeasureBubble;
			if (measureBubble == null)
			{
				return 72f;
			}

			var height = measureBubble.MeasureHeight(data, _viewport.rect.width);
			return Mathf.Max(1f, height);
		}

		private int FindStartIndex(float scroll)
		{
			if (_messages.Count == 0)
			{
				return 0;
			}

			var left = 0;
			var right = _messages.Count - 1;
			var result = 0;

			while (left <= right)
			{
				var middle = (left + right) / 2;
				var start = _prefixHeights[middle];
				var end = _prefixHeights[middle + 1];

				if (scroll < start)
				{
					right = middle - 1;
				}
				else if (scroll >= end)
				{
					left = middle + 1;
					result = Mathf.Min(_messages.Count - 1, left);
				}
				else
				{
					return middle;
				}
			}

			return Mathf.Clamp(result, 0, _messages.Count - 1);
		}
	}
}