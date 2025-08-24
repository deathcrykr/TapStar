using DG.Tweening;
using TapStar.Structs;
using UnityEngine;
using UnityEngine.UI;

namespace TapStar.Controller
{
	/// <summary>
	/// 라인 노트의 움직임과 시각적 효과를 제어하는 컨트롤러
	/// </summary>
	public class LineNoteController : MonoBehaviour
	{
		[Tooltip("노트 데이터")]
		[SerializeField] private Note m_NoteData;
		public Note NoteData { get => m_NoteData; set => m_NoteData = value; }

		[Tooltip("게임 매니저 참조")]
		[SerializeField] private GameManager m_GameManager;
		public GameManager GameManager { get => m_GameManager; set => m_GameManager = value; }

		[Tooltip("게임 캔버스 참조")]
		[SerializeField] private Canvas m_GameCanvas;
		public Canvas GameCanvas { get => m_GameCanvas; set => m_GameCanvas = value; }

		[Tooltip("중앙 타겟 참조")]
		[SerializeField] private Transform m_CenterTarget;
		public Transform CenterTarget { get => m_CenterTarget; set => m_CenterTarget = value; }

		[Tooltip("애니메이션 지속 시간")]
		[SerializeField] private float m_Duration = 2f;
		public float Duration { get => m_Duration; set => m_Duration = value; }

		private float m_StartTime;
		private Image m_LineImage;
		private RectTransform m_RectTransform;
		private float m_StartX;
		private float m_TargetX;
		private static Sprite s_LineSprite; // 정적 캐싱으로 성능 향상
		private bool m_HasReachedCenter = false; // 중앙 도달 플래그


		// Lifecycle Methods
		/// <summary>
		/// 컴포넌트를 초기화합니다.
		/// </summary>
		private void Awake()
		{
			m_LineImage = GetComponent<Image>();
			if (m_LineImage == null)
			{
				m_LineImage = gameObject.AddComponent<Image>();
			}

			m_RectTransform = GetComponent<RectTransform>();
			if (m_RectTransform == null)
			{
				m_RectTransform = gameObject.AddComponent<RectTransform>();
			}
		}

		/// <summary>
		/// 노트 상태를 업데이트합니다.
		/// </summary>
		private void Update()
		{
			// 중앙에 도달했으면 Update에서 색상 조정하지 않음
			if (m_HasReachedCenter)
			{
				return;
			}

			// Level 3 (Hard) 노트는 완전히 보이지 않음
			if (m_NoteData.Level == 3)
			{
				return; // 알파값 조정하지 않음
			}

			// 타이밍에 따른 알파값 조정 (Level 1, 2만)
			float targetTime = m_NoteData.TimeSeconds - m_GameManager.currentTime;
			float absTargetTime = Mathf.Abs(targetTime);

			// 레벨별 기본 알파값
			float baseAlpha = m_NoteData.Level == 1 ? 1f : 0.2f; // Level 1: 100%, Level 2: 20%

			if (absTargetTime < 0.3f)
			{
				// 타이밍이 가까워지면 기본 알파값으로
				var color = m_LineImage.color;
				if (Mathf.Abs(color.a - baseAlpha) > 0.01f)
				{
					color.a = baseAlpha;
					m_LineImage.color = color;
				}
			}
			else
			{
				// 거리에 따른 페이드 (기본 알파값 기준)
				float alpha = Mathf.Clamp01((1f - absTargetTime / m_Duration) * baseAlpha);
				var color = m_LineImage.color;
				if (Mathf.Abs(color.a - alpha) > 0.01f)
				{
					color.a = alpha;
					m_LineImage.color = color;
				}
			}
		}

