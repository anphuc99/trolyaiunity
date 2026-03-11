using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        private Image _backgroundImage;

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

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            StopAllCoroutines();
            KillFadeTween();
            StartCoroutine(StartShowNotification(message, duration));
        }

        private IEnumerator StartShowNotification(string message, float duration)
        {
            _notificationText.text = message ?? string.Empty;

            var textColor = _notificationText.color;
            textColor.a = 1f;
            _notificationText.color = textColor;

            Color? backgroundColor = null;
            if (_backgroundImage != null)
            {
                var color = _backgroundImage.color;
                color.a = 1f;
                _backgroundImage.color = color;
                backgroundColor = color;
            }

            gameObject.SetActive(true);

            if (duration > 0f)
            {
                yield return new WaitForSeconds(duration);
            }

            var fadeDuration = Mathf.Max(0.01f, _fadeDuration);
            var fadeSequence = DOTween.Sequence();
            fadeSequence.Join(
                DOTween.To(
                    () => _notificationText.color,
                    value => _notificationText.color = value,
                    new Color(textColor.r, textColor.g, textColor.b, 0f),
                    fadeDuration));

            if (backgroundColor.HasValue && _backgroundImage != null)
            {
                var bg = backgroundColor.Value;
                fadeSequence.Join(
                    DOTween.To(
                        () => _backgroundImage.color,
                        value => _backgroundImage.color = value,
                        new Color(bg.r, bg.g, bg.b, 0f),
                        fadeDuration));
            }

            _fadeTween = fadeSequence.SetUpdate(true);

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