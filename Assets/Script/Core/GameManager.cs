using System.Collections.Generic;
using DG.Tweening;
using FMODUnity;
using TabStar.Controller;
using TabStar.Structs;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_EDITOR
using TabStar.Editors;
#endif

namespace TabStar
{
	/// <summary>
	/// 리듬 게임의 핵심 로직을 관리하는 매니저 클래스
	/// </summary>
	public class GameManager : MonoBehaviour
	{
		public static GameManager Instance { get; private set; }

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

		[Header("Clicker Rewards")]
		[Tooltip("기본 클릭 보상")]
		[SerializeField] private int m_BaseClickReward = 10;
		public int BaseClickReward { get => m_BaseClickReward; set => m_BaseClickReward = value; }

		[Header("Clicker Control")]
		[Tooltip("클릭 가능 상태 (기본값: true)")]
		[SerializeField] private bool m_IsClickEnabled = true;
		public bool IsClickEnabled { get => m_IsClickEnabled; set => m_IsClickEnabled = value; }

		public float currentTime = 0f;
		public bool IsPlaying => m_IsPlaying;

		// Constants
		private const float MIN_HIT_SOUND_INTERVAL = 0.1f;
		private const float NOTE_SPAWN_TIME = 2f;
		private const float SECTION_SPAWN_TIME = 3f;
		private const float NOTE_DESTROY_DELAY = 0.3f;

		// Static resources (캐싱으로 메모리 효율 개선)
		private static Sprite s_LineSprite;
		private static Sprite s_CircleSprite;
		private static Sprite s_HorizontalLineSprite;
		private static readonly Dictionary<string, float> s_TimingMultipliers = new Dictionary<string, float>
		{
			{ "PERFECT", 6f },
			{ "NICE", 4f },
			{ "GOOD", 3f },
			{ "BAD", 2f },
			{ "BASIC", 1f }
		};

		// Game state
		private FMOD.Studio.EventInstance m_MusicInstance;
		private NoteData m_CurrentNoteData;
		private List<VocalSection> m_CurrentVocalSections = new List<VocalSection>();
		private List<GameObject> m_ActiveNotes = new List<GameObject>();
		private List<GameObject> m_ActiveSectionLines = new List<GameObject>();
		private int m_NextNoteIndex = 0;
		private int m_NextSectionIndex = 0;
		private bool m_IsPlaying = false;
		private float m_LastHitSoundTime = 0f;

		// Cached references (성능 최적화)
		private MobileUIController m_CachedUIController;
		private RectTransform m_CenterRectTransform;
		private float m_CanvasWidth;

		// Input cache (매 프레임 new 방지)
		private readonly List<LineNoteController> m_NoteControllersCache = new List<LineNoteController>(32);

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

		private void Start()
		{
			if (!CheckFMODAvailability())
			{
				Debug.LogError("❌ FMOD is not available! GameManager disabled.");
				enabled = false;
				return;
			}

			InitializeComponents();
			LoadMusicAndNotes();

			// 자동 시작은 옵션으로 (필요시 주석 해제)
			// Invoke(nameof(StartGame), 3f);
		}

		private void Update()
		{
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

			m_CachedUIController?.ShowGameOver(m_Score);
		}

		/// <summary>
		/// 난이도 레벨을 설정합니다
		/// </summary>
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

		public void SetAudioLatencyOffset(float offsetSeconds) => m_AudioLatencyOffset = offsetSeconds;
		public void EnableClicking() => IsClickEnabled = true;
		public void DisableClicking() => IsClickEnabled = false;
		public void ToggleClicking() => IsClickEnabled = !IsClickEnabled;

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

		private void InitializeComponents()
		{
			// UI Controller 캐싱
			m_CachedUIController = FindFirstObjectByType<MobileUIController>();

			// Canvas 설정 및 캐싱
			if (m_GameCanvas == null)
			{
				m_GameCanvas = FindFirstObjectByType<Canvas>();
			}

			if (m_GameCanvas != null)
			{
				m_CanvasWidth = m_GameCanvas.GetComponent<RectTransform>().rect.width;
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

		private void ResetGameState()
		{
			m_Score = 0;
			currentTime = 0f;
			m_NextNoteIndex = 0;
			m_NextSectionIndex = 0;
			ClearAllGameObjects();
		}

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

		private void SpawnNotes()
		{
			if (m_CurrentNoteData?.Notes == null) return;

			while (m_NextNoteIndex < m_CurrentNoteData.Notes.Count)
			{
				Note note = m_CurrentNoteData.Notes[m_NextNoteIndex];

				if (note.TimeSeconds - currentTime > NOTE_SPAWN_TIME) break;

				SpawnNote(note);
				m_NextNoteIndex++;
			}
		}

		private void SpawnNote(Note note)
		{
			if (m_NotePrefab == null || m_GameCanvas == null || m_CenterTarget == null) return;

			GameObject noteObj = Instantiate(m_NotePrefab, m_GameCanvas.transform);
			var controller = noteObj.GetComponent<LineNoteController>() ??
							noteObj.AddComponent<LineNoteController>();

			controller.Initialize(note, this, m_GameCanvas, m_CenterTarget);
			m_ActiveNotes.Add(noteObj);
		}

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

		private void CheckNoteInput()
		{
			if (!m_IsClickEnabled) return;

			bool inputPressed = Keyboard.current?.spaceKey.wasPressedThisFrame == true ||
							   Mouse.current?.leftButton.wasPressedThisFrame == true;

			// 터치 입력 체크
			if (!inputPressed && Touchscreen.current != null)
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
				ProcessClickerInput();
			}
		}

		private void ProcessClickerInput()
		{
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
						var controller = noteObj.GetComponent<LineNoteController>();
						if (controller != null)
						{
							m_NoteControllersCache.Add(controller);
						}
					}
				}

