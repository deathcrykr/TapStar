using Obvious.Soap;
using Obvious.Soap.Attributes;
using TapStar.Extensions;
using UnityEngine;

namespace TapStar.Soap
{
	[CreateAssetMenu(fileName = "PlayerSO", menuName = "TapStar/Soap/Player")]
	public class PlayerSO : ScriptableObject
	{
		[Header("Player Progress")]
		[Tooltip("팬 수 (경을 넘어갈 수 있음)")]
		[SubAsset][SerializeField] private EncryptStringVariable _fanCount;
		public EncryptStringVariable FanCount => _fanCount;
		public string FanCountFormatted => _fanCount.Value.FormatBigInt();

		[Tooltip("현재 경험치")]
		[SubAsset][SerializeField] private EncryptFloatVariable _experience;
		public EncryptFloatVariable Experience => _experience;

		[Tooltip("다음 레벨까지 필요한 경험치")]
		[SubAsset][SerializeField] private EncryptFloatVariable _experienceToNext;
		public EncryptFloatVariable ExperienceToNext => _experienceToNext;

		[Header("Player Stats")]
		[Tooltip("총 클릭 횟수")]
		[SubAsset][SerializeField] private EncryptStringVariable _totalClicks;
		public EncryptStringVariable TotalClicks => _totalClicks;
		public string TotalClicksFormatted => _totalClicks.Value.FormatBigInt();

		[Tooltip("현재 세션 클릭 횟수")]
		[SubAsset][SerializeField] private IntVariable _sessionClicks;
		public IntVariable SessionClicks => _sessionClicks;

		[Header("Currency")]
		[Tooltip("보유 머니")]
		[SubAsset][SerializeField] private EncryptStringVariable _money;
		public EncryptStringVariable Money => _money;
		public string MoneyFormatted => _money.Value.FormatBigInt();

		[Tooltip("보석")]
		[SubAsset][SerializeField] private EncryptIntVariable _gems;
		public EncryptIntVariable Gems => _gems;
		public string GemsFormatted => _gems.Value.FormatBigInt();

		[Header("Crypto Mining")]
		[Tooltip("크립토 코인 (소수점 18자리)")]
		[SubAsset][SerializeField] private EncryptStringVariable _cryptoCoin;
		public EncryptStringVariable CryptoCoin => _cryptoCoin;
		public string CryptoCoinFormatted => _cryptoCoin.Value.FormatBigInt();

		[Tooltip("채굴 속도 (초당)")]
		[SubAsset][SerializeField] private EncryptFloatVariable _miningRate;
		public EncryptFloatVariable MiningRate => _miningRate;

		[Tooltip("채굴 파워")]
		[SubAsset][SerializeField] private EncryptFloatVariable _miningPower;
		public EncryptFloatVariable MiningPower => _miningPower;
	}
}
