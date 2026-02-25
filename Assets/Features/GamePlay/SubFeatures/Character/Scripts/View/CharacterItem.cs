using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace Features.GamePlay.SubFeatures.Character.View
{
    public class CharacterItem : MonoBehaviour
    {
        public TextMeshProUGUI NameText;
        public Image AvatarImage;
        private Action _onClicked;

        public void Initialize(string name, Sprite avatar, Action onClicked)
        {
            NameText.text = name;
            AvatarImage.sprite = avatar;
            _onClicked = onClicked;
        }

        public void OnClicked()
        {
            _onClicked?.Invoke();
        }
    }

}
