namespace TapStar.Structs
{
	/// <summary>
	/// 리듬 게임 노트 데이터 구조체
	/// </summary>
	[System.Serializable]
	public class Note
	{
		/// <summary>
		/// 노트가 나타날 시간 (초 단위)
		/// </summary>
		/// <summary>
		/// 노트가 나타날 시간 (초 단위)
		/// </summary>
		[UnityEngine.SerializeField] private float time_seconds;
		/// <summary>
		/// 노트가 나타날 시간 (초 단위)
		/// </summary>
		public float TimeSeconds => time_seconds;

		/// <summary>
		/// 노트의 레인 번호
		/// </summary>
		[UnityEngine.SerializeField] private int lane;
		/// <summary>
		/// 노트의 레인 번호
		/// </summary>
		public int Lane => lane;

		/// <summary>
		/// 노트 타입 (예: "tap", "hold", "flick")
		/// </summary>
		[UnityEngine.SerializeField] private string type;
		/// <summary>
		/// 노트 타입 (예: "tap", "hold", "flick")
		/// </summary>
		public string Type => type;

		/// <summary>
		/// 노트 강도 (0.0 ~ 1.0)
		/// </summary>
		[UnityEngine.SerializeField] private float intensity;
		/// <summary>
		/// 노트 강도 (0.0 ~ 1.0)
		/// </summary>
		public float Intensity => intensity;

		/// <summary>
		/// 난이도 레벨 (1=Easy, 2=Medium, 3=Hard)
		/// </summary>
		[UnityEngine.SerializeField] private int level = 1;
		/// <summary>
		/// 난이도 레벨 (1=Easy, 2=Medium, 3=Hard)
		/// </summary>
		public int Level => level;
	}
}
