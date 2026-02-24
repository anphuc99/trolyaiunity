using TMPro;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.Chat.View
{
	/// <summary>
	/// Auto-resizes a message bubble to fit text content.
	/// Width is capped by <see cref="_maxWidth"/>, while height can grow without limit.
	/// </summary>
	[ExecuteAlways]
	public sealed class ChatMessageAutoResize : MonoBehaviour
	{
		[SerializeField]
		private TMP_Text _messageText;

		[SerializeField]
		private RectTransform _bubbleRect;

		[SerializeField]
		[Min(1f)]
		private float _maxWidth = 420f;

		[SerializeField]
		[Min(0f)]
		private float _paddingTop = 16f;

		[SerializeField]
		[Min(0f)]
		private float _paddingBottom = 16f;

		[SerializeField]
		[Min(0f)]
		private float _paddingLeft = 20f;

		[SerializeField]
		[Min(0f)]
		private float _paddingRight = 20f;

		private string _lastText;

		private void Reset()
		{
			_messageText = GetComponentInChildren<TMP_Text>();
			_bubbleRect = transform as RectTransform;
			RefreshLayout();
		}

		private void OnEnable()
		{
			RefreshLayout();
		}

		private void OnValidate()
		{
			RefreshLayout();
		}

		private void LateUpdate()
		{
			if (_messageText == null)
			{
				return;
			}

			if (_lastText != _messageText.text)
			{
				RefreshLayout();
			}
		}

		/// <summary>
		/// Recalculates and applies text and bubble size.
		/// </summary>
		public void RefreshLayout()
		{
			if (_messageText == null || _bubbleRect == null)
			{
				return;
			}

			var textRect = _messageText.rectTransform;
			var horizontalPadding = _paddingLeft + _paddingRight;
			var verticalPadding = _paddingTop + _paddingBottom;
			var availableTextWidth = Mathf.Max(1f, _maxWidth - horizontalPadding);

			_messageText.enableWordWrapping = true;
			_messageText.overflowMode = TextOverflowModes.Overflow;

			var unconstrainedPreferred = _messageText.GetPreferredValues(_messageText.text, Mathf.Infinity, Mathf.Infinity);
			var textWidth = Mathf.Min(availableTextWidth, unconstrainedPreferred.x);
			var constrainedPreferred = _messageText.GetPreferredValues(_messageText.text, textWidth, Mathf.Infinity);

			textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth);
			textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, constrainedPreferred.y);

			if (textRect.parent == _bubbleRect)
			{
				textRect.offsetMin = new Vector2(_paddingLeft, _paddingBottom);
				textRect.offsetMax = new Vector2(-_paddingRight, -_paddingTop);
			}

			_bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth + horizontalPadding);
			_bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, constrainedPreferred.y + verticalPadding);

			_lastText = _messageText.text;
		}
	}
}