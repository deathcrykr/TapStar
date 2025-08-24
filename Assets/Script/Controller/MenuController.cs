using System;
using DG.Tweening;
using Obvious.Soap;
using UnityEngine;

namespace TapStar.Controller
{
	/// <summary>
	/// 메뉴 컨트롤러 클래스입니다. Soap 변수를 기반으로 지정된 위치, 회전, 스케일로 애니메이션합니다.
	/// </summary>
	public class MenuController : MonoBehaviour
	{
		/// <summary>
		/// 메뉴의 위치, 회전, 스케일을 정의하는 클래스입니다.
		/// </summary>
		[System.Serializable]
		public class AnimationSettings
		{
			[Tooltip("애니메이션 지속 시간")]
			public float Duration = 0.3f;

			[Tooltip("애니메이션 곡선 설정")]
			public Ease EaseType = Ease.InOutQuad;

			[Tooltip("메뉴의 여백 (x=Left, y=Top, z=Right, w=Bottom)")]
			public Vector4 AnchoredPosition = Vector4.zero;

			[Tooltip("메뉴의 회전")]
			public Vector3 Rotation = Vector3.zero;

			[Tooltip("메뉴의 스케일")]
			public Vector3 Scale = new Vector3(1.0f, 1.0f, 1.0f);

			[Tooltip("메뉴의 투명도 (alpha)")]
			[Range(0f, 1f)]
			public float Alpha = 1.0f;

			/// <summary>
			/// AnchoredPosition을 offsetMin (Left, Bottom)으로 변환
			/// </summary>
			public Vector2 GetOffsetMin() => new Vector2(AnchoredPosition.x, AnchoredPosition.w);

			/// <summary>
			/// AnchoredPosition을 offsetMax (-Right, -Top)으로 변환
			/// </summary>
			public Vector2 GetOffsetMax() => new Vector2(-AnchoredPosition.z, -AnchoredPosition.y);
		}

		[Header("Soap Variables")]
		[Tooltip("메뉴를 제어하는 BoolVariable")]
		[SerializeField] private BoolVariable _toggleValue;
		public BoolVariable ToggleValue => _toggleValue;

		[Header("UI Components")]
		[Tooltip("컨텐츠 오브젝트")]
		[SerializeField] private GameObject m_ContentObject;

		[Header("Animation Settings")]
		[Tooltip("메뉴가 활성화될 때의 설정")]
		[SerializeField] private AnimationSettings m_ShowSettings;

		[Tooltip("메뉴가 비활성화될 때의 설정")]
		[SerializeField] private AnimationSettings m_HideSettings;

		private bool _isOpen = false;
		private Sequence _currentAnimation;

		private RectTransform m_MenuRectTransform;
		private CanvasGroup m_CanvasGroup;

		private void Start()
		{
			InitializeComponents();

			if (_toggleValue != null)
			{
				// 초기 Soap 값에 따라 애니메이션을 처리
				if (_toggleValue.Value)
				{
					// 초기값이 true이면, Hide 상태로 설정 후 Show 애니메이션 실행
					SetTransformValues(m_HideSettings);
					Show();
				}
				else
				{
					SetTransformValues(m_HideSettings);
				}
			}
		}

		private void OnDestroy()
		{
			// Soap 변수의 이벤트 리스너 해제
			if (_toggleValue != null)
			{
				_toggleValue.OnValueChanged -= OnToggle;
			}

			// 현재 진행 중인 애니메이션 중지
			_currentAnimation?.Kill();
		}

		/// <summary>
		/// 컴포넌트들을 초기화합니다.
		/// </summary>
		private void InitializeComponents()
		{
			// Soap 변수에 대한 초기 값을 설정
			if (_toggleValue != null)
			{
				_isOpen = _toggleValue.Value;
				_toggleValue.OnValueChanged += OnToggle;
			}

			// RectTransform 컴포넌트를 가져옵니다.
			if (!TryGetComponent(out m_MenuRectTransform))
			{
				Debug.LogError("[MenuController] RectTransform 컴포넌트를 찾을 수 없습니다.");
				return;
			}

			// CanvasGroup을 가져오거나 없으면 새로 추가합니다.
			if (!TryGetComponent(out m_CanvasGroup))
			{
				m_CanvasGroup = gameObject.AddComponent<CanvasGroup>();
			}
		}

