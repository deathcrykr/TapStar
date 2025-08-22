using FMODUnity;
using UnityEngine;

/// <summary>
/// FMOD 사운드 데이터 구조체
/// </summary>
[System.Serializable]
public class FMODSound
{
	/// <summary>
	/// 사운드 이름 (식별자)
	/// </summary>
	public string name;

	/// <summary>
	/// FMOD 이벤트 참조
	/// </summary>
	public EventReference eventReference;

	/// <summary>
	/// 볼륨 (0.0 ~ 1.0)
	/// </summary>
	[Range(0f, 1f)]
	public float volume = 1f;

	/// <summary>
	/// 3D 사운드 여부
	/// </summary>
	public bool is3D = false;

	/// <summary>
	/// FMOD 이벤트 인스턴스 (런타임에서 사용)
	/// </summary>
	[HideInInspector]
	public FMOD.Studio.EventInstance instance;
}
