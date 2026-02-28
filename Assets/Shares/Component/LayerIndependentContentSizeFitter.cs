using UnityEngine;

namespace Share.Components
{
	/// <summary>
	/// Resizes a RectTransform to fit its child content without relying on Unity's layout system.
	/// This avoids parent layer/layout constraints that can block ContentSizeFitter.
	/// </summary>
	[ExecuteAlways]
	public sealed class LayerIndependentContentSizeFitter : MonoBehaviour
	{
		[SerializeField]
		private RectTransform _targetRect;

		[SerializeField]
		private RectTransform _contentRoot;

		[SerializeField]
		private bool _fitWidth = true;

		[SerializeField]
		private bool _fitHeight = true;

		[SerializeField]
		private Vector2 _minSize = Vector2.zero;

		[SerializeField]
		private float _paddingLeft = 0f;

		[SerializeField]
		private float _paddingRight = 0f;

		[SerializeField]
		private float _paddingTop = 0f;

		[SerializeField]
		private float _paddingBottom = 0f;

		[SerializeField]
		private bool _includeInactive = false;

		private void Reset()
		{
			_targetRect = transform as RectTransform;
			_contentRoot = _targetRect;
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
			RefreshLayout();
		}

		/// <summary>
		/// Recalculates and applies size based on child bounds.
		/// </summary>
		public void RefreshLayout()
		{
			if (_targetRect == null || _contentRoot == null)
			{
				return;
			}

			var corners = new Vector3[4];
			var hasBounds = false;
			var minX = float.MaxValue;
			var maxX = float.MinValue;
			var minY = float.MaxValue;
			var maxY = float.MinValue;

			for (var i = 0; i < _contentRoot.childCount; i++)
			{
				var child = _contentRoot.GetChild(i) as RectTransform;
				if (child == null)
				{
					continue;
				}

				if (!_includeInactive && !child.gameObject.activeInHierarchy)
				{
					continue;
				}

				child.GetWorldCorners(corners);
				for (var c = 0; c < corners.Length; c++)
				{
					var local = _targetRect.InverseTransformPoint(corners[c]);
					if (local.x < minX)
					{
						minX = local.x;
					}
					if (local.x > maxX)
					{
						maxX = local.x;
					}
					if (local.y < minY)
					{
						minY = local.y;
					}
					if (local.y > maxY)
					{
						maxY = local.y;
					}
				}

				hasBounds = true;
			}

			var width = _minSize.x;
			var height = _minSize.y;
			if (hasBounds)
			{
				width = Mathf.Max(_minSize.x, (maxX - minX) + _paddingLeft + _paddingRight);
				height = Mathf.Max(_minSize.y, (maxY - minY) + _paddingTop + _paddingBottom);
			}

			if (_fitWidth)
			{
				_targetRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
			}

			if (_fitHeight)
			{
				_targetRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
			}
		}
	}
}
