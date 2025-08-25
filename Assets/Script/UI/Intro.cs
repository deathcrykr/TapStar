using DG.Tweening;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TapStar.UI
{
	public class Intro : MonoBehaviour
	{
		[Header("Page Variables")]
		[Tooltip("현재 페이지 활성화 상태를 나타내는 변수")]
		[SerializeField] private BoolVariable _isPageActive;
		[Tooltip("다음 페이지 활성화 상태를 나타내는 변수")]
		[SerializeField] private BoolVariable _isNextPageActive;

		[Header("UI Components")]
		[Tooltip("페이드 인/아웃 효과를 위한 캔버스 그룹")]
		[SerializeField] private CanvasGroup m_CanvasGroup;

		[Header("Animation Settings")]
		[Tooltip("페이드 인 애니메이션 지속 시간")]
		[SerializeField] private float m_FadeInDuration = 1f;
		[Tooltip("페이드 아웃 애니메이션 지속 시간")]
		[SerializeField] private float m_FadeOutDuration = 1f;

		private bool m_IsLoading = true;


		/// <summary>
		/// 컴포넌트 초기화 및 인트로 애니메이션 시작
		/// </summary>
		private void Start()
		{
			// CanvasGroup이 할당되지 않았다면 자동으로 추가 및 할당
			if (m_CanvasGroup == null)
			{
				m_CanvasGroup = GetComponent<CanvasGroup>();
				if (m_CanvasGroup == null)
				{
					m_CanvasGroup = gameObject.AddComponent<CanvasGroup>();
				}
			}

			ShowIntro();
		}

		/// <summary>
		/// 매 프레임마다 사용자 입력을 확인 (웹/모바일 플랫폼별 최적화)
		/// </summary>
		private void Update()
		{
			if (!m_IsLoading && IsInputDetected())
			{
				OnScreenClick();
			}
		}

		/// <summary>
		/// 플랫폼별로 최적화된 입력 감지
		/// </summary>
		/// <returns>입력이 감지되었는지 여부</returns>
		private bool IsInputDetected()
		{
#if UNITY_ANDROID || UNITY_IOS
			// 모바일 플랫폼: 터치 입력 감지
			return Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
#elif UNITY_WEBGL || UNITY_STANDALONE
			// 웹/PC 플랫폼: 마우스 클릭 감지
			return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
			// 기타 플랫폼: 둘 다 지원
			if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
				return true;
			if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
				return true;
			return false;
#endif
		}

		/// <summary>
		/// 인트로 페이드 인 애니메이션을 실행 (스케일 바운스 효과 포함)
		/// </summary>
		private void ShowIntro()
		{
			// 초기 상태 설정
			m_CanvasGroup.alpha = 0;
			transform.localScale = Vector3.one * 10f;

			// 페이드 인 애니메이션
			DOTween.To(() => m_CanvasGroup.alpha, x => m_CanvasGroup.alpha = x, 1f, m_FadeInDuration);

			// 스케일 바운스 애니메이션 (10에서 1로)
			transform.DOScale(1f, m_FadeInDuration)
				.SetEase(Ease.OutBounce)
				.OnComplete(() =>
				{
					m_IsLoading = false;
				});
		}

		/// <summary>
		/// 화면 클릭 시 페이드 아웃 및 페이지 전환 처리
		/// </summary>
		private void OnScreenClick()
		{
			DOTween.To(() => m_CanvasGroup.alpha, x => m_CanvasGroup.alpha = x, 0f, m_FadeOutDuration)
				.OnComplete(() =>
				{
					_isPageActive.Value = false;
					_isNextPageActive.Value = true;
					Destroy(gameObject);
				});
		}
	}

}
