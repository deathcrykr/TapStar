using System;
using System.Collections.Generic;
using UnityEngine;

namespace TapStar.Soap
{
	/// <summary>
	/// 파티클 시스템의 데이터를 정의하는 클래스입니다.
	/// </summary>
	[Serializable]
	public class Particle
	{
		[Tooltip("파티클 시스템의 이름입니다.")]
		public string Name;

		[Tooltip("재생할 파티클 시스템 프리팹입니다.")]
		public GameObject Prefab;
		[Tooltip("파티클 시스템의 초기 위치입니다.")]
		public Vector3 Position;

		[Tooltip("파티클 시스템의 초기 회전값입니다.")]
		public Quaternion Rotation = Quaternion.identity;

		[Tooltip("파티클 시스템의 크기입니다.")]
		public Vector3 Scale = Vector3.zero;

		public int ScaleUI = 0;
		public string comment;
	}

	/// <summary>
	/// 파티클 시스템 데이터를 관리하는 ScriptableObject 클래스입니다.
	/// </summary>
	[CreateAssetMenu(fileName = "ParticleAsset", menuName = "TapStar/Soap/Particle", order = 1)]
	public class ParticleAssets : ScriptableObject
	{
		[Tooltip("파티클 시스템 목록을 정의하는 리스트입니다.")]
		public List<Particle> Particles;
	}
}
