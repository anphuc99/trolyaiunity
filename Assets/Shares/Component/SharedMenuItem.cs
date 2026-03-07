using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Share.Components
{
    public class SharedMenuItem : MonoBehaviour
    {
        public TextMeshProUGUI Text;
        public Button button;
        public Action OnClick;

        [NonSerialized]
        public string id;

        void OnValidate()
        {
            if (Text == null)
            {
                Text = GetComponentInChildren<TextMeshProUGUI>();
            }

            if (button == null)
            {
                button = GetComponent<Button>();
            }
        }

        private void Awake()
        {
            if(button != null)
            {
                button.onClick.AddListener(() => OnClick?.Invoke());
            }
        }
    }
}