		/// <summary>
		/// 노트 컨트롤러를 초기화합니다.
		/// </summary>
		/// <param name="note">노트 데이터</param>
		/// <param name="manager">게임 매니저</param>
		/// <param name="canvas">캔버스</param>
		/// <param name="center">중앙 타겟</param>
		public void Initialize(Note note, GameManager manager, Canvas canvas, Transform center)
		{
			this.m_NoteData = note;
			this.m_GameManager = manager;
			this.m_GameCanvas = canvas;
			this.m_CenterTarget = center;

			// 세로 라인 설정
			m_LineImage.sprite = CreateLineSprite();

			// 레벨별 외관 설정
			SetupNoteAppearanceByLevel(note.Level);

			// 레벨별 초기 크기 설정
			float initialHeight = m_NoteData.Level == 2 ? 10f : 200f; // Level 2는 10px에서 시작
			m_RectTransform.sizeDelta = new Vector2(8, initialHeight);

			// CenterTarget의 앵커와 동일하게 설정
			var centerRect = m_CenterTarget.GetComponent<RectTransform>();
			m_RectTransform.anchorMin = centerRect.anchorMin;
			m_RectTransform.anchorMax = centerRect.anchorMax;
			m_RectTransform.pivot = centerRect.pivot;

			// 시작 위치 결정 (랜덤하게 좌측 또는 우측)
			float canvasWidth = canvas.GetComponent<RectTransform>().rect.width;
			bool fromLeft = Random.Range(0, 2) == 0;

			m_StartX = fromLeft ? -canvasWidth * 0.6f : canvasWidth * 0.6f;
			m_TargetX = 0f; // 화면 중앙

			// 시작 위치 설정 (m_CenterTarget의 Y 위치와 동일하게)
			float centerY = m_CenterTarget.GetComponent<RectTransform>().anchoredPosition.y;
			Debug.Log($"🎯 CenterTarget Y position: {centerY}");
			Debug.Log($"⚓ CenterTarget anchors: min({centerRect.anchorMin.x}, {centerRect.anchorMin.y}) max({centerRect.anchorMax.x}, {centerRect.anchorMax.y}) pivot({centerRect.pivot.x}, {centerRect.pivot.y})");
			m_RectTransform.anchoredPosition = new Vector2(m_StartX, centerY);
			Debug.Log($"📍 Note spawned at position: ({m_StartX}, {centerY})");

			// 애니메이션 시간 계산 (정확한 타이밍 보장)
			float timeToTarget = m_NoteData.TimeSeconds - m_GameManager.currentTime;

			// 타이밍 안전성 검증
			if (timeToTarget <= 0)
			{
				Debug.LogWarning($"⚠️ Note spawn timing issue: timeToTarget={timeToTarget}, noteTime={m_NoteData.TimeSeconds}, currentTime={m_GameManager.currentTime}");
				timeToTarget = 0.1f; // 최소 시간 보장
			}

			// DOTween 애니메이션들 - Linear easing으로 정확한 타이밍 보장
			// 1. 중앙으로 이동은 아래에서 OnComplete와 함께 처리

			// 2. 크기 변화 - 레벨별 애니메이션 (Linear로 정확한 타이밍)
			if (m_NoteData.Level == 1) // Level 1: 200px → 100px
			{
				m_RectTransform.DOSizeDelta(new Vector2(8, 100), timeToTarget).SetEase(Ease.Linear);
			}
			else if (m_NoteData.Level == 2) // Level 2: 10px → 100px
			{
				m_RectTransform.DOSizeDelta(new Vector2(8, 100), timeToTarget).SetEase(Ease.Linear);
			}
			// Level 3은 보이지 않으므로 크기 애니메이션 없음

			// 3. 중앙 도달 시점에 흰색으로 변경 후 0.2초 대기 후 사라짐
			if (m_NoteData.Level != 3)
			{
				// 이동 애니메이션이 완료되면 (중앙 도달 시) 흰색으로 변경
				// Y 위치는 m_CenterTarget과 동일하게 유지
				float targetY = m_CenterTarget.GetComponent<RectTransform>().anchoredPosition.y;
				m_RectTransform.DOAnchorPos(new Vector2(m_TargetX, targetY), timeToTarget).SetEase(Ease.Linear)
					.OnComplete(() =>
					{
						// Update() 간섭 방지를 위해 플래그 설정
						m_HasReachedCenter = true;

						// 중앙 도달 순간에 흰색으로 변경
						m_LineImage.DOKill(); // 기존 애니메이션 중단
											  // 중앙 도달 순간에 흰색으로 변경
						m_LineImage.color = Color.white;

						// 0.1초 대기 후 투명하게 사라짐
						DOVirtual.DelayedCall(0.1f, () =>
						{
							if (this != null && m_LineImage != null)
							{
								m_LineImage.DOFade(0f, 0.5f).SetEase(Ease.OutCubic)
									.OnComplete(() =>
									{
										if (this != null && gameObject != null)
										{
											Destroy(gameObject);
										}
									});
							}
						});
					});
			}

			// 로그 메시지를 레벨별로 구체화
			string sizeInfo = m_NoteData.Level == 1 ? "200px→100px" :
							 m_NoteData.Level == 2 ? "10px→100px" : "invisible";
			Debug.Log($"📏 Line note spawned: Level {m_NoteData.Level}, from ({m_StartX}, {centerY}) to ({m_TargetX}, {centerY}), size {sizeInfo}, duration: {timeToTarget}s");
			Debug.Log($"🎯 NOTE TIMING: Will reach center at {m_NoteData.TimeSeconds}s (current: {m_GameManager.currentTime}s, offset: {m_GameManager.AudioLatencyOffset}s)");
			Debug.Log($"⏱️  ANIMATION: {timeToTarget}s to complete, spawn-to-hit delay: {timeToTarget}");
		}