		/// <summary>
		/// Transform의 Position, Rotation, Scale, Alpha 값을 설정합니다.
		/// </summary>
		private void SetTransformValues(AnimationSettings settings)
		{
			// 앵커가 늘어진 상태인지 확인
			bool isStretchedX = !Mathf.Approximately(m_MenuRectTransform.anchorMin.x, m_MenuRectTransform.anchorMax.x);
			bool isStretchedY = !Mathf.Approximately(m_MenuRectTransform.anchorMin.y, m_MenuRectTransform.anchorMax.y);

			if (isStretchedX || isStretchedY)
			{
				// 늘어진 앵커: 각 축별로 개별 처리
				Vector2 currentOffsetMin = m_MenuRectTransform.offsetMin;
				Vector2 currentOffsetMax = m_MenuRectTransform.offsetMax;

				if (isStretchedX)
				{
					// X축이 늘어진 경우에만 Left, Right 설정
					currentOffsetMin.x = settings.AnchoredPosition.x; // Left
					currentOffsetMax.x = -settings.AnchoredPosition.z; // -Right
				}

				if (isStretchedY)
				{
					// Y축이 늘어진 경우에만 Top, Bottom 설정
					currentOffsetMin.y = settings.AnchoredPosition.w; // Bottom
					currentOffsetMax.y = -settings.AnchoredPosition.y; // -Top
				}

				m_MenuRectTransform.offsetMin = currentOffsetMin;
				m_MenuRectTransform.offsetMax = currentOffsetMax;

				// 늘어지지 않은 축은 anchoredPosition으로 처리
				if (!isStretchedX || !isStretchedY)
				{
					Vector2 currentAnchoredPos = m_MenuRectTransform.anchoredPosition;

					if (!isStretchedX)
					{
						// X축이 늘어지지 않은 경우: X 위치 설정
						currentAnchoredPos.x = settings.AnchoredPosition.x;
					}

					if (!isStretchedY)
					{
						// Y축이 늘어지지 않은 경우: Y 위치 설정
						currentAnchoredPos.y = settings.AnchoredPosition.y;
					}

					m_MenuRectTransform.anchoredPosition = currentAnchoredPos;
				}
			}
			else
			{
				// 점 앵커: Vector4의 x, y 값을 anchoredPosition으로 사용
				m_MenuRectTransform.anchoredPosition = new Vector2(settings.AnchoredPosition.x, settings.AnchoredPosition.y);
			}

			m_MenuRectTransform.localEulerAngles = settings.Rotation;
			m_MenuRectTransform.localScale = settings.Scale;
			if (m_CanvasGroup != null)
			{
				m_CanvasGroup.alpha = settings.Alpha;
			}
		}

		/// <summary>
		/// 메뉴의 활성화 상태가 변경될 때 호출되는 메서드입니다.
		/// </summary>
		public void OnToggle(bool newValue)
		{
			// 현재 상태와 새로운 값이 같다면 무시합니다.
			if (_isOpen == newValue) return;

			_isOpen = newValue;

			// 현재 진행 중인 애니메이션이 있으면 중지합니다.
			_currentAnimation?.Kill();

			if (newValue)
			{
				Show();
			}
			else
			{
				Hide();
			}
		}

		/// <summary>
		/// 메뉴를 활성화 상태로 전환합니다.
		/// </summary>
		private void Show()
		{
			if (TryGetComponent(out IMenu menu))
			{
				menu.OnShow();
			}

			m_ContentObject?.SetActive(true);
			Animate(m_ShowSettings);
		}

		/// <summary>
		/// 메뉴를 비활성화 상태로 전환합니다.
		/// </summary>
		private void Hide()
		{
			if (TryGetComponent(out IMenu menu))
			{
				menu.OnHide();
			}

			Animate(m_HideSettings, () => m_ContentObject?.SetActive(false));
		}

