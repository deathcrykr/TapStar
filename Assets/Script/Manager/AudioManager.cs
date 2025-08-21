using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
	[Header("FMOD Sound Effects")]
	[Tooltip("FMOD 사운드 이벤트 배열 설정")]
	[SerializeField] private FMODSound[] m_Sounds;
	public FMODSound[] Sounds { get => m_Sounds; set => m_Sounds = value; }

	[Header("Music")]
	[Tooltip("배경음악으로 사용할 FMOD 이벤트 레퍼런스")]
	[SerializeField] private EventReference m_BackgroundMusicEvent;
	public EventReference BackgroundMusicEvent { get => m_BackgroundMusicEvent; set => m_BackgroundMusicEvent = value; }

	[Tooltip("게임 시작 시 자동으로 배경음악 재생 여부")]
	[SerializeField] private bool m_PlayMusicOnStart = true;
	public bool PlayMusicOnStart { get => m_PlayMusicOnStart; set => m_PlayMusicOnStart = value; }

	[Header("Settings")]
	[Tooltip("전체 볼륨 설정 (0.0 ~ 1.0)")]
	[SerializeField][Range(0f, 1f)] private float m_MasterVolume = 1f;
	public float MasterVolume { get => m_MasterVolume; set => m_MasterVolume = value; }

	[Tooltip("배경음악 볼륨 설정 (0.0 ~ 1.0)")]
	[SerializeField][Range(0f, 1f)] private float m_MusicVolume = 1f;
	public float MusicVolume { get => m_MusicVolume; set => m_MusicVolume = value; }

	[Tooltip("효과음 볼륨 설정 (0.0 ~ 1.0)")]
	[SerializeField][Range(0f, 1f)] private float m_SoundVolume = 1f;
	public float SoundVolume { get => m_SoundVolume; set => m_SoundVolume = value; }

	[Tooltip("배경음악 활성화 여부")]
	[SerializeField] private bool m_MusicEnabled = true;
	public bool MusicEnabled { get => m_MusicEnabled; set => m_MusicEnabled = value; }

	[Tooltip("효과음 활성화 여부")]
	[SerializeField] private bool m_SoundEnabled = true;
	public bool SoundEnabled { get => m_SoundEnabled; set => m_SoundEnabled = value; }

	[Tooltip("진동 활성화 여부 (모바일 플랫폼 전용)")]
	[SerializeField] private bool m_VibrationEnabled = true;
	public bool VibrationEnabled { get => m_VibrationEnabled; set => m_VibrationEnabled = value; }

	public static AudioManager Instance;

	private FMOD.Studio.EventInstance m_MusicInstance;
	private Dictionary<string, FMODSound> m_SoundDictionary = new Dictionary<string, FMODSound>();

	// Lifecycle Methods
	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
			DontDestroyOnLoad(gameObject);
			InitializeAudio();
		}
		else
		{
			Destroy(gameObject);
		}
	}

	private void Start()
	{
		if (m_PlayMusicOnStart && !m_BackgroundMusicEvent.IsNull)
		{
			PlayMusic(m_BackgroundMusicEvent);
		}
	}

	private void OnApplicationPause(bool pauseStatus)
	{
		if (pauseStatus)
		{
			PauseMusic();
		}
		else
		{
			if (m_MusicEnabled)
				ResumeMusic();
		}
	}

	private void OnApplicationFocus(bool hasFocus)
	{
		if (!hasFocus)
		{
			PauseMusic();
		}
		else
		{
			if (m_MusicEnabled)
				ResumeMusic();
		}
	}

	// Public Methods
	/// <summary>
	/// 지정된 이름의 사운드를 재생합니다.
	/// </summary>
	/// <param name="soundName">재생할 사운드의 이름</param>
	public void PlaySound(string soundName)
	{
		if (!m_SoundEnabled)
			return;

		if (m_SoundDictionary.ContainsKey(soundName))
		{
			FMODSound sound = m_SoundDictionary[soundName];
			if (!sound.eventReference.IsNull)
			{
				FMOD.Studio.EventInstance instance = RuntimeManager.CreateInstance(sound.eventReference);

				float finalVolume = sound.volume * m_SoundVolume * m_MasterVolume;
				instance.setVolume(finalVolume);

				if (sound.is3D)
				{
					FMOD.ATTRIBUTES_3D attributes = RuntimeUtils.To3DAttributes(transform.position);
					instance.set3DAttributes(attributes);
				}

				instance.start();
				instance.release();
			}
			else
			{
				PlaySynthesizedSound(soundName);
			}
		}
		else
		{
			Debug.LogWarning($"Sound '{soundName}' not found!");
		}
	}

	/// <summary>
	/// 배경음악을 재생합니다.
	/// </summary>
	/// <param name="eventReference">재생할 FMOD 이벤트 레퍼런스</param>
	public void PlayMusic(EventReference eventReference)
	{
		if (!m_MusicEnabled || eventReference.IsNull)
			return;

		StopMusic();

		m_MusicInstance = RuntimeManager.CreateInstance(eventReference);
		m_MusicInstance.setVolume(m_MusicVolume * m_MasterVolume);
		m_MusicInstance.start();
	}

	/// <summary>
	/// 현재 재생 중인 배경음악을 정지합니다.
	/// </summary>
	public void StopMusic()
	{
		if (m_MusicInstance.isValid())
		{
			m_MusicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
			m_MusicInstance.release();
		}
	}

	/// <summary>
	/// 현재 재생 중인 배경음악을 일시정지합니다.
	/// </summary>
	public void PauseMusic()
	{
		if (m_MusicInstance.isValid())
		{
			m_MusicInstance.setPaused(true);
		}
	}

	/// <summary>
	/// 일시정지된 배경음악을 다시 재생합니다.
	/// </summary>
	public void ResumeMusic()
	{
		if (m_MusicInstance.isValid())
		{
			m_MusicInstance.setPaused(false);
		}
	}

	/// <summary>
	/// 클릭 사운드를 재생하고 진동을 실행합니다.
	/// </summary>
	public void PlayClickSound()
	{
		PlaySound("click");
		if (m_VibrationEnabled && Application.isMobilePlatform)
		{
			Handheld.Vibrate();
		}
	}

	/// <summary>
	/// 히트 사운드를 재생하고 진동을 실행합니다.
	/// </summary>
	public void PlayHitSound()
	{
		PlaySound("hit");
		if (m_VibrationEnabled && Application.isMobilePlatform)
		{
			Handheld.Vibrate();
		}
	}

	/// <summary>
	/// 퍼펙트 사운드를 재생하고 진동을 실행합니다.
	/// </summary>
	public void PlayPerfectSound()
	{
		PlaySound("perfect");
		if (m_VibrationEnabled && Application.isMobilePlatform)
		{
			Handheld.Vibrate();
		}
	}

	/// <summary>
	/// 미스 사운드를 재생합니다.
	/// </summary>
	public void PlayMissSound()
	{
		PlaySound("miss");
	}

	/// <summary>
	/// 마스터 볼륨을 설정합니다.
	/// </summary>
	/// <param name="volume">설정할 볼륨 값 (0.0 ~ 1.0)</param>
	public void SetMasterVolume(float volume)
	{
		m_MasterVolume = Mathf.Clamp01(volume);
		UpdateVolumes();
	}

	/// <summary>
	/// 배경음악 볼륨을 설정합니다.
	/// </summary>
	/// <param name="volume">설정할 볼륨 값 (0.0 ~ 1.0)</param>
	public void SetMusicVolume(float volume)
	{
		m_MusicVolume = Mathf.Clamp01(volume);
		UpdateVolumes();
	}

	/// <summary>
	/// 효과음 볼륨을 설정합니다.
	/// </summary>
	/// <param name="volume">설정할 볼륨 값 (0.0 ~ 1.0)</param>
	public void SetSoundVolume(float volume)
	{
		m_SoundVolume = Mathf.Clamp01(volume);
		UpdateVolumes();
	}

	/// <summary>
	/// 배경음악의 활성화 상태를 토글합니다.
	/// </summary>
	public void ToggleMusic()
	{
		m_MusicEnabled = !m_MusicEnabled;

		if (m_MusicEnabled)
		{
			if (!m_BackgroundMusicEvent.IsNull)
			{
				PlayMusic(m_BackgroundMusicEvent);
			}
		}
		else
		{
			StopMusic();
		}
	}

	/// <summary>
	/// 효과음의 활성화 상태를 토글합니다.
	/// </summary>
	public void ToggleSound()
	{
		m_SoundEnabled = !m_SoundEnabled;
	}

	/// <summary>
	/// 진동의 활성화 상태를 토글합니다.
	/// </summary>
	public void ToggleVibration()
	{
		m_VibrationEnabled = !m_VibrationEnabled;
	}

	// Private Methods
	/// <summary>
	/// 오디오 시스템을 초기화합니다.
	/// </summary>
	private void InitializeAudio()
	{
		SetupSounds();
		CreateDefaultSounds();

		if (!m_BackgroundMusicEvent.IsNull)
		{
			m_MusicInstance = RuntimeManager.CreateInstance(m_BackgroundMusicEvent);
		}
	}

	/// <summary>
	/// 사운드 배열을 딕셔너리로 설정합니다.
	/// </summary>
	private void SetupSounds()
	{
		m_SoundDictionary.Clear();

		foreach (FMODSound sound in m_Sounds)
		{
			if (!sound.eventReference.IsNull)
			{
				m_SoundDictionary[sound.name] = sound;
			}
		}
	}

	/// <summary>
	/// 기본 사운드들을 생성합니다.
	/// </summary>
	private void CreateDefaultSounds()
	{
		if (m_SoundDictionary.Count == 0)
		{
			CreateDummySound("click", "event:/SFX/Click", 1f);
			CreateDummySound("hit", "event:/SFX/Hit", 1f);
			CreateDummySound("perfect", "event:/SFX/Perfect", 1.2f);
			CreateDummySound("miss", "event:/SFX/Miss", 0.8f);
		}
	}

	/// <summary>
	/// 더미 사운드를 생성하여 딕셔너리에 추가합니다.
	/// </summary>
	/// <param name="soundName">사운드 이름</param>
	/// <param name="eventRef">FMOD 이벤트 경로</param>
	/// <param name="volume">볼륨 값</param>
	private void CreateDummySound(string soundName, string eventRef, float volume)
	{
		FMODSound dummySound = new FMODSound
		{
			name = soundName,
			eventReference = EventReference.Find(eventRef),
			volume = volume,
			is3D = false
		};

		m_SoundDictionary[soundName] = dummySound;
	}

	/// <summary>
	/// 합성된 대체 사운드를 재생합니다.
	/// </summary>
	/// <param name="soundName">재생할 사운드 이름</param>
	private void PlaySynthesizedSound(string soundName)
	{
		switch (soundName)
		{
			case "click":
			case "hit":
				PlayBeep(800f, 0.1f);
				break;
			case "perfect":
				PlaySuccessSound();
				break;
			case "miss":
				PlayBeep(400f, 0.2f);
				break;
		}
	}

	/// <summary>
	/// 대체용 비프 사운드를 재생합니다.
	/// </summary>
	/// <param name="frequency">주파수</param>
	/// <param name="duration">지속 시간</param>
	private void PlayBeep(float frequency, float duration)
	{
		Debug.Log($"Fallback beep sound: {frequency}Hz for {duration}s");
	}

	/// <summary>
	/// 대체용 코드 사운드를 재생합니다.
	/// </summary>
	private void PlayChord()
	{
		Debug.Log("Fallback chord sound");
	}

	/// <summary>
	/// 대체용 성공 사운드를 재생합니다.
	/// </summary>
	private void PlaySuccessSound()
	{
		Debug.Log("Fallback success sound");
	}

	/// <summary>
	/// 모든 볼륨 설정을 업데이트합니다.
	/// </summary>
	private void UpdateVolumes()
	{
		if (m_MusicInstance.isValid())
		{
			m_MusicInstance.setVolume(m_MusicVolume * m_MasterVolume);
		}

		RuntimeManager.StudioSystem.setParameterByName("MasterVolume", m_MasterVolume);
		RuntimeManager.StudioSystem.setParameterByName("MusicVolume", m_MusicVolume);
		RuntimeManager.StudioSystem.setParameterByName("SFXVolume", m_SoundVolume);
	}
}
