using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보컬 섹션을 시각적으로 표현하는 가로선 컨트롤러
/// </summary>
public class SectionLineController : MonoBehaviour
{

	#region Static Cache
	/// <summary>
	/// 캐시된 가로선 스프라이트 (메모리 최적화)
	/// </summary>
	private static Sprite s_CachedLineSprite;
	#endregion

	#region Private Fields
	/// <summary>
	/// 현재 섹션의 데이터 정보
	/// </summary>
	private VocalSection m_SectionData;

	/// <summary>
	/// 리듬 게임 매니저 참조
	/// </summary>
	private RhythmGameManager m_GameManager;

	/// <summary>
	/// 게임 캔버스 참조
	/// </summary>
	private Canvas m_GameCanvas;

	/// <summary>
	/// 중앙 타겟 Transform 참조
	/// </summary>
	private Transform m_CenterTarget;

	/// <summary>
	/// 가로선 이미지 컴포넌트
	/// </summary>
	private Image m_LineImage;

	/// <summary>
	/// RectTransform 컴포넌트
	/// </summary>
	private RectTransform m_RectTransform;

	/// <summary>
	/// DOTween 애니메이션 시퀀스 (정리용)
	/// </summary>
	private Sequence m_AnimationSequence;

	/// <summary>
	/// 가로선의 초기 너비 (화면 너비보다 넓게 설정)
	/// </summary>
	private float m_InitialWidth = 1200f;

	/// <summary>
	/// 중앙에서 축소될 목표 너비
	/// </summary>
	private float m_TargetWidth = 50f;

	/// <summary>
	/// 선의 높이 (매우 얇게 설정)
	/// </summary>
	private float m_LineHeight = 1f;
	#endregion

	#region Unity Lifecycle
	/// <summary>
	/// Unity Awake 메서드 - 컴포넌트 초기화
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
	/// Unity OnDestroy - 리소스 정리
	/// </summary>
	private void OnDestroy()
	{
		CleanupAnimations();
	}
	#endregion

	#region Public Methods
	/// <summary>
	/// 섹션 라인 컨트롤러 초기화
	/// </summary>
	/// <param name="section">보컬 섹션 데이터</param>
	/// <param name="manager">리듬 게임 매니저</param>
	/// <param name="canvas">게임 캔버스</param>
	/// <param name="center">중앙 타겟 Transform</param>
	public void Initialize(VocalSection section, RhythmGameManager manager, Canvas canvas, Transform center)
	{
		// Null 체크
		if (section == null)
		{
#if UNITY_EDITOR
			Debug.LogError("SectionLineController: VocalSection is null!");
#endif
			return;
		}

		if (manager == null)
		{
#if UNITY_EDITOR
			Debug.LogError("SectionLineController: RhythmGameManager is null!");
#endif
			return;
		}

		this.m_SectionData = section;
		this.m_GameManager = manager;
		this.m_GameCanvas = canvas;
		this.m_CenterTarget = center;

		// 구역 타입별 색상 설정
		SetupSectionAppearance(section.type);

		// 가로선 초기 설정
		SetupInitialTransform();

		// 애니메이션 스케줄링
		ScheduleSectionAnimation();

#if UNITY_EDITOR
		Debug.Log($"🎤 Section line spawned: {section.type} ({section.start_time}s - {section.end_time}s), width: {m_InitialWidth}→{m_TargetWidth}");
#endif
	}

	/// <summary>
	/// 섹션이 만료되었는지 확인
	/// </summary>
	/// <returns>만료 여부</returns>
	public bool IsExpired()
	{
		const float EXPIRE_BUFFER_TIME = 1f; // 만료 버퍼 시간 (초)

		if (m_GameManager == null || m_SectionData == null)
			return true;

		return m_GameManager.currentTime > m_SectionData.end_time + EXPIRE_BUFFER_TIME;
	}

	/// <summary>
	/// 섹션 시작 시간 반환
	/// </summary>
	/// <returns>시작 시간</returns>
	public float GetStartTime()
	{
		return m_SectionData?.start_time ?? 0f;
	}

	/// <summary>
	/// 섹션 종료 시간 반환
	/// </summary>
	/// <returns>종료 시간</returns>
	public float GetEndTime()
	{
		return m_SectionData?.end_time ?? 0f;
	}

	/// <summary>
	/// 섹션 타입 반환
	/// </summary>
	/// <returns>섹션 타입</returns>
	public string GetSectionType()
	{
		return m_SectionData?.type ?? string.Empty;
	}
	#endregion

	#region Private Methods

	/// <summary>
	/// 초기 Transform 설정
	/// </summary>
	private void SetupInitialTransform()
	{
		const float POSITION_Y_OFFSET = -130f; // Y축 위치 오프셋 (노트보다 뒤쪽 레이어)

		m_RectTransform.sizeDelta = new Vector2(m_InitialWidth, m_LineHeight);
		m_RectTransform.anchorMin = new Vector2(0.5f, 0.5f);
		m_RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
		m_RectTransform.pivot = new Vector2(0.5f, 0.5f);

		// 초기 위치 (노트보다 뒤쪽 레이어)
		m_RectTransform.anchoredPosition = new Vector2(0, POSITION_Y_OFFSET);
	}

	/// <summary>
	/// 섹션 애니메이션 스케줄링
	/// </summary>
	private void ScheduleSectionAnimation()
	{
		// 애니메이션 시간 계산
		float timeToTarget = m_SectionData.start_time - m_GameManager.currentTime;

		// DOTween 애니메이션: 구역 시작 시점에 축소 시작
		if (timeToTarget > 0)
		{
			// 구역 시작까지 대기 후 축소 애니메이션
			DOVirtual.DelayedCall(timeToTarget, () =>
			{
				if (this != null && gameObject != null)
				{
					StartSectionAnimation();
				}
			});
		}
		else
		{
			// 이미 시작된 구역이면 즉시 축소 시작
			StartSectionAnimation();
		}
	}

