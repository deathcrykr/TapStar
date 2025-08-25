using System.Collections.Generic;
using DG.Tweening;
using FMODUnity;
using TapStar.Controller;
using TapStar.Manager;
using TapStar.Structs;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Obvious.Soap;

#if UNITY_EDITOR
using TapStar.Editors;
#endif

namespace TapStar
{
	/// <summary>
	/// 리듬 게임의 핵심 로직을 관리하는 매니저 클래스
	/// </summary>
	public class GameManager : MonoBehaviour
	{
		public static GameManager Instance { get; private set; }
		[Header("Soap Variable")]
		[SerializeField] private BoolVariable _isIntroPage;

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
		[Tooltip("Note 생성 영역")]
		[SerializeField] private Transform m_NoteSpawnArea;
		public Transform NoteSpawnArea { get => m_NoteSpawnArea; set => m_NoteSpawnArea = value; }

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

		[Header("Clicker Rewards")]
		[Tooltip("기본 클릭 보상")]
		[SerializeField] private int m_BaseClickReward = 10;
		public int BaseClickReward { get => m_BaseClickReward; set => m_BaseClickReward = value; }

		[Header("Clicker Control")]
		[Tooltip("클릭 가능 상태 (기본값: true)")]
		[SerializeField] private bool m_IsClickEnabled = true;
		public bool IsClickEnabled { get => m_IsClickEnabled; set => m_IsClickEnabled = value; }

		public float currentTime = 0f; // 현재 게임 시간
		public bool IsPlaying => m_IsPlaying; // 게임 실행 상태

		// Static resources (캐싱으로 메모리 효율 개선)
		/// <summary>캐시된 라인 스프라이트</summary>
		private static Sprite s_LineSprite;
		/// <summary>캐시된 원 스프라이트</summary>
		private static Sprite s_CircleSprite;
		/// <summary>캐시된 수평 라인 스프라이트</summary>
		private static Sprite s_HorizontalLineSprite;
		/// <summary>타이밍별 점수 배율</summary>
		private static readonly Dictionary<string, float> s_TimingMultipliers = new Dictionary<string, float>
		{
			{ "PERFECT", 6f },
			{ "NICE", 4f },
			{ "GOOD", 3f },
			{ "BAD", 2f },
			{ "BASIC", 1f }
		};

		// Game state
		/// <summary>FMOD 음악 인스턴스</summary>
		private FMOD.Studio.EventInstance m_MusicInstance;
		/// <summary>현재 로드된 노트 데이터</summary>
		private NoteData m_CurrentNoteData;
		/// <summary>현재 보컬 섹션 데이터</summary>
		private List<VocalSection> m_CurrentVocalSections = new List<VocalSection>();
		/// <summary>활성화된 노트 오브젝트들</summary>
		private List<GameObject> m_ActiveNotes = new List<GameObject>();
		/// <summary>활성화된 섹션 라인 오브젝트들</summary>
		private List<GameObject> m_ActiveSectionLines = new List<GameObject>();
		/// <summary>다음 생성할 노트 인덱스</summary>
		private int m_NextNoteIndex = 0;
		/// <summary>다음 생성할 섹션 인덱스</summary>
		private int m_NextSectionIndex = 0;
		/// <summary>게임 실행 여부</summary>
		private bool m_IsPlaying = false;
		/// <summary>마지막 히트 사운드 재생 시간</summary>
		private float m_LastHitSoundTime = 0f;

		// Cached references (성능 최적화)
		/// <summary>중앙 타겟의 RectTransform 캐시</summary>
		private RectTransform m_CenterRectTransform;
		private RectTransform m_NoteSpawnAreaTransform;

		private bool m_IsIntro = true;

		// Input cache (매 프레임 new 방지)
		/// <summary>노트 컨트롤러 캐시 리스트</summary>
		private readonly List<LineNoteController> m_NoteControllersCache = new List<LineNoteController>(32);

		/// <summary>
		/// 싱글톤 패턴 구현 - GameManager 인스턴스 초기화
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
		/// 게임 매니저 초기화 - FMOD 체크, 컴포넌트 설정, 노트 데이터 로드
		/// </summary>
		private void Start()
		{
			if (!CheckFMODAvailability())
			{
				Debug.LogError("❌ FMOD is not available! GameManager disabled.");
				enabled = false;
				return;
			}

			if (_isIntroPage != null)
			{
				_isIntroPage.OnValueChanged += OnCheckIntroPage;
			}

			InitializeComponents();
			LoadMusicAndNotes();

			// 자동 시작은 옵션으로 (필요시 주석 해제)
			// Invoke(nameof(StartGame), 3f);
		}

