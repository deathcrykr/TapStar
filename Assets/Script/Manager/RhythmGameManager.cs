using System.Collections.Generic;
using DG.Tweening;
using FMODUnity;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 리듬 게임의 핵심 로직을 관리하는 매니저 클래스
/// </summary>
public class RhythmGameManager : MonoBehaviour
{
	public static RhythmGameManager Instance { get; private set; }

	[Header("Game Settings")]
	[Tooltip("노트가 이동하는 속도")]
	[SerializeField] private float m_NoteSpeed = 2f;
	public float NoteSpeed { get => m_NoteSpeed; set => m_NoteSpeed = value; }

	[Tooltip("히트 판정 윈도우 (50ms - 전문 리듬게임 수준의 정확도)")]
	[SerializeField] private float m_HitWindow = 0.05f;
	public float HitWindow { get => m_HitWindow; set => m_HitWindow = value; }

	[Header("Audio Calibration")]
	[Tooltip("정밀한 리듬 게임용 오디오 지연 보정 (20ms)")]
	[SerializeField] private float m_AudioLatencyOffset = 0.020f;
	public float AudioLatencyOffset { get => m_AudioLatencyOffset; set => m_AudioLatencyOffset = value; }

	[Header("Difficulty")]
	[Tooltip("난이도 레벨 (1=Easy, 2=Medium, 3=Hard)")]
	[SerializeField] private int m_DifficultyLevel = 3;
	public int DifficultyLevel { get => m_DifficultyLevel; set => m_DifficultyLevel = value; }

	[Header("Debug")]
	[Tooltip("디버그 모드: 보컬 섹션 라인 표시")]
	[SerializeField] private bool m_ShowSectionLines = false;
	public bool ShowSectionLines { get => m_ShowSectionLines; set => m_ShowSectionLines = value; }

	[Header("UI Elements")]
	[Tooltip("중앙 타겟 오브젝트")]
	[SerializeField] private Transform m_CenterTarget;
	public Transform CenterTarget { get => m_CenterTarget; set => m_CenterTarget = value; }

	[Tooltip("노트 프리팹")]
	[SerializeField] private GameObject m_NotePrefab;
	public GameObject NotePrefab { get => m_NotePrefab; set => m_NotePrefab = value; }

	[Tooltip("섹션 라인 프리팹")]
	[SerializeField] private GameObject m_SectionLinePrefab;
	public GameObject SectionLinePrefab { get => m_SectionLinePrefab; set => m_SectionLinePrefab = value; }

	[Tooltip("게임 캔버스")]
	[SerializeField] private Canvas m_GameCanvas;
	public Canvas GameCanvas { get => m_GameCanvas; set => m_GameCanvas = value; }

	[Header("Audio")]
	[Tooltip("음악 이벤트 경로")]
	[SerializeField] private EventReference m_MusicEventPath;
	public EventReference MusicEventPath { get => m_MusicEventPath; set => m_MusicEventPath = value; }

	[Tooltip("히트 사운드 이벤트 경로")]
	[SerializeField] private EventReference m_HitSoundEventPath;
	public EventReference HitSoundEventPath { get => m_HitSoundEventPath; set => m_HitSoundEventPath = value; }

	[Tooltip("음악 파일명")]
	[SerializeField] private string m_MusicFileName = "disco-train";
	public string MusicFileName { get => m_MusicFileName; set => m_MusicFileName = value; }

	[Header("Scoring")]
	[Tooltip("현재 점수")]
	[SerializeField] private int m_Score = 0;
	public int Score { get => m_Score; set => m_Score = value; }

	[Tooltip("점수 텍스트 UI")]
	[SerializeField] private Text m_ScoreText;
	public Text ScoreText { get => m_ScoreText; set => m_ScoreText = value; }

	public float currentTime = 0f;

