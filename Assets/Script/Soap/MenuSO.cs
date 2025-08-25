using Obvious.Soap;
using Obvious.Soap.Attributes;
using UnityEngine;

namespace TapStar.Soap
{
	[CreateAssetMenu(fileName = "UISO", menuName = "TapStar/Soap/UI")]
	public class UISO : ScriptableObject
	{
		[Header("Menu State")]
		[Tooltip("상단 UI 표시 여부")]
		[SubAsset][SerializeField] private BoolVariable _top;
		public BoolVariable Top => _top;

		[Tooltip("사이드바 UI 표시 여부")]
		[SubAsset][SerializeField] private BoolVariable _sidebar;
		public BoolVariable Sidebar => _sidebar;

		[Tooltip("대시보드 UI 표시 여부")]
		[SubAsset][SerializeField] private BoolVariable _footer;
		public BoolVariable Footer => _footer;

		[Header("Page")]
		[Tooltip("페이지 UI 표시 여부")]
		[SubAsset][SerializeField] private BoolVariable _introPage;
		public BoolVariable IntroPage => _introPage;

		[SubAsset][SerializeField] private BoolVariable _gamePage;
		public BoolVariable GamePage => _gamePage;
	}
}
