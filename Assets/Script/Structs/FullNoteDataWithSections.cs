using System.Collections.Generic;
using UnityEngine;

namespace TapStar.Structs
{
	/// <summary>
	/// 노트 데이터와 보컬 섹션을 포함한 전체 데이터 구조체
	/// </summary>
	[System.Serializable]
	public class FullNoteDataWithSections
	{
		/// <summary>
		/// 노트 목록
		/// </summary>
		[SerializeField] private List<Note> notes = new List<Note>();
		public List<Note> Notes => notes;

		/// <summary>
		/// 보컬 섹션 목록
		/// </summary>

		[SerializeField] private List<VocalSection> vocal_sections = new List<VocalSection>();
		public List<VocalSection> VocalSections => vocal_sections;
	}
}