		/// <summary>
		/// 매 프레임 게임 로직 업데이트 - 입력 처리, 시간 업데이트, 노트 생성/업데이트
		/// </summary>
		private void Update()
		{
			if (m_IsIntro) return;

			CheckNoteInput();

			if (!m_IsPlaying) return;

			UpdateGameTime();
			SpawnNotes();
			UpdateNotes();

#if UNITY_EDITOR
			if (m_ShowSectionLines)
			{
				SpawnSectionLines();
				UpdateSectionLines();
			}
#endif
		}

		private void OnDestroy()
		{
			if (_isIntroPage != null)
			{
				_isIntroPage.OnValueChanged -= OnCheckIntroPage;
			}
		}

		/// <summary>
		/// 게임을 시작합니다
		/// </summary>
		public void StartGame()
		{
			if (m_IsPlaying)
			{
				Debug.LogWarning("⚠️ Game is already playing!");
				return;
			}

			if (m_CurrentNoteData == null)
			{
				Debug.LogError("❌ No note data loaded!");
				return;
			}

			if (m_MusicEventPath.IsNull)
			{
				Debug.LogError("❌ FMOD Event Path is required!");
				return;
			}

			// 기존 인스턴스 정리
			if (m_MusicInstance.isValid())
			{
				m_MusicInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
				m_MusicInstance.release();
			}

			try
			{
				m_MusicInstance = RuntimeManager.CreateInstance(m_MusicEventPath);
				m_MusicInstance.start();
			}
			catch (System.Exception e)
			{
				Debug.LogError($"❌ Failed to start FMOD music: {e.Message}");
				return;
			}

			ResetGameState();
			m_IsPlaying = true;
		}

		/// <summary>
		/// 게임을 정지합니다
		/// </summary>
		public void StopGame()
		{
			m_IsPlaying = false;

			if (m_MusicInstance.isValid())
			{
				m_MusicInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
				m_MusicInstance.release();
			}

			ClearAllGameObjects();
		}

		/// <summary>
		/// 난이도 레벨을 설정합니다
		/// </summary>
		/// <summary>
		/// 난이도 레벨 설정 (1=Easy, 2=Medium, 3=Hard)
		/// </summary>
		/// <param name="level">설정할 난이도 레벨</param>
		public void SetDifficultyLevel(int level)
		{
			if (level < 1 || level > 3)
			{
				Debug.LogWarning($"❌ Invalid difficulty level: {level}");
				return;
			}

			m_DifficultyLevel = level;

			if (!m_IsPlaying)
			{
				LoadMusicAndNotes();
			}
		}

		/// <summary>오디오 지연 보정값 설정</summary>
		/// <param name="offsetSeconds">지연 보정값 (초)</param>
		public void SetAudioLatencyOffset(float offsetSeconds) => m_AudioLatencyOffset = offsetSeconds;

		/// <summary>클릭 기능 활성화</summary>
		public void EnableClicking() => IsClickEnabled = true;

		/// <summary>클릭 기능 비활성화</summary>
		public void DisableClicking() => IsClickEnabled = false;

		/// <summary>클릭 기능 토글</summary>
		public void ToggleClicking() => IsClickEnabled = !IsClickEnabled;

		/// <summary>
		/// FMOD 시스템 사용 가능 여부 확인
		/// </summary>
		/// <returns>FMOD 사용 가능 여부</returns>
		private bool CheckFMODAvailability()
		{
			try
			{
				if (!RuntimeManager.HasBankLoaded("Master"))
				{
					Debug.LogError("❌ FMOD Master Bank not loaded!");
					return false;
				}

				if (m_MusicEventPath.IsNull)
				{
					Debug.LogError("❌ FMOD Event Path is not set!");
					return false;
				}

				return true;
			}
			catch (System.Exception e)
			{
				Debug.LogError($"❌ FMOD check failed: {e.Message}");
				return false;
			}
		}