		/// <summary>
		/// 레벨에 따른 노트 외관을 설정합니다.
		/// </summary>
		/// <param name="level">노트 레벨</param>
		private void SetupNoteAppearanceByLevel(int level)
		{
			switch (level)
			{
				case 1: // Easy - 기존 노트 (100% 불투명, cyan)
					m_LineImage.color = Color.cyan;
					gameObject.SetActive(true);
					Debug.Log($"🟦 Level 1 (Easy) note: Full opacity, cyan color");
					break;

				case 2: // Medium - 반투명 노트 (20% 불투명, cyan, 10px→100px 애니메이션)
					m_LineImage.color = new Color(0f, 1f, 1f, 0.2f); // cyan with 20% alpha
					gameObject.SetActive(true);
					Debug.Log($"🟦 Level 2 (Medium) note: 20% opacity, cyan color, 10px→100px animation");
					break;

				case 3: // Hard - 보이지 않는 노트 (판정만 존재)
					m_LineImage.color = new Color(0f, 1f, 1f, 0f); // 완전 투명
					gameObject.SetActive(true); // 판정을 위해 GameObject는 활성화 유지
					Debug.Log($"👻 Level 3 (Hard) note: Invisible (judgment only)");
					break;

				default:
					m_LineImage.color = Color.cyan; // 기본값
					gameObject.SetActive(true);
					Debug.LogWarning($"⚠️ Unknown note level: {level}, using default appearance");
					break;
			}
		}

		/// <summary>
		/// 시간 차이를 반환합니다.
		/// </summary>
		/// <param name="currentTime">현재 시간</param>
		/// <returns>시간 차이</returns>
		public float GetTimeDifference(float currentTime)
		{
			return m_NoteData.TimeSeconds - currentTime;
		}

		/// <summary>
		/// 노트가 만료되었는지 확인합니다.
		/// </summary>
		/// <returns>만료 여부</returns>
		public bool IsExpired()
		{
			return GetTimeDifference(m_GameManager.currentTime) < -1f;
		}

		/// <summary>
		/// 라인 스프라이트를 생성합니다.
		/// </summary>
		/// <returns>라인 스프라이트</returns>
		private Sprite CreateLineSprite()
		{
			// 이미 생성된 스프라이트가 있으면 재사용
			if (s_LineSprite != null) return s_LineSprite;

			const int WIDTH = 8;
			const int HEIGHT = 64;

			var texture = new Texture2D(WIDTH, HEIGHT);
			var colors = new Color[WIDTH * HEIGHT];

			// 전체를 흰색으로 채움
			for (int i = 0; i < colors.Length; i++)
			{
				colors[i] = Color.white;
			}

			texture.SetPixels(colors);
			texture.Apply();

			s_LineSprite = Sprite.Create(texture, new Rect(0, 0, WIDTH, HEIGHT), new Vector2(0.5f, 0.5f));
			return s_LineSprite;
		}
	}
}
