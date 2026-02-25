using Core.Infrastructure.Views;
using Features.CharacterInfo.Events;
using Features.CharacterInfo.Infrastructure.Attributes;
using Features.CharacterInfo.Requests;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Features.CharacterInfo.View
{
	/// <summary>
	/// View for CharacterInfo.
	/// </summary>
	public sealed class CharacterInfoView : BaseView
	{
		[Header("Thông tin cơ bản")]
		[SerializeField] 
		private TextMeshProUGUI _name;
		[SerializeField]
		private Image _avatar;
		[SerializeField]
		private TextMeshProUGUI _age;
		[SerializeField]
		private TextMeshProUGUI _description;

		[Header("Giới tính")]
		[SerializeField]
		private TextMeshProUGUI _gender;
		[SerializeField]
		private Image _genderIcon;
		[SerializeField]
		private Sprite _maleIcon;
		[SerializeField]
		private Sprite _femaleIcon;
		
		[Header("Giọng nói")]
		[SerializeField]
		private TextMeshProUGUI _voiceName;
		[SerializeField]
		private TextMeshProUGUI _pitch;
		[SerializeField]
		private Slider _pitchSlider;

		[Header("Chỉnh sửa")]
		[SerializeField]
		private Button _editButton;
		[SerializeField]
		private Button _removeButton;
		[SerializeField]
		private Button _backButton;
	}
}