		/// <summary>
		/// 게임 컴포넌트들 초기화 - 캔버스, 타겟, 프리팹 설정
		/// </summary>
		private void InitializeComponents()
		{
			// Canvas 설정 및 캐싱
			if (m_GameCanvas == null)
			{
				m_GameCanvas = FindFirstObjectByType<Canvas>();
			}


			// CenterTarget 설정 및 캐싱
			if (m_CenterTarget == null)
			{
				CreateCenterTarget();
			}
			else
			{
				m_CenterRectTransform = m_CenterTarget.GetComponent<RectTransform>();
			}

			if (m_NoteSpawnArea != null)
			{
				m_NoteSpawnAreaTransform = m_NoteSpawnArea.GetComponent<RectTransform>();

				Vector2 centerPosition = m_CenterRectTransform.anchoredPosition;
				centerPosition.x = m_NoteSpawnAreaTransform.anchoredPosition.x;
				m_NoteSpawnAreaTransform.anchoredPosition = centerPosition;
			}

			// NotePrefab 설정
			if (m_NotePrefab == null)
			{
				CreateDefaultNotePrefab();
			}

#if UNITY_EDITOR
			if (m_SectionLinePrefab == null)
			{
				CreateDefaultSectionLinePrefab();
			}
#endif
		}

		/// <summary>
		/// 중앙 타겟 오브젝트 생성 및 설정
		/// </summary>
		private void CreateCenterTarget()
		{
			var go = new GameObject("CenterTarget");
			go.transform.SetParent(m_GameCanvas.transform);
			m_CenterTarget = go.transform;

			m_CenterRectTransform = go.AddComponent<RectTransform>();
			m_CenterRectTransform.anchoredPosition = Vector2.zero;
			m_CenterRectTransform.sizeDelta = new Vector2(100, 100);

			var image = go.AddComponent<Image>();
			image.color = new Color(1f, 0.5f, 0f, 0.9f);
			image.sprite = GetOrCreateCircleSprite();

			// 펄스 효과
			image.transform.DOScale(1.1f, 0.8f)
				.SetLoops(-1, LoopType.Yoyo)
				.SetEase(Ease.InOutSine);
		}

		/// <summary>
		/// 음악 파일과 노트 데이터 로드 및 파싱
		/// </summary>
		private void LoadMusicAndNotes()
		{
			string resourcePath = $"Music/{m_MusicFileName}";
			TextAsset jsonAsset = Resources.Load<TextAsset>(resourcePath);

			if (jsonAsset == null)
			{
				Debug.LogError($"❌ Note file not found: Resources/{resourcePath}");
				return;
			}

			try
			{
				var fullData = JsonUtility.FromJson<FullNoteDataWithSections>(jsonAsset.text);
				var filteredNotes = FilterNotesByDifficulty(fullData.Notes, m_DifficultyLevel);
				m_CurrentNoteData = new NoteData { Notes = filteredNotes };
				m_CurrentVocalSections = fullData.VocalSections ?? new List<VocalSection>();
			}
			catch (System.Exception e)
			{
				Debug.LogError($"❌ Failed to parse JSON: {e.Message}");
			}
		}

		/// <summary>
		/// 난이도에 따른 노트 필터링
		/// </summary>
		/// <param name="allNotes">전체 노트 리스트</param>
		/// <param name="maxLevel">최대 난이도 레벨</param>
		/// <returns>필터링된 노트 리스트</returns>
		private List<Note> FilterNotesByDifficulty(List<Note> allNotes, int maxLevel)
		{
			if (allNotes == null) return new List<Note>();

			var filtered = new List<Note>(allNotes.Count);
			foreach (var note in allNotes)
			{
				int level = note.Level > 0 ? note.Level : 1;
				if (level <= maxLevel)
				{
					filtered.Add(note);
				}
			}
			return filtered;
		}

		/// <summary>
		/// 게임 상태 초기화 - 점수, 시간, 인덱스 리셋
		/// </summary>
		private void ResetGameState()
		{
			m_Score = 0;
			currentTime = 0f;
			m_NextNoteIndex = 0;
			m_NextSectionIndex = 0;
			ClearAllGameObjects();
		}

		/// <summary>
		/// 게임 시간 업데이트 - FMOD 재생 시간 동기화 및 상태 체크
		/// </summary>
		private void UpdateGameTime()
		{
			if (!m_MusicInstance.isValid())
			{
				Debug.LogError("❌ FMOD music instance is invalid!");
				StopGame();
				return;
			}

			m_MusicInstance.getPlaybackState(out FMOD.Studio.PLAYBACK_STATE playbackState);

			if (playbackState == FMOD.Studio.PLAYBACK_STATE.PLAYING)
			{
				m_MusicInstance.getTimelinePosition(out int position);
				currentTime = (position / 1000f) + m_AudioLatencyOffset;
			}
			else if (playbackState == FMOD.Studio.PLAYBACK_STATE.STOPPED && currentTime > 2f)
			{
				StopGame();
			}
		}

