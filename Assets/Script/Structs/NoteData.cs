using System.Collections.Generic;

/// <summary>
/// 노트 데이터 컨테이너
/// </summary>
[System.Serializable]
public class NoteData
{
	/// <summary>
	/// 노트 목록
	/// </summary>
	public List<Note> notes = new List<Note>();
}
