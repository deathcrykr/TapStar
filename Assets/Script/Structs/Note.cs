using UnityEngine;

/// <summary>
/// 리듬 게임 노트 데이터 구조체
/// </summary>
[System.Serializable]
public class Note
{
	/// <summary>
	/// 노트가 나타날 시간 (초 단위)
	/// </summary>
	public float time_seconds;

	/// <summary>
	/// 노트의 레인 번호
	/// </summary>
	public int lane;

	/// <summary>
	/// 노트 타입 (예: "tap", "hold", "flick")
	/// </summary>
	public string type;

	/// <summary>
	/// 노트 강도 (0.0 ~ 1.0)
	/// </summary>
	public float intensity;

	/// <summary>
	/// 난이도 레벨 (1=Easy, 2=Medium, 3=Hard)
	/// </summary>
	public int level = 1;
}