		/// <summary>
		/// 노트 생성 로직 - 시간에 맞춰 노트들을 화면에 스폰
		/// </summary>
		private void SpawnNotes()
		{
			const float NOTE_SPAWN_TIME = 2f; // 노트 생성 시점 (초)

			if (m_CurrentNoteData?.Notes == null) return;

			while (m_NextNoteIndex < m_CurrentNoteData.Notes.Count)
			{
				Note note = m_CurrentNoteData.Notes[m_NextNoteIndex];

				if (note.TimeSeconds - currentTime > NOTE_SPAWN_TIME) break;

				SpawnNote(note);
				m_NextNoteIndex++;
			}
		}

		/// <summary>
		/// 개별 노트 오브젝트 생성 및 초기화
		/// </summary>
		/// <param name="note">생성할 노트 데이터</param>
		private void SpawnNote(Note note)
		{
			if (m_NotePrefab == null || m_GameCanvas == null || m_CenterTarget == null) return;

			GameObject noteObj = Instantiate(m_NotePrefab, m_GameCanvas.transform);
			noteObj.transform.SetParent(m_NoteSpawnArea.transform);
			LineNoteController controller = noteObj.GetComponent<LineNoteController>() ?? noteObj.AddComponent<LineNoteController>();

			controller.Initialize(note, this, m_GameCanvas, m_CenterTarget);
			m_ActiveNotes.Add(noteObj);
		}

		/// <summary>
		/// 활성화된 노트들 상태 업데이트 및 만료된 노트 제거
		/// </summary>
		private void UpdateNotes()
		{
			// 역순으로 순회하며 제거 (RemoveAt 효율성)
			for (int i = m_ActiveNotes.Count - 1; i >= 0; i--)
			{
				var noteObj = m_ActiveNotes[i];
				if (noteObj == null)
				{
					m_ActiveNotes.RemoveAt(i);
					continue;
				}

				// GetComponent 최소화 - 필요한 경우만
				var controller = noteObj.GetComponent<LineNoteController>();
				if (controller != null && controller.IsExpired())
				{
					m_ActiveNotes.RemoveAt(i);
					Destroy(noteObj);
				}
			}
		}

		/// <summary>
		/// 사용자 입력 감지 - 키보드, 마우스, 터치 입력 처리
		/// </summary>
		private void CheckNoteInput()
		{
			if (!m_IsClickEnabled) return;

			Vector3 inputPosition = Vector3.zero;
			bool inputPressed = false;

			// 키보드 입력 (스크린 중앙으로 설정)
			if (Keyboard.current?.spaceKey.wasPressedThisFrame == true)
			{
				inputPressed = true;
				inputPosition = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0);
			}

			// 마우스 입력
			if (!inputPressed && Mouse.current?.leftButton.wasPressedThisFrame == true)
			{
				inputPressed = true;
				inputPosition = Mouse.current.position.ReadValue();
			}

			// 터치 입력 체크
			if (!inputPressed && Touchscreen.current != null)
			{
				foreach (var touch in Touchscreen.current.touches)
				{
					if (touch.press.wasPressedThisFrame)
					{
						inputPressed = true;
						inputPosition = touch.position.ReadValue();
						break;
					}
				}
			}

			if (inputPressed)
			{
				ProcessClickerInput(inputPosition);
			}
		}

