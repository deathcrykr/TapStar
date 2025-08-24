
namespace TapStar.Structs
{
	/// <summary>
	/// 보컬 섹션 데이터 구조체
	/// </summary>
	[System.Serializable]
	public class VocalSection
	{
		/// <summary>
		/// 섹션 시작 시간 (초 단위)
		/// </summary>
		/// <summary>
		/// 섹션 시작 시간 (초 단위)
		/// </summary>
		[UnityEngine.SerializeField] private float start_time;
		/// <summary>
		/// 섹션 시작 시간 (초 단위)
		/// </summary>
		public float StartTime { get => start_time; set => start_time = value; }

		/// <summary>
		/// 섹션 종료 시간 (초 단위)
		/// </summary>
		[UnityEngine.SerializeField] private float end_time;
		/// <summary>
		/// 섹션 종료 시간 (초 단위)
		/// </summary>
		public float EndTime { get => end_time; set => end_time = value; }

		/// <summary>
		/// 섹션 타입 (vocal, sub_vocal, rap, ttaechang)
		/// </summary>
		[UnityEngine.SerializeField] private string type;
		/// <summary>
		/// 섹션 타입 (vocal, sub_vocal, rap, ttaechang)
		/// </summary>
		public string Type { get => type; set => type = value; }
	}
}
