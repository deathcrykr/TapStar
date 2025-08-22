using System.Collections.Generic;

namespace TabStar.Structs
{
	/// <summary>
	/// 노트 데이터 컨테이너
	/// </summary>
	[System.Serializable]
	public class NoteData
	{
		/// <summary>
		/// 노트 목록
		/// </summary>
		[UnityEngine.SerializeField] private List<Note> notes = new List<Note>();
		public List<Note> Notes
		{
			get => notes;
			set => notes = value;
		}
	}
}