		/// <summary>
		/// 클릭/터치 입력 처리 - 타이밍 판정, 점수 계산, 시각/음향 효과
		/// </summary>
		/// <param name="inputPosition">입력 위치 (스크린 좌표)</param>
		private void ProcessClickerInput(Vector3 inputPosition)
		{
			const float NOTE_DESTROY_DELAY = 0.3f; // 노트 파괴 지연 시간 (초)

			float multiplier = s_TimingMultipliers["BASIC"];
			string bonusType = "BASIC";

			// 음악 재생 중이고 노트가 있을 때만 타이밍 체크
			if (m_IsPlaying && m_ActiveNotes.Count > 0)
			{
				// 캐시 리스트 재사용
				m_NoteControllersCache.Clear();

				// 유효한 컨트롤러만 수집
				foreach (var noteObj in m_ActiveNotes)
				{
					if (noteObj != null)
					{
						LineNoteController controller = noteObj.GetComponent<LineNoteController>();
						if (controller != null)
						{
							m_NoteControllersCache.Add(controller);
						}
					}
				}

				// 가장 가까운 노트 찾기 + 중앙 원 영역 내 위치 체크
				LineNoteController closestNote = null;
				float closestTimeDiff = float.MaxValue;

				foreach (var controller in m_NoteControllersCache)
				{
					float timeDiff = controller.GetTimeDifference(currentTime);
					float absTimeDiff = Mathf.Abs(timeDiff);

					// 시간 기반 + 위치 기반 이중 체크
					if (absTimeDiff < closestTimeDiff && IsNoteInCenterArea(controller))
					{
						closestNote = controller;
						closestTimeDiff = absTimeDiff;
						break;
					}
				}

				// 히트 윈도우 내에 있으면 보너스
				if (closestNote != null && closestTimeDiff <= m_HitWindow)
				{
					bonusType = GetTimingBonus(closestTimeDiff);
					multiplier = s_TimingMultipliers[bonusType];

					// 노트 제거
					GameObject noteToRemove = closestNote.gameObject;
					m_ActiveNotes.Remove(noteToRemove);

					// 시각적 효과 - LineNoteController에서 개별 애니메이션 실행
					closestNote.PlayHitAnimation(bonusType);
					Destroy(noteToRemove, NOTE_DESTROY_DELAY);
				}
			}

			// 터치/클릭 위치에 파티클 효과 재생
			PlayHitParticleEffect(inputPosition, bonusType);

			// 점수 계산 및 업데이트
			int finalReward = Mathf.RoundToInt(m_BaseClickReward * multiplier);
			m_Score += finalReward;

			ShowClickerFeedback(bonusType, finalReward);
			UpdateScoreDisplay();
		}

		/// <summary>
		/// 타이밍 차이에 따른 보너스 등급 계산
		/// </summary>
		/// <param name="timeDiff">시간 차이 (초)</param>
		/// <returns>보너스 등급 문자열</returns>
		private string GetTimingBonus(float timeDiff)
		{
			if (timeDiff <= 0.015f) return "PERFECT";
			if (timeDiff <= 0.030f) return "NICE";
			if (timeDiff <= 0.050f) return "GOOD";
			return "BAD";
		}

		/// <summary>
		/// 터치 위치에 타이밍에 따른 파티클 효과 재생
		/// </summary>
		/// <param name="screenPosition">터치한 스크린 위치</param>
		/// <param name="bonusType">타이밍 보너스 타입 (HIT, PERFECT, NICE, GOOD, BAD)</param>
		private void PlayHitParticleEffect(Vector3 screenPosition, string bonusType)
		{
			if (ParticleManager.Instance == null) return;

			// 타이밍에 따른 파티클 이름 결정
			string particleName = bonusType.ToUpper() switch
			{
				"PERFECT" => "HitPerfect",
				"NICE" => "HitNice",
				"GOOD" => "HitGood",
				"BAD" => "HitBad",
				_ => "Hit"
			};

			// 월드 파티클 재생
			Camera mainCamera = Camera.main;
			if (mainCamera == null) return;

			Vector3 worldPosition = mainCamera.ScreenToWorldPoint(
				new Vector3(screenPosition.x, screenPosition.y, 10f));

			ParticleManager.Instance.PlayWorld(particleName, worldPosition, null, false,
				$"HitEffect_{bonusType}", "", 1);
		}

		/// <summary>
		/// 노트가 중앙 타겟 영역(원) 내에 있는지 확인
		/// </summary>
		/// <param name="noteController">확인할 노트 컨트롤러</param>
		/// <returns>중앙 영역 내 포함 여부</returns>
		private bool IsNoteInCenterArea(LineNoteController noteController)
		{
			if (m_CenterRectTransform == null || noteController == null) return false;

			// 중앙 타겟의 위치와 반지름
			Vector2 centerPos = m_CenterRectTransform.anchoredPosition;
			float centerRadius = m_CenterRectTransform.sizeDelta.x * 0.5f; // 원의 반지름

			// 노트의 현재 위치
			RectTransform noteRect = noteController.GetComponent<RectTransform>();
			if (noteRect == null) return false;

			Vector2 notePos = noteRect.anchoredPosition;

			// 두 점 사이의 거리 계산
			float distance = Vector2.Distance(centerPos, notePos);

			// 판정 여유도 추가 (중앙 타겟 반지름의 120% 내에서 히트 가능)
			float hitRadius = centerRadius * 1.2f;

			bool isInArea = distance <= hitRadius;

			if (isInArea)
			{
				Debug.Log($"🎯 Note in center area: distance={distance:F1}, hitRadius={hitRadius:F1}");
			}

			return isInArea;
		}

