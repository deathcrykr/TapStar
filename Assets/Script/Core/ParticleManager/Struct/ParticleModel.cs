using System;
using UnityEngine;

namespace TapStar.Manager
{
	[Serializable]
	public class ParticleModel
	{
		public string name;

		[Space(10)]
		public GameObject wrapObject;
		public GameObject particleObject;
		public Vector3 offset;

		[HideInInspector]
		public Vector3 originalPosition; // 원래 위치 저장

		[Space(10)]
		[HideInInspector]
		public bool isInit = false;
		[HideInInspector]
		public bool isPlaying = false; // 애니메이션 실행 여부
		public bool isCopy = false;
		public int playCount = 1;

		/// <summary>
		/// 초기화 메서드
		/// </summary>
		public void Init()
		{
			if (isInit)
				return;

			isInit = true;
			playCount = 1;
			if (particleObject != null)
			{
				originalPosition = particleObject.transform.position; // 초기화 시 원래 위치 설정
			}
		}
	}
}