		/// <summary>
		/// 지정된 설정으로 Transform을 애니메이션합니다.
		/// </summary>
		/// <param name="settings">애니메이션 설정</param>
		/// <param name="onComplete">애니메이션 완료 시 호출될 콜백 (옵션)</param>
		private void Animate(AnimationSettings settings, Action onComplete = null)
		{
			// 애니메이션을 병렬로 실행하기 위한 Sequence 생성
			_currentAnimation = DOTween.Sequence()
				.SetUpdate(true) // Unscaled Time 적용
				.OnComplete(() => onComplete?.Invoke());

			if (m_MenuRectTransform != null)
			{
				// 앵커가 늘어진 상태인지 확인
				bool isStretchedX = !Mathf.Approximately(m_MenuRectTransform.anchorMin.x, m_MenuRectTransform.anchorMax.x);
				bool isStretchedY = !Mathf.Approximately(m_MenuRectTransform.anchorMin.y, m_MenuRectTransform.anchorMax.y);

				if (isStretchedX || isStretchedY)
				{
					// 늘어진 앵커: 각 축별로 개별 애니메이션
					if (isStretchedX)
					{
						// X축이 늘어진 경우: offsetMin.x, offsetMax.x 애니메이션
						_currentAnimation.Join(DOTween.To(
							() => m_MenuRectTransform.offsetMin.x,
							(float value) =>
							{
								Vector2 offsetMin = m_MenuRectTransform.offsetMin;
								offsetMin.x = value;
								m_MenuRectTransform.offsetMin = offsetMin;
							},
							settings.AnchoredPosition.x, // Left
							settings.Duration
						).SetEase(settings.EaseType));

						_currentAnimation.Join(DOTween.To(
							() => m_MenuRectTransform.offsetMax.x,
							(float value) =>
							{
								Vector2 offsetMax = m_MenuRectTransform.offsetMax;
								offsetMax.x = value;
								m_MenuRectTransform.offsetMax = offsetMax;
							},
							-settings.AnchoredPosition.z, // -Right
							settings.Duration
						).SetEase(settings.EaseType));
					}

					if (isStretchedY)
					{
						// Y축이 늘어진 경우: offsetMin.y, offsetMax.y 애니메이션
						_currentAnimation.Join(DOTween.To(
							() => m_MenuRectTransform.offsetMin.y,
							(float value) =>
							{
								Vector2 offsetMin = m_MenuRectTransform.offsetMin;
								offsetMin.y = value;
								m_MenuRectTransform.offsetMin = offsetMin;
							},
							settings.AnchoredPosition.w, // Bottom
							settings.Duration
						).SetEase(settings.EaseType));

						_currentAnimation.Join(DOTween.To(
							() => m_MenuRectTransform.offsetMax.y,
							(float value) =>
							{
								Vector2 offsetMax = m_MenuRectTransform.offsetMax;
								offsetMax.y = value;
								m_MenuRectTransform.offsetMax = offsetMax;
							},
							-settings.AnchoredPosition.y, // -Top
							settings.Duration
						).SetEase(settings.EaseType));
					}

					// 늘어지지 않은 축은 anchoredPosition으로 애니메이션
					if (!isStretchedX)
					{
						// X축이 늘어지지 않은 경우: anchoredPosition.x 애니메이션
						_currentAnimation.Join(DOTween.To(
							() => m_MenuRectTransform.anchoredPosition.x,
							(float value) =>
							{
								Vector2 anchoredPos = m_MenuRectTransform.anchoredPosition;
								anchoredPos.x = value;
								m_MenuRectTransform.anchoredPosition = anchoredPos;
							},
							settings.AnchoredPosition.x,
							settings.Duration
						).SetEase(settings.EaseType));
					}

					if (!isStretchedY)
					{
						// Y축이 늘어지지 않은 경우: anchoredPosition.y 애니메이션
						_currentAnimation.Join(DOTween.To(
							() => m_MenuRectTransform.anchoredPosition.y,
							(float value) =>
							{
								Vector2 anchoredPos = m_MenuRectTransform.anchoredPosition;
								anchoredPos.y = value;
								m_MenuRectTransform.anchoredPosition = anchoredPos;
							},
							settings.AnchoredPosition.y,
							settings.Duration
						).SetEase(settings.EaseType));
					}
				}
				else
				{
					// 점 앵커: anchoredPosition 애니메이션 (Vector4의 x, y 사용)
					Vector2 targetPosition = new Vector2(settings.AnchoredPosition.x, settings.AnchoredPosition.y);
					_currentAnimation.Join(DOTween.To(
						() => m_MenuRectTransform.anchoredPosition,
						(Vector2 value) => m_MenuRectTransform.anchoredPosition = value,
						targetPosition,
						settings.Duration
					).SetEase(settings.EaseType));
				}

				// 회전 애니메이션
				_currentAnimation.Join(DOTween.To(
					() => m_MenuRectTransform.localEulerAngles,
					(Vector3 value) => m_MenuRectTransform.localEulerAngles = value,
					settings.Rotation,
					settings.Duration
				).SetEase(settings.EaseType));

				// 스케일 애니메이션
				_currentAnimation.Join(DOTween.To(
					() => m_MenuRectTransform.localScale,
					(Vector3 value) => m_MenuRectTransform.localScale = value,
					settings.Scale,
					settings.Duration
				).SetEase(settings.EaseType));
			}

			// CanvasGroup이 있는 경우 alpha 값을 애니메이션
			if (m_CanvasGroup != null)
			{
				_currentAnimation.Join(DOTween.To(
					() => m_CanvasGroup.alpha,
					(float value) => m_CanvasGroup.alpha = value,
					settings.Alpha,
					settings.Duration
				).SetEase(settings.EaseType));
			}
		}
	}
}