	private FMOD.Studio.EventInstance m_MusicInstance;
	private NoteData m_CurrentNoteData;
	private List<VocalSection> m_CurrentVocalSections = new List<VocalSection>();
	private List<GameObject> m_ActiveNotes = new List<GameObject>();
	private List<GameObject> m_ActiveSectionLines = new List<GameObject>();
	private int m_NextNoteIndex = 0;
	private int m_NextSectionIndex = 0;
	private bool m_IsPlaying = false;

	// Lifecycle Methods
	/// <summary>
	/// 싱글톤 인스턴스를 설정합니다.
	/// </summary>
	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
		}
		else
		{
			Destroy(gameObject);
		}
	}

	/// <summary>
	/// 게임 매니저를 초기화합니다.
	/// </summary>
	private void Start()
	{
		Debug.Log("🎮 RhythmGameManager starting...");

		// FMOD 필수 체크
		if (!CheckFMODAvailability())
		{
			Debug.LogError("❌ FMOD is not available! RhythmGameManager disabled.");
			enabled = false;
			return;
		}

		// 필수 컴포넌트들 미리 설정
		SetupRequiredComponents();

		LoadMusicAndNotes();

		// 자동 게임 시작 (테스트용)
		if (m_CurrentNoteData != null && m_CurrentNoteData.notes.Count > 0)
		{
			Debug.Log("🚀 Auto-starting game for testing...");
			Invoke(nameof(StartGame), 1f); // 1초 후 자동 시작
		}
		else
		{
			Debug.LogError("❌ Cannot start game - no note data available!");
			if (m_CurrentNoteData == null) Debug.LogError("   - currentNoteData is null");
			else Debug.LogError($"   - currentNoteData has {m_CurrentNoteData.notes.Count} notes");
		}
	}

	/// <summary>
	/// 게임 상태를 업데이트합니다.
	/// </summary>
	private void Update()
	{
		if (m_IsPlaying)
		{
			UpdateGameTime();
			SpawnNotes();
			if (m_ShowSectionLines) // 디버그 모드에서만 섹션 라인 표시
			{
				SpawnSectionLines();
			}
			UpdateNotes();
			if (m_ShowSectionLines) // 디버그 모드에서만 섹션 라인 업데이트
			{
				UpdateSectionLines();
			}
			CheckInput();
		}

		// 새로운 Input System 사용
		if (Keyboard.current?.spaceKey.wasPressedThisFrame == true && !m_IsPlaying)
		{
			StartGame();
		}
	}

	// Public Methods
	/// <summary>
	/// 게임을 시작합니다.
	/// </summary>
	public void StartGame()
	{
		if (m_CurrentNoteData == null)
		{
			Debug.LogError("❌ No note data loaded! Check Resources/Music folder.");
			return;
		}

		// FMOD 음악 재생 (필수)
		if (m_MusicEventPath.IsNull)
		{
			Debug.LogError("❌ FMOD Event Path is required! Cannot start game.");
			return;
		}

		try
		{
			m_MusicInstance = RuntimeManager.CreateInstance(m_MusicEventPath);
			m_MusicInstance.start();
			Debug.Log($"🎵 Started FMOD music: {m_MusicEventPath}");
		}
		catch (System.Exception e)
		{
			Debug.LogError($"❌ Failed to start FMOD music: {e.Message}");
			Debug.LogError("❌ Game cannot start without FMOD working properly.");
			return;
		}

		m_IsPlaying = true;
		currentTime = 0f;
		m_NextNoteIndex = 0;
		m_NextSectionIndex = 0;
		m_Score = 0;

		Debug.Log($"✅ Rhythm game started! Notes: {m_CurrentNoteData.notes.Count}, Vocal sections: {m_CurrentVocalSections.Count}");
	}

	/// <summary>
	/// 게임을 정지합니다.
	/// </summary>
	public void StopGame()
	{
		m_IsPlaying = false;

		// FMOD 음악 정지
		if (m_MusicInstance.isValid())
		{
			m_MusicInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
			m_MusicInstance.release();
			Debug.Log("Stopped FMOD music");
		}

		ClearAllNotes();
		ClearAllSectionLines();

		// UI에 게임 종료 알림
		MobileRhythmUI ui = FindFirstObjectByType<MobileRhythmUI>();
		if (ui != null)
		{
			ui.ShowGameOver(m_Score);
		}

		Debug.Log($"Game ended! Final Score: {m_Score}");
	}

	/// <summary>
	/// 현재 점수를 반환합니다.
	/// </summary>
	/// <returns>현재 점수</returns>
	public int GetCurrentScore() => m_Score;

	/// <summary>
	/// 난이도 레벨을 설정합니다.
	/// </summary>
	/// <param name="level">난이도 레벨 (1-3)</param>
	public void SetDifficultyLevel(int level)
	{
		if (level < 1 || level > 3)
		{
			Debug.LogWarning($"❌ Invalid difficulty level: {level}. Must be 1-3.");
			return;
		}

		m_DifficultyLevel = level;
		Debug.Log($"🎯 Difficulty level set to: {level} ({GetDifficultyName(level)})");

		// 게임이 진행 중이 아니면 노트 데이터 다시 로드
		if (!m_IsPlaying)
		{
			LoadMusicAndNotes();
		}
	}

	/// <summary>
	/// 난이도 레벨 이름을 반환합니다.
	/// </summary>
	/// <param name="level">난이도 레벨</param>
	/// <returns>난이도 이름</returns>
	public string GetDifficultyName(int level)
	{
		switch (level)
		{
			case 1: return "Easy";
			case 2: return "Medium";
			case 3: return "Hard";
			default: return "Unknown";
		}
	}

	/// <summary>
	/// 현재 난이도 레벨을 반환합니다.
	/// </summary>
	/// <returns>현재 난이도 레벨</returns>
	public int GetDifficultyLevel() => m_DifficultyLevel;

	/// <summary>
	/// 오디오 지연 보정값을 설정합니다.
	/// </summary>
	/// <param name="offsetSeconds">보정값 (초 단위)</param>
	public void SetAudioLatencyOffset(float offsetSeconds)
	{
		m_AudioLatencyOffset = offsetSeconds;
		Debug.Log($"🎚️ Audio latency offset set to: {offsetSeconds * 1000:F1}ms");
	}

	/// <summary>
	/// 현재 오디오 지연 보정값을 반환합니다.
	/// </summary>
	/// <returns>오디오 지연 보정값</returns>
	public float GetAudioLatencyOffset() => m_AudioLatencyOffset;

	/// <summary>
	/// 캘리브레이션 모드를 시작합니다.
	/// </summary>
	public void StartCalibrationMode()
	{
		Debug.Log("🎚️ Starting calibration mode - adjust offset until audio feels in sync");
		// 캘리브레이션 UI나 테스트 패턴 시작 가능
	}

	// Private Methods
	/// <summary>
	/// FMOD 사용 가능성을 확인합니다.
	/// </summary>
	/// <returns>FMOD 사용 가능 여부</returns>
	private bool CheckFMODAvailability()
	{
		try
		{
			// FMOD Studio 시스템이 초기화되었는지 확인
			if (!RuntimeManager.HasBankLoaded("Master"))
			{
				Debug.LogError("❌ FMOD Master Bank not loaded!");
				Debug.LogError("💡 Make sure FMOD is properly configured and banks are built.");
				return false;
			}

			if (m_MusicEventPath.IsNull)
			{
				Debug.LogError("❌ FMOD Event Path is not set in Inspector!");
				Debug.LogError("💡 Please assign a valid FMOD event to 'Music Event Path'.");
				return false;
			}

			Debug.Log("✅ FMOD is available and ready.");
			return true;
		}
		catch (System.Exception e)
		{
			Debug.LogError($"❌ FMOD availability check failed: {e.Message}");
			return false;
		}
	}

	/// <summary>
	/// 필수 컴포넌트들을 설정합니다.
	/// </summary>
	private void SetupRequiredComponents()
	{
		// NotePrefab 미리 생성
		if (m_NotePrefab == null)
		{
			Debug.Log("🔧 Creating default note prefab...");
			CreateDefaultNotePrefab();
		}

		// Canvas 미리 찾기
		if (m_GameCanvas == null)
		{
			m_GameCanvas = FindFirstObjectByType<Canvas>();
			if (m_GameCanvas == null)
			{
				Debug.LogError("❌ No Canvas found in scene!");
			}
			else
			{
				Debug.Log("✅ Canvas found and assigned");
			}
		}

		// CenterTarget 미리 설정
		if (m_CenterTarget == null)
		{
			var go = new GameObject("CenterTarget");
			go.transform.SetParent(m_GameCanvas.transform);
			m_CenterTarget = go.transform;
			var rectTransform = go.AddComponent<RectTransform>();
			rectTransform.anchoredPosition = Vector2.zero;
			rectTransform.sizeDelta = new Vector2(100, 100); // 노트 타겟 크기와 맞춤

			var image = go.AddComponent<Image>();
			image.color = new Color(1f, 0.5f, 0f, 0.9f); // 주황색으로 명확한 타이밍 표시
			image.sprite = CreateDefaultCircleSprite();

			// 중앙 타겟 펄스 효과로 타이밍 강조
			image.transform.DOScale(1.1f, 0.8f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);

			Debug.Log("✅ CenterTarget created and positioned");
		}
	}

	/// <summary>
	/// 음악과 노트 데이터를 로드합니다.
	/// </summary>
	private void LoadMusicAndNotes()
	{
		// Resources 폴더에서 JSON 파일 로드
		string resourcePath = "Music/" + m_MusicFileName;
		Debug.Log($"🔍 Trying to load: {resourcePath}");

		TextAsset jsonAsset = Resources.Load<TextAsset>(resourcePath);

		if (jsonAsset != null)
		{
			Debug.Log($"✅ JSON file loaded successfully: {jsonAsset.name}");
			try
			{
				var fullData = JsonUtility.FromJson<FullNoteDataWithSections>(jsonAsset.text);

				// 난이도에 따른 노트 필터링
				var filteredNotes = FilterNotesByDifficulty(fullData.notes, m_DifficultyLevel);
				m_CurrentNoteData = new NoteData { notes = filteredNotes };

				// 보컬 구역 정보 로드
				m_CurrentVocalSections = fullData.vocal_sections ?? new List<VocalSection>();

				Debug.Log($"✅ Loaded {fullData.notes.Count} total notes, filtered to {m_CurrentNoteData.notes.Count} for difficulty level {m_DifficultyLevel}");
				Debug.Log($"✅ Loaded {m_CurrentVocalSections.Count} vocal sections");
			}
			catch (System.Exception e)
			{
				Debug.LogError($"❌ Failed to parse JSON: {e.Message}");
			}
		}
		else
		{
			Debug.LogError($"❌ Note file not found: Resources/{resourcePath}");
			Debug.LogError($"❌ Make sure disco-train.json exists in Assets/Resources/Music/");

			// 모든 Resources/Music 파일 목록 출력
			TextAsset[] allAssets = Resources.LoadAll<TextAsset>("Music");
			Debug.Log($"📁 Found {allAssets.Length} files in Resources/Music:");
			foreach (var asset in allAssets)
			{
				Debug.Log($"   - {asset.name}");
			}
		}

		// FMOD 이벤트 경로 필수 확인
		if (m_MusicEventPath.IsNull)
		{
			Debug.LogError("❌ FMOD Event Path is required! Game cannot start without FMOD.");
			Debug.LogError("💡 Please set 'Music Event Path' in Inspector and configure FMOD properly.");
			enabled = false; // 컴포넌트 비활성화
			return;
		}
	}

	/// <summary>
	/// 난이도에 따라 노트를 필터링합니다.
	/// </summary>
	/// <param name="allNotes">전체 노트 목록</param>
	/// <param name="maxLevel">최대 레벨</param>
	/// <returns>필터링된 노트 목록</returns>
	private List<Note> FilterNotesByDifficulty(List<Note> allNotes, int maxLevel)
	{
		if (allNotes == null) return new List<Note>();

		var filteredNotes = new List<Note>();

		foreach (var note in allNotes)
		{
			int noteLevel = note.level > 0 ? note.level : 1;

			if (noteLevel <= maxLevel)
			{
				filteredNotes.Add(note);
			}
		}

		Debug.Log($"🎯 Difficulty filtering: {allNotes.Count} → {filteredNotes.Count} notes (max level: {maxLevel})");
		return filteredNotes;
	}

	/// <summary>
	/// 게임을 리셋합니다.
	/// </summary>
	private void ResetGame()
	{
		m_Score = 0;
		currentTime = 0f;
		m_NextNoteIndex = 0;
		m_NextSectionIndex = 0;
		m_IsPlaying = false;

		ClearAllNotes();
		ClearAllSectionLines();
	}

	/// <summary>
	/// 게임 시간을 업데이트합니다.
	/// </summary>
	private void UpdateGameTime()
	{
		// FMOD 타임라인만 사용 (필수)
		if (!m_MusicInstance.isValid())
		{
			Debug.LogError("❌ FMOD music instance is invalid! Stopping game.");
			StopGame();
			return;
		}

		m_MusicInstance.getPlaybackState(out FMOD.Studio.PLAYBACK_STATE playbackState);

		if (playbackState == FMOD.Studio.PLAYBACK_STATE.PLAYING)
		{
			// FMOD의 실제 재생 위치를 밀리초로 가져와서 초로 변환
			m_MusicInstance.getTimelinePosition(out int position);
			float rawTime = position / 1000f; // 밀리초를 초로 변환

			// 오디오 지연 보정 적용
			currentTime = rawTime + m_AudioLatencyOffset;
		}
		else if (playbackState == FMOD.Studio.PLAYBACK_STATE.STOPPED && currentTime > 2f)
		{
			Debug.Log("🎵 FMOD music finished, stopping game.");
			StopGame();
		}
		else if (playbackState == FMOD.Studio.PLAYBACK_STATE.STOPPING)
		{
			// 음악이 끝나가고 있음
			m_MusicInstance.getTimelinePosition(out int position);
			float rawTime = position / 1000f;
			currentTime = rawTime + m_AudioLatencyOffset;
		}
	}

	/// <summary>
	/// 노트를 스폰합니다.
	/// </summary>
	private void SpawnNotes()
	{
		if (m_CurrentNoteData == null || m_CurrentNoteData.notes == null)
		{
			Debug.LogError("❌ Cannot spawn notes - no note data!");
			return;
		}

		float spawnTime = 2f; // 2초 전에 스폰

		while (m_NextNoteIndex < m_CurrentNoteData.notes.Count)
		{
			Note note = m_CurrentNoteData.notes[m_NextNoteIndex];

			if (note.time_seconds - currentTime <= spawnTime)
			{
				Debug.Log($"🎯 Attempting to spawn note {m_NextNoteIndex} at time {note.time_seconds}s");
				SpawnNote(note);
				m_NextNoteIndex++;
			}
			else
			{
				break;
			}
		}
	}

	/// <summary>
	/// 단일 노트를 스폰합니다.
	/// </summary>
	/// <param name="note">스폰할 노트</param>
	private void SpawnNote(Note note)
	{
		// 필수 컴포넌트 확인
		if (m_NotePrefab == null || m_GameCanvas == null || m_CenterTarget == null)
		{
			Debug.LogError("❌ Required components not set up! Skipping note spawn.");
			return;
		}

		GameObject noteObj = Instantiate(m_NotePrefab, m_GameCanvas.transform);

		// 세로 라인 노트 설정
		var controller = noteObj.GetComponent<LineNoteController>() ?? noteObj.AddComponent<LineNoteController>();
		controller.Initialize(note, this, m_GameCanvas, m_CenterTarget);

		m_ActiveNotes.Add(noteObj);

		Debug.Log($"📝 Spawned note at time: {note.time_seconds}s, intensity: {note.intensity}");
	}

	/// <summary>
	/// 섹션 라인을 스폰합니다.
	/// </summary>
	private void SpawnSectionLines()
	{
		if (m_CurrentVocalSections == null || m_CurrentVocalSections.Count == 0)
		{
			return;
		}

		float spawnTime = 3f; // 3초 전에 스폰

		while (m_NextSectionIndex < m_CurrentVocalSections.Count)
		{
			VocalSection section = m_CurrentVocalSections[m_NextSectionIndex];

			if (section.start_time - currentTime <= spawnTime)
			{
				Debug.Log($"🎤 Attempting to spawn section line: {section.type} at {section.start_time}s");
				SpawnSectionLine(section);
				m_NextSectionIndex++;
			}
			else
			{
				break;
			}
		}
	}

	/// <summary>
	/// 단일 섹션 라인을 스폰합니다.
	/// </summary>
	/// <param name="section">스폰할 섹션</param>
	private void SpawnSectionLine(VocalSection section)
	{
		if (m_GameCanvas == null || m_CenterTarget == null)
		{
			Debug.LogError("❌ Required components not set up! Skipping section line spawn.");
			return;
		}

		if (m_SectionLinePrefab == null)
		{
			Debug.Log("🔧 Creating default section line prefab...");
			CreateDefaultSectionLinePrefab();
		}

		GameObject sectionLineObj = Instantiate(m_SectionLinePrefab, m_GameCanvas.transform);
		sectionLineObj.transform.SetSiblingIndex(0);

		var controller = sectionLineObj.GetComponent<SectionLineController>() ?? sectionLineObj.AddComponent<SectionLineController>();
		controller.Initialize(section, this, m_GameCanvas, m_CenterTarget);

		m_ActiveSectionLines.Add(sectionLineObj);

		Debug.Log($"🎨 Spawned section line: {section.type} ({section.start_time}s - {section.end_time}s)");
	}

	/// <summary>
	/// 노트들을 업데이트합니다.
	/// </summary>
	private void UpdateNotes()
	{
		for (int i = m_ActiveNotes.Count - 1; i >= 0; i--)
		{
			var noteObj = m_ActiveNotes[i];
			if (noteObj == null)
			{
				m_ActiveNotes.RemoveAt(i);
				continue;
			}

			var controller = noteObj.GetComponent<LineNoteController>();
			if (controller?.IsExpired() == true)
			{
				m_ActiveNotes.RemoveAt(i);
				Destroy(noteObj);
			}
		}
	}

	/// <summary>
	/// 섹션 라인들을 업데이트합니다.
	/// </summary>
	private void UpdateSectionLines()
	{
		for (int i = m_ActiveSectionLines.Count - 1; i >= 0; i--)
		{
			var sectionLineObj = m_ActiveSectionLines[i];
			if (sectionLineObj == null)
			{
				m_ActiveSectionLines.RemoveAt(i);
				continue;
			}

			var controller = sectionLineObj.GetComponent<SectionLineController>();
			if (controller?.IsExpired() == true)
			{
				m_ActiveSectionLines.RemoveAt(i);
				Destroy(sectionLineObj);
			}
		}
	}

	/// <summary>
	/// 입력을 확인합니다.
	/// </summary>
	private void CheckInput()
	{
		bool inputPressed = false;
		float inputTime = currentTime;

		if (Mouse.current?.leftButton.wasPressedThisFrame == true)
			inputPressed = true;

		if (Keyboard.current?.spaceKey.wasPressedThisFrame == true)
			inputPressed = true;

		if (Touchscreen.current != null)
		{
			foreach (var touch in Touchscreen.current.touches)
			{
				if (touch.press.wasPressedThisFrame)
				{
					inputPressed = true;
					break;
				}
			}
		}

		if (inputPressed)
		{
			CheckNoteHit(inputTime);
		}
	}

	/// <summary>
	/// 노트 히트를 확인합니다.
	/// </summary>
	/// <param name="inputTime">입력 시간</param>
	private void CheckNoteHit(float inputTime)
	{
		LineNoteController closestNote = null;
		float closestTimeDiff = float.MaxValue;

		foreach (GameObject noteObj in m_ActiveNotes)
		{
			LineNoteController controller = noteObj.GetComponent<LineNoteController>();
			if (controller != null)
			{
				float timeDiff = controller.GetTimeDifference(inputTime);

				if (Mathf.Abs(timeDiff) < m_HitWindow && Mathf.Abs(timeDiff) < closestTimeDiff)
				{
					closestNote = controller;
					closestTimeDiff = Mathf.Abs(timeDiff);
				}
			}
		}

		if (closestNote != null)
		{
			string accuracy = GetAccuracyGrade(closestTimeDiff);
			OnNoteHit(closestNote, accuracy, closestTimeDiff);
			m_ActiveNotes.Remove(closestNote.gameObject);
			Destroy(closestNote.gameObject);
		}
	}

	/// <summary>
	/// 정확도 등급을 계산합니다.
	/// </summary>
	/// <param name="timeDiff">시간 차이</param>
	/// <returns>정확도 등급</returns>
	private string GetAccuracyGrade(float timeDiff)
	{
		if (timeDiff <= 0.015f) return "PERFECT";
		if (timeDiff <= 0.030f) return "GREAT";
		if (timeDiff <= 0.050f) return "GOOD";
		return "OK";
	}

	/// <summary>
	/// 노트 히트 처리를 합니다.
	/// </summary>
	/// <param name="note">히트된 노트</param>
	/// <param name="accuracy">정확도</param>
	/// <param name="timeDiff">시간 차이</param>
	private void OnNoteHit(LineNoteController note, string accuracy, float timeDiff)
	{
		int baseScore = accuracy switch
		{
			"PERFECT" => 300,
			"GREAT" => 200,
			"GOOD" => 100,
			"OK" => 50,
			_ => 0
		};

		m_Score += baseScore;

		Debug.Log($"🎯 Hit! Accuracy: {accuracy} ({timeDiff * 1000:F1}ms), Score: +{baseScore}");

		if (m_CenterTarget != null)
		{
			m_CenterTarget.DOKill();
			m_CenterTarget.DOPunchScale(Vector3.one * 0.2f, 0.3f, 5, 0.5f);
		}

		note.transform.DOScale(0f, 0.2f).SetEase(Ease.InBack);
		note.GetComponent<Image>().DOFade(0f, 0.2f);

		if (AudioManager.Instance != null)
		{
			AudioManager.Instance.PlayClickSound();
		}
		else if (!m_HitSoundEventPath.IsNull)
		{
			RuntimeManager.PlayOneShot(m_HitSoundEventPath);
		}

		MobileRhythmUI ui = FindFirstObjectByType<MobileRhythmUI>();
		if (ui != null)
		{
			ui.UpdateScore(m_Score);
		}

		if (m_ScoreText != null)
		{
			m_ScoreText.text = "Score: " + m_Score;
			m_ScoreText.transform.DOKill();
			m_ScoreText.transform.DOPunchScale(Vector3.one * 0.1f, 0.2f, 3, 0.3f);
		}

		Debug.Log($"Hit! Score: {m_Score}");
	}

	/// <summary>
	/// 모든 노트를 제거합니다.
	/// </summary>
	private void ClearAllNotes()
	{
		foreach (GameObject noteObj in m_ActiveNotes)
		{
			if (noteObj != null)
				Destroy(noteObj);
		}
		m_ActiveNotes.Clear();
	}

	/// <summary>
	/// 모든 섹션 라인을 제거합니다.
	/// </summary>
	private void ClearAllSectionLines()
	{
		foreach (GameObject sectionLineObj in m_ActiveSectionLines)
		{
			if (sectionLineObj != null)
				Destroy(sectionLineObj);
		}
		m_ActiveSectionLines.Clear();
	}

	/// <summary>
	/// 기본 노트 프리팹을 생성합니다.
	/// </summary>
	private void CreateDefaultNotePrefab()
	{
		m_NotePrefab = new GameObject("DefaultLineNote");
		var image = m_NotePrefab.AddComponent<Image>();
		var rectTransform = m_NotePrefab.GetComponent<RectTransform>();

		image.sprite = CreateDefaultLineSprite();
		image.color = Color.cyan;
		rectTransform.sizeDelta = new Vector2(8, 200);

		Debug.Log("✅ Created default line note prefab");
	}

	/// <summary>
	/// 기본 섹션 라인 프리팹을 생성합니다.
	/// </summary>
	private void CreateDefaultSectionLinePrefab()
	{
		m_SectionLinePrefab = new GameObject("DefaultSectionLine");
		var image = m_SectionLinePrefab.AddComponent<Image>();
		var rectTransform = m_SectionLinePrefab.GetComponent<RectTransform>();

		image.color = new Color(1f, 1f, 1f, 0.5f);
		image.sprite = CreateDefaultHorizontalLineSprite();
		rectTransform.sizeDelta = new Vector2(1200, 1);

		Debug.Log("✅ Created default section line prefab");
	}

	/// <summary>
	/// 기본 라인 스프라이트를 생성합니다.
	/// </summary>
	/// <returns>라인 스프라이트</returns>
	private static Sprite CreateDefaultLineSprite()
	{
		const int WIDTH = 8;
		const int HEIGHT = 64;

		var texture = new Texture2D(WIDTH, HEIGHT);
		var colors = new Color[WIDTH * HEIGHT];

		for (int i = 0; i < colors.Length; i++)
		{
			colors[i] = Color.white;
		}

		texture.SetPixels(colors);
		texture.Apply();

		return Sprite.Create(texture, new Rect(0, 0, WIDTH, HEIGHT), new Vector2(0.5f, 0.5f));
	}

	/// <summary>
	/// 기본 가로 라인 스프라이트를 생성합니다.
	/// </summary>
	/// <returns>가로 라인 스프라이트</returns>
	private static Sprite CreateDefaultHorizontalLineSprite()
	{
		const int WIDTH = 64;
		const int HEIGHT = 1;

		var texture = new Texture2D(WIDTH, HEIGHT);
		var colors = new Color[WIDTH * HEIGHT];

		for (int i = 0; i < colors.Length; i++)
		{
			colors[i] = Color.white;
		}

		texture.SetPixels(colors);
		texture.Apply();

		return Sprite.Create(texture, new Rect(0, 0, WIDTH, HEIGHT), new Vector2(0.5f, 0.5f));
	}

	/// <summary>
	/// 기본 원형 스프라이트를 생성합니다.
	/// </summary>
	/// <returns>원형 스프라이트</returns>
	private static Sprite CreateDefaultCircleSprite()
	{
		var texture = new Texture2D(64, 64);
		var center = new Vector2(32, 32);
		var colors = new Color[64 * 64];

		for (int i = 0; i < colors.Length; i++)
		{
			int x = i % 64;
			int y = i / 64;
			float distance = Vector2.Distance(new Vector2(x, y), center);

			colors[i] = (distance <= 30 && distance >= 25) ? Color.white : Color.clear;
		}

		texture.SetPixels(colors);
		texture.Apply();

		return Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
	}
}
