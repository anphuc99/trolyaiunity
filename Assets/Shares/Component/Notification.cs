using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Share.Components
{
    /// <summary>
    /// Displays a temporary notification text, then fades and hides it.
    /// </summary>
    public sealed class Notification : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _notificationText;

        [SerializeField]
        private float _fadeDuration = 0.4f;

        private Tween _fadeTween;

        /// <summary>
        /// Shows a notification, keeps it visible for <paramref name="duration"/>,
        /// then fades it out and hides the object.
        /// </summary>
        /// <param name="message">Notification text.</param>
        /// <param name="duration">Visible duration before fading.</param>
        public void ShowNotification(string message, float duration = 2f)
        {
            if (_notificationText == null)
            {
                Debug.LogWarning("[Notification] Missing text reference.", this);
                return;
            }

            StopAllCoroutines();
            KillFadeTween();
            StartCoroutine(StartShowNotification(message, duration));
        }

        private IEnumerator StartShowNotification(string message, float duration)
        {
            _notificationText.text = message ?? string.Empty;

            var color = _notificationText.color;
            color.a = 1f;
            _notificationText.color = color;

            gameObject.SetActive(true);

            if (duration > 0f)
            {
                yield return new WaitForSeconds(duration);
            }

            _fadeTween = DOTween
                .To(
                    () => _notificationText.color,
                    value => _notificationText.color = value,
                    new Color(color.r, color.g, color.b, 0f),
                    Mathf.Max(0.01f, _fadeDuration))
                .SetUpdate(true);

            yield return _fadeTween.WaitForCompletion();

            gameObject.SetActive(false);
            _fadeTween = null;
        }

        private void OnDisable()
        {
            KillFadeTween();
        }

        private void KillFadeTween()
        {
            if (_fadeTween == null)
            {
                return;
            }

            _fadeTween.Kill();
            _fadeTween = null;
        }
    }
}