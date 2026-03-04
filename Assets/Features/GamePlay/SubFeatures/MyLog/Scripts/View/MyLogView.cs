using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.MyLog.Events;
using Features.GamePlay.SubFeatures.MyLog.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.MyLog.Requests;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Features.GamePlay.SubFeatures.MyLog.View
{
	/// <summary>
	/// View for MyLog.
	/// </summary>
	public sealed class MyLogView : BaseView
	{

		[Header("Tạo nhật ký mới")]
		[SerializeField]
		private TMP_InputField _inputNewLog;
		[SerializeField]
		private Button _btnCreateNewLog;

		[Header("Danh sách nhật ký")]
		[SerializeField]
		private MyLogItemView _itemViewPrefab;
		[SerializeField]
		private Transform _itemViewContainer;
    }
}
