/// <summary>
/// 보컬 섹션 데이터 구조체
/// </summary>
[System.Serializable]
public class VocalSection
{
	/// <summary>
	/// 섹션 시작 시간 (초 단위)
	/// </summary>
	public float start_time;

	/// <summary>
	/// 섹션 종료 시간 (초 단위)
	/// </summary>
	public float end_time;

	/// <summary>
	/// 섹션 타입 (vocal, sub_vocal, rap, ttaechang)
	/// </summary>
	public string type;
}