		/// <summary>
		/// 클릭 피드백 표시 - 중앙 타겟 효과, 히트 사운드 재생
		/// </summary>
		/// <param name="bonusType">보너스 타입</param>
		/// <param name="reward">획득 점수</param>
		private void ShowClickerFeedback(string bonusType, int reward)
		{
			const float MIN_HIT_SOUND_INTERVAL = 0.1f; // 히트 사운드 최소 간격 (초)

			// 중앙 타겟 위치 안정성 유지 (애니메이션 제거됨)
			// 개별 노트에서 애니메이션을 처리하므로 중앙 타겟은 안정적으로 유지

			// 히트 사운드 (중복 방지)
			if (!m_HitSoundEventPath.IsNull && bonusType != "BASIC")
			{
				float currentTime = Time.time;
				if (currentTime - m_LastHitSoundTime >= MIN_HIT_SOUND_INTERVAL)
				{
					RuntimeManager.PlayOneShot(m_HitSoundEventPath);
					m_LastHitSoundTime = currentTime;
				}
			}
		}

		/// <summary>
		/// 점수 표시 UI 업데이트 및 펀치 스케일 효과
		/// </summary>
		private void UpdateScoreDisplay()
		{
			if (m_ScoreText != null)
			{
				m_ScoreText.text = $"Score: {m_Score}";
				m_ScoreText.transform.DOKill();
				m_ScoreText.transform.DOPunchScale(Vector3.one * 0.1f, 0.2f, 3, 0.3f);
			}
		}

		/// <summary>
		/// 게임 오브젝트들 정리 - 노트, 섹션 라인 등 모든 활성 오브젝트 제거
		/// </summary>
		private void ClearAllGameObjects()
		{
			// 노트 정리
			foreach (var noteObj in m_ActiveNotes)
			{
				if (noteObj != null)
				{
					noteObj.transform.DOKill();
					Destroy(noteObj);
				}
			}
			m_ActiveNotes.Clear();

#if UNITY_EDITOR
			// 섹션 라인 정리
			foreach (var lineObj in m_ActiveSectionLines)
			{
				if (lineObj != null)
				{
					lineObj.transform.DOKill();
					Destroy(lineObj);
				}
			}
			m_ActiveSectionLines.Clear();
#endif
		}

		/// <summary>
		/// 기본 노트 프리팹 생성
		/// </summary>
		private void CreateDefaultNotePrefab()
		{
			m_NotePrefab = new GameObject("DefaultLineNote");
			// m_NotePrefab.transform.SetParent(m_GameCanvas.transform);
			var image = m_NotePrefab.AddComponent<Image>();
			var rectTransform = m_NotePrefab.GetComponent<RectTransform>();

			image.sprite = GetOrCreateLineSprite();
			image.color = Color.cyan;
			rectTransform.sizeDelta = new Vector2(8, 200);
		}

		/// <summary>
		/// 라인 스프라이트 생성 또는 캐시에서 반환
		/// </summary>
		/// <returns>라인 스프라이트</returns>
		private static Sprite GetOrCreateLineSprite()
		{
			if (s_LineSprite != null) return s_LineSprite;

			const int WIDTH = 8, HEIGHT = 64;
			var texture = new Texture2D(WIDTH, HEIGHT);
			var colors = new Color[WIDTH * HEIGHT];

			for (int i = 0; i < colors.Length; i++)
				colors[i] = Color.white;

			texture.SetPixels(colors);
			texture.Apply();

			s_LineSprite = Sprite.Create(texture, new Rect(0, 0, WIDTH, HEIGHT), new Vector2(0.5f, 0.5f));
			return s_LineSprite;
		}

