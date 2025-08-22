using System.Collections.Generic;

/// <summary>
/// 노트 데이터와 보컬 섹션을 포함한 전체 데이터 구조체
/// </summary>
[System.Serializable]
public class FullNoteDataWithSections
{
	/// <summary>
	/// 노트 목록
	/// </summary>
	public List<Note> notes = new List<Note>();

	/// <summary>
	/// 보컬 섹션 목록
	/// </summary>
	public List<VocalSection> vocal_sections = new List<VocalSection>();
}