				// 가장 가까운 노트 찾기
				LineNoteController closestNote = null;
				float closestTimeDiff = float.MaxValue;

				foreach (var controller in m_NoteControllersCache)
				{
					float timeDiff = controller.GetTimeDifference(currentTime);
					float absTimeDiff = Mathf.Abs(timeDiff);

					if (absTimeDiff < closestTimeDiff)
					{
						closestNote = controller;
						closestTimeDiff = absTimeDiff;
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

					// 시각적 효과
					ApplyHitVisualEffect(closestNote);
					Destroy(noteToRemove, NOTE_DESTROY_DELAY);
				}
			}

			// 점수 계산 및 업데이트
			int finalReward = Mathf.RoundToInt(m_BaseClickReward * multiplier);
			m_Score += finalReward;

			ShowClickerFeedback(bonusType, finalReward);
			UpdateScoreDisplay();
		}

		private string GetTimingBonus(float timeDiff)
		{
			if (timeDiff <= 0.015f) return "PERFECT";
			if (timeDiff <= 0.030f) return "NICE";
			if (timeDiff <= 0.050f) return "GOOD";
			return "BAD";
		}

		private void ApplyHitVisualEffect(LineNoteController note)
		{
			note.transform.DOScale(0f, 0.2f).SetEase(Ease.InBack);
			var image = note.GetComponent<Image>();
			if (image != null)
			{
				image.DOFade(0f, 0.2f);
			}
		}

		private void ShowClickerFeedback(string bonusType, int reward)
		{
			// 중앙 타겟 효과
			if (m_CenterTarget != null)
			{
				m_CenterTarget.DOKill();

				float scale = bonusType switch
				{
					"PERFECT" => 0.4f,
					"NICE" => 0.3f,
					"GOOD" => 0.25f,
					"BAD" => 0.2f,
					_ => 0.15f
				};

				m_CenterTarget.DOPunchScale(Vector3.one * scale, 0.3f, 5, 0.5f);
			}

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

		private void UpdateScoreDisplay()
		{
			m_CachedUIController?.UpdateScore(m_Score);

			if (m_ScoreText != null)
			{
				m_ScoreText.text = $"Score: {m_Score}";
				m_ScoreText.transform.DOKill();
				m_ScoreText.transform.DOPunchScale(Vector3.one * 0.1f, 0.2f, 3, 0.3f);
			}
		}

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

		// 스프라이트 생성 메서드들 (static 캐싱)
		private void CreateDefaultNotePrefab()
		{
			m_NotePrefab = new GameObject("DefaultLineNote");
			var image = m_NotePrefab.AddComponent<Image>();
			var rectTransform = m_NotePrefab.GetComponent<RectTransform>();

			image.sprite = GetOrCreateLineSprite();
			image.color = Color.cyan;
			rectTransform.sizeDelta = new Vector2(8, 200);
		}

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

#if UNITY_EDITOR
		// 디버그용 섹션 라인 메서드들
		private void CreateDefaultSectionLinePrefab()
		{
			m_SectionLinePrefab = new GameObject("DefaultSectionLine");
			var image = m_SectionLinePrefab.AddComponent<Image>();
			var rectTransform = m_SectionLinePrefab.GetComponent<RectTransform>();

			image.color = new Color(1f, 1f, 1f, 0.5f);
			image.sprite = GetOrCreateHorizontalLineSprite();
			rectTransform.sizeDelta = new Vector2(1200, 1);
		}

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

		private void SpawnSectionLines()
		{
			if (m_CurrentVocalSections == null || m_CurrentVocalSections.Count == 0) return;

			while (m_NextSectionIndex < m_CurrentVocalSections.Count)
			{
				var section = m_CurrentVocalSections[m_NextSectionIndex];

				if (section.StartTime - currentTime > SECTION_SPAWN_TIME) break;

				SpawnSectionLine(section);
				m_NextSectionIndex++;
			}
		}

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