		/// <summary>
		/// 원형 스프라이트 생성 또는 캐시에서 반환
		/// </summary>
		/// <returns>원형 스프라이트</returns>
		private static Sprite GetOrCreateCircleSprite()
		{
			if (s_CircleSprite != null) return s_CircleSprite;

			const int SIZE = 64;
			var texture = new Texture2D(SIZE, SIZE);
			var center = new Vector2(32, 32);
			var colors = new Color[SIZE * SIZE];

			for (int i = 0; i < colors.Length; i++)
			{
				int x = i % SIZE;
				int y = i / SIZE;
				float distance = Vector2.Distance(new Vector2(x, y), center);
				colors[i] = (distance <= 30 && distance >= 25) ? Color.white : Color.clear;
			}

			texture.SetPixels(colors);
			texture.Apply();

			s_CircleSprite = Sprite.Create(texture, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f));
			return s_CircleSprite;
		}

		private void OnCheckIntroPage(bool isActive)
		{
			m_IsIntro = isActive;
		}

#if UNITY_EDITOR
		/// <summary>
		/// 디버그용 기본 섹션 라인 프리팹 생성
		/// </summary>
		private void CreateDefaultSectionLinePrefab()
		{
			m_SectionLinePrefab = new GameObject("DefaultSectionLine");
			// m_SectionLinePrefab.transform.SetParent(m_GameCanvas.transform);
			var image = m_SectionLinePrefab.AddComponent<Image>();
			var rectTransform = m_SectionLinePrefab.GetComponent<RectTransform>();

			image.color = new Color(1f, 1f, 1f, 0.5f);
			image.sprite = GetOrCreateHorizontalLineSprite();
			rectTransform.sizeDelta = new Vector2(1200, 1);
		}

		/// <summary>
		/// 수평 라인 스프라이트 생성 또는 캐시에서 반환
		/// </summary>
		/// <returns>수평 라인 스프라이트</returns>
		private static Sprite GetOrCreateHorizontalLineSprite()
		{
			if (s_HorizontalLineSprite != null) return s_HorizontalLineSprite;

			const int WIDTH = 64, HEIGHT = 1;
			var texture = new Texture2D(WIDTH, HEIGHT);
			var colors = new Color[WIDTH * HEIGHT];

			for (int i = 0; i < colors.Length; i++)
				colors[i] = Color.white;

			texture.SetPixels(colors);
			texture.Apply();

			s_HorizontalLineSprite = Sprite.Create(texture, new Rect(0, 0, WIDTH, HEIGHT), new Vector2(0.5f, 0.5f));
			return s_HorizontalLineSprite;
		}

		/// <summary>
		/// 디버그용 섹션 라인 생성 로직
		/// </summary>
		private void SpawnSectionLines()
		{
			const float SECTION_SPAWN_TIME = 3f; // 섹션 라인 생성 시점 (초)

			if (m_CurrentVocalSections == null || m_CurrentVocalSections.Count == 0) return;

			while (m_NextSectionIndex < m_CurrentVocalSections.Count)
			{
				var section = m_CurrentVocalSections[m_NextSectionIndex];

				if (section.StartTime - currentTime > SECTION_SPAWN_TIME) break;

				SpawnSectionLine(section);
				m_NextSectionIndex++;
			}
		}

		/// <summary>
		/// 개별 섹션 라인 오브젝트 생성
		/// </summary>
		/// <param name="section">생성할 섹션 데이터</param>
		private void SpawnSectionLine(VocalSection section)
		{
			if (m_GameCanvas == null || m_CenterTarget == null) return;

			var sectionLineObj = Instantiate(m_SectionLinePrefab, m_GameCanvas.transform);
			sectionLineObj.transform.SetSiblingIndex(0);

			var controller = sectionLineObj.GetComponent<SectionLineController>() ??
							sectionLineObj.AddComponent<SectionLineController>();

			controller.Initialize(section, this, m_GameCanvas, m_CenterTarget);
			m_ActiveSectionLines.Add(sectionLineObj);
		}

		/// <summary>
		/// 활성화된 섹션 라인들 상태 업데이트 및 만료된 라인 제거
		/// </summary>
		private void UpdateSectionLines()
		{
			for (int i = m_ActiveSectionLines.Count - 1; i >= 0; i--)
			{
				var lineObj = m_ActiveSectionLines[i];
				if (lineObj == null)
				{
					m_ActiveSectionLines.RemoveAt(i);
					continue;
				}

				var controller = lineObj.GetComponent<SectionLineController>();
				if (controller != null && controller.IsExpired())
				{
					m_ActiveSectionLines.RemoveAt(i);
					Destroy(lineObj);
				}
			}
		}
#endif
	}
}
