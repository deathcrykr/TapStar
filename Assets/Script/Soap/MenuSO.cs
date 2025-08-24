using Obvious.Soap;
using Obvious.Soap.Attributes;
using UnityEngine;

namespace TapStar.Soap
{
	[CreateAssetMenu(fileName = "MenuSO", menuName = "TapStar/Soap/Menu")]
	public class MenuSO : ScriptableObject
	{
		[Header("Menu State s")]
		[Tooltip("상단 UI 표시 여부")]
		[SubAsset][SerializeField] private BoolVariable _top;
		public BoolVariable Top { get => _top; }

		[Tooltip("사이드바 UI 표시 여부")]
		[SubAsset][SerializeField] private BoolVariable _sidebar;
		public BoolVariable Sidebar { get => _sidebar; }

		[Tooltip("대시보드 UI 표시 여부")]
		[SubAsset][SerializeField] private BoolVariable _footer;
		public BoolVariable Footer { get => _footer; }
	}
}