	/// <summary>
	/// 섹션 타입에 따른 외형 설정
	/// </summary>
	/// <param name="sectionType">섹션 타입 (vocal, sub_vocal, rap, ttaechang)</param>
	private void SetupSectionAppearance(string sectionType)
	{
		Color sectionColor;
		float alpha = 0.7f;

		switch (sectionType)
		{
			case "vocal":
				sectionColor = new Color(0f, 1f, 1f, alpha); // 시안 (기본 보컬)
				break;
			case "sub_vocal":
				sectionColor = new Color(0.5f, 0.5f, 1f, alpha); // 연보라 (서브 보컬)
				break;
			case "rap":
				sectionColor = new Color(1f, 0.5f, 0f, alpha); // 주황 (랩)
				break;
			case "ttaechang":
				sectionColor = new Color(1f, 1f, 0f, alpha); // 노란 (때창)
				break;
			default:
				sectionColor = new Color(1f, 1f, 1f, alpha); // 흰색 (기본)
				break;
		}

		m_LineImage.color = sectionColor;
		m_LineImage.sprite = GetOrCreateLineSprite();

#if UNITY_EDITOR
		Debug.Log($"🎨 Section appearance: {sectionType} = {sectionColor}");
#endif
	}

	/// <summary>
	/// 섹션 애니메이션 시작
	/// </summary>
	private void StartSectionAnimation()
	{
		// 애니메이션 설정 상수들
		const float FADE_START_RATIO = 0.3f;    // 페이드 시작 비율
		const float FADE_DURATION_RATIO = 0.7f; // 페이드 지속 시간 비율
		const float PULSE_SCALE = 1.3f;         // 펄스 스케일
		const float PULSE_DURATION = 0.8f;      // 펄스 지속 시간

		// 기존 애니메이션이 있다면 정리
		CleanupAnimations();

		// 구역 지속 시간 계산
		float sectionDuration = m_SectionData.end_time - m_SectionData.start_time;

		// 애니메이션 시퀀스 생성
		m_AnimationSequence = DOTween.Sequence();

		// 1. 가로선 축소 애니메이션 (양쪽에서 중앙으로)
		var sizeAnimation = m_RectTransform.DOSizeDelta(new Vector2(m_TargetWidth, m_LineHeight), sectionDuration)
										   .SetEase(Ease.InOutQuart);

		// 2. 점진적 페이드아웃
		var fadeAnimation = m_LineImage.DOFade(0.3f, sectionDuration * FADE_DURATION_RATIO)
									   .SetDelay(sectionDuration * FADE_START_RATIO)
									   .SetEase(Ease.OutQuart);

		// 3. 구역 종료 시 완전 페이드아웃
		var finalFadeAnimation = m_LineImage.DOFade(0f, 0.5f)
											.SetDelay(sectionDuration)
											.OnComplete(() =>
											{
												if (this != null && gameObject != null)
												{
													Destroy(gameObject);
												}
											});

		// 4. 펄스 효과 (구역 강조)
		var pulseAnimation = m_RectTransform.DOScaleY(PULSE_SCALE, PULSE_DURATION)
											.SetLoops(-1, LoopType.Yoyo)
											.SetEase(Ease.InOutSine);

		// 시퀀스에 애니메이션 추가
		m_AnimationSequence.Join(sizeAnimation);
		m_AnimationSequence.Join(fadeAnimation);
		m_AnimationSequence.Join(finalFadeAnimation);
		m_AnimationSequence.Join(pulseAnimation);

#if UNITY_EDITOR
		Debug.Log($"🎬 Section animation started: {m_SectionData.type}, duration: {sectionDuration}s");
#endif
	}

	/// <summary>
	/// 애니메이션 정리
	/// </summary>
	private void CleanupAnimations()
	{
		if (m_AnimationSequence != null && m_AnimationSequence.IsActive())
		{
			m_AnimationSequence.Kill();
			m_AnimationSequence = null;
		}
	}

	/// <summary>
	/// 캐시된 가로선 스프라이트 반환 (없으면 생성)
	/// </summary>
	/// <returns>가로선 스프라이트</returns>
	private static Sprite GetOrCreateLineSprite()
	{
		if (s_CachedLineSprite == null)
		{
			s_CachedLineSprite = CreateHorizontalLineSprite();
		}
		return s_CachedLineSprite;
	}

	/// <summary>
	/// 가로선 스프라이트 생성 (메모리 최적화)
	/// </summary>
	/// <returns>생성된 가로선 스프라이트</returns>
	private static Sprite CreateHorizontalLineSprite()
	{
		// 스프라이트 크기 설정
		const int SPRITE_WIDTH = 64;
		const int SPRITE_HEIGHT = 1;

		var texture = new Texture2D(SPRITE_WIDTH, SPRITE_HEIGHT);
		var colors = new Color[SPRITE_WIDTH * SPRITE_HEIGHT];

		// 전체를 흰색으로 채움
		for (int i = 0; i < colors.Length; i++)
		{
			colors[i] = Color.white;
		}

		texture.SetPixels(colors);
		texture.Apply();

		return Sprite.Create(texture, new Rect(0, 0, SPRITE_WIDTH, SPRITE_HEIGHT), new Vector2(0.5f, 0.5f));
	}
	#endregion
}
