using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Share.Components
{
	/// <summary>
	/// Animates input height when focused and restores when unfocused.
	/// </summary>
	public sealed class InputFocusHeightTween : MonoBehaviour
	{
		[SerializeField]
		private TMP_InputField _inputField;

		[SerializeField]
		private RectTransform _targetRect;

		[SerializeField]
		[Min(0f)]
		private float _heightIncrease = 12f;

		[SerializeField]
		[Min(0.01f)]
		private float _duration = 0.2f;

		[SerializeField]
		private Ease _focusEase = Ease.OutCubic;

		[SerializeField]
		private Ease _blurEase = Ease.InCubic;

		private float _baseHeight;
		private Tween _heightTween;

		private void Awake()
		{
			EnsureReferences();
			CacheBaseHeight();
			BindEvents();
		}

		private void OnEnable()
		{
			EnsureReferences();
			CacheBaseHeight();
			BindEvents();
		}

		private void OnDisable()
		{
			UnbindEvents();
			KillTween();
		}

		private void OnDestroy()
		{
			UnbindEvents();
			KillTween();
		}

		private void OnValidate()
		{
			EnsureReferences();
			CacheBaseHeight();
		}

		/// <summary>
		/// Refreshes the base height from the target rect.
		/// </summary>
		public void RefreshBaseHeight()
		{
			CacheBaseHeight();
		}

		private void EnsureReferences()
		{
			if (_inputField == null)
			{
				_inputField = GetComponent<TMP_InputField>();
			}

			if (_targetRect == null)
			{
				_targetRect = _inputField != null ? _inputField.transform as RectTransform : transform as RectTransform;
			}
		}

		private void CacheBaseHeight()
		{
			if (_targetRect == null)
			{
				return;
			}

			_baseHeight = _targetRect.sizeDelta.y;
		}

		private void BindEvents()
		{
			if (_inputField == null)
			{
				return;
			}

			_inputField.onSelect.RemoveListener(HandleSelected);
			_inputField.onDeselect.RemoveListener(HandleDeselected);
			_inputField.onSelect.AddListener(HandleSelected);
			_inputField.onDeselect.AddListener(HandleDeselected);
		}

		private void UnbindEvents()
		{
			if (_inputField == null)
			{
				return;
			}

			_inputField.onSelect.RemoveListener(HandleSelected);
			_inputField.onDeselect.RemoveListener(HandleDeselected);
		}

		private void HandleSelected(string value)
		{
			AnimateHeight(_baseHeight + _heightIncrease, _focusEase);
		}

		private void HandleDeselected(string value)
		{
			AnimateHeight(_baseHeight, _blurEase);
		}

		private void AnimateHeight(float targetHeight, Ease ease)
		{
			if (_targetRect == null)
			{
				return;
			}

			KillTween();

			var currentSize = _targetRect.sizeDelta;
			var endSize = new Vector2(currentSize.x, targetHeight);
			_heightTween = DOTween
				.To(() => _targetRect.sizeDelta, value => _targetRect.sizeDelta = value, endSize, _duration)
				.SetEase(ease);
		}

		private void KillTween()
		{
			if (_heightTween != null)
			{
				_heightTween.Kill();
				_heightTween = null;
			}
		}
	}
}
