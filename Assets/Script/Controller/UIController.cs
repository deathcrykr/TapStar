using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AmericanTruck
{
	/// <summary>
	/// UIController 클래스는 씬 로드 시 EventSystem과 InputSystemUIInputModule의 활성화 상태를 관리하며,
	/// iOS 및 노치 기기를 고려해 Safe Area를 적용하여 UI가 안전한 영역에 표시되도록 합니다.
	/// 각 방향(상하좌우)에 대해 개별 마진을 설정할 수 있습니다.
	/// </summary>
	public class UIController : MonoBehaviour
	{
		[Header("UI Components")]
		[Tooltip("UI 요소들을 포함하는 부모 GameObject입니다.")]
		[SerializeField] private GameObject m_Content;
		[SerializeField] private GameObject m_SafeArea;

		[Header("Safe Area Settings")]
		[Tooltip("왼쪽 SafeArea 적용 여부")]
		[SerializeField] private bool m_ApplyLeftSafeArea = true;
		[Tooltip("오른쪽 SafeArea 적용 여부")]
		[SerializeField] private bool m_ApplyRightSafeArea = true;
		[Tooltip("위쪽 SafeArea 적용 여부")]
		[SerializeField] private bool m_ApplyTopSafeArea = true;
		[Tooltip("아래쪽 SafeArea 적용 여부")]
		[SerializeField] private bool m_ApplyBottomSafeArea = true;

		[Header("Safe Area Margins")]
		[Tooltip("Safe Area에 추가할 왼쪽 마진 (픽셀 단위)")]
		[SerializeField] private float m_LeftMargin = 0f;
		[Tooltip("Safe Area에 추가할 오른쪽 마진 (픽셀 단위)")]
		[SerializeField] private float m_RightMargin = 0f;
		[Tooltip("Safe Area에 추가할 위쪽 마진 (픽셀 단위)")]
		[SerializeField] private float m_TopMargin = 0f;
		[Tooltip("Safe Area에 추가할 아래쪽 마진 (픽셀 단위)")]
		[SerializeField] private float m_BottomMargin = 0f;

		[Header("Debug")]
		[SerializeField] private bool m_DebugMode = false;

		private EventSystem m_EventSystem;
		private InputSystemUIInputModule m_InputSystem;

		// SafeArea 관련 변수
		private Rect m_LastSafeArea = new Rect(0, 0, 0, 0);
		private Vector2Int m_LastScreenSize = new Vector2Int(0, 0);
		private ScreenOrientation m_LastOrientation = ScreenOrientation.AutoRotation;
		private RectTransform m_SafeAreaRect;

		void Start()
		{
			if (m_Content != null)
			{
				// m_Content에서 EventSystem과 InputSystemUIInputModule 컴포넌트 가져오기
				m_EventSystem = m_Content.GetComponent<EventSystem>();
				m_InputSystem = m_Content.GetComponent<InputSystemUIInputModule>();

				// 씬 로드 이벤트에 메서드 등록
				SceneManager.sceneUnloaded += OnSceneUnloaded;

				// 현재 씬에 대해 초기화
				ActiveInputSystem();

				// SafeArea 적용을 위한 RectTransform 가져오기
				m_SafeAreaRect = m_SafeArea.GetComponent<RectTransform>();

				// 초기 SafeArea 적용
				ApplySafeArea();
			}
			else
			{
				Debug.LogError("m_Content가 설정되지 않았습니다.");
			}
		}

		void Update()
		{
			// 화면 방향 전환이나 해상도 변경 시 SafeArea 체크
			Rect safeArea = Screen.safeArea;
			if (safeArea != m_LastSafeArea
				|| Screen.width != m_LastScreenSize.x
				|| Screen.height != m_LastScreenSize.y
				|| Screen.orientation != m_LastOrientation)
			{
				ApplySafeArea();
			}
		}

		/// <summary>
		/// 씬 언로드 시 호출되어 입력 시스템을 다시 활성화합니다.
		/// </summary>
		/// <param name="scene">언로드된 씬</param>
		private void OnSceneUnloaded(Scene scene)
		{
			ActiveInputSystem();
		}

		/// <summary>
		/// MonoBehaviour가 파괴될 때 씬 언로드 이벤트 등록 해제
		/// </summary>
		private void OnDestroy()
		{
			SceneManager.sceneUnloaded -= OnSceneUnloaded;
		}

		/// <summary>
		/// 활성화된 EventSystem의 개수를 확인하여 로컬 컴포넌트를 활성화 또는 비활성화합니다.
		/// </summary>
		public void ActiveInputSystem()
		{
			// 모든 활성화된 EventSystem 컴포넌트 찾기
			EventSystem[] eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
			int activeEventSystemCount = 0;
			foreach (EventSystem eventSystem in eventSystems)
			{
				if (eventSystem.isActiveAndEnabled)
				{
					activeEventSystemCount++;
				}
			}
			if (activeEventSystemCount > 1)
			{
				if (m_EventSystem != null)
					m_EventSystem.enabled = false;
				if (m_InputSystem != null)
					m_InputSystem.enabled = false;
			}
			else if (activeEventSystemCount <= 1)
			{
				if (m_EventSystem != null)
					m_EventSystem.enabled = true;
				if (m_InputSystem != null)
					m_InputSystem.enabled = true;
			}
		}

		/// <summary>
		/// SafeArea를 적용해 UI가 노치 및 홈 인디케이터 등으로 가려지지 않도록 합니다.
		/// 각 방향별 SafeArea 적용과 추가 마진을 설정할 수 있습니다.
		/// </summary>
		private void ApplySafeArea()
		{
			if (m_SafeAreaRect == null)
				return;

			// 현재 화면 정보 업데이트
			Rect currentSafeArea = Screen.safeArea;
			m_LastSafeArea = currentSafeArea;
			m_LastScreenSize.x = Screen.width;
			m_LastScreenSize.y = Screen.height;
			m_LastOrientation = Screen.orientation;

			float leftInset = currentSafeArea.x;
			float rightInset = Screen.width - (currentSafeArea.x + currentSafeArea.width);
			float bottomInset = currentSafeArea.y;
			float topInset = Screen.height - (currentSafeArea.y + currentSafeArea.height);

			if (m_DebugMode)
			{
				Debug.LogFormat("[UIController] 원본 인셋: 좌={0}, 우={1}, 상={2}, 하={3}", leftInset, rightInset, topInset, bottomInset);
			}

			float appliedLeftInset = m_ApplyLeftSafeArea ? leftInset + m_LeftMargin : 0;
			float appliedRightInset = m_ApplyRightSafeArea ? rightInset + m_RightMargin : 0;
			float appliedBottomInset = m_ApplyBottomSafeArea ? bottomInset + m_BottomMargin : 0;
			float appliedTopInset = m_ApplyTopSafeArea ? topInset + m_TopMargin : 0;

			if (m_DebugMode)
			{
				Debug.LogFormat("[UIController] 계산된 인셋+마진: 좌={0}, 우={1}, 상={2}, 하={3}", appliedLeftInset, appliedRightInset, appliedTopInset, appliedBottomInset);
			}

			Rect finalRect = new Rect(
				appliedLeftInset,
				appliedBottomInset,
				Screen.width - (appliedLeftInset + appliedRightInset),
				Screen.height - (appliedBottomInset + appliedTopInset)
			);

			if (m_DebugMode)
			{
				Debug.LogFormat("[UIController] 최종 영역: x={0:F1}, y={1:F1}, 너비={2:F1}, 높이={3:F1}", finalRect.x, finalRect.y, finalRect.width, finalRect.height);
			}

			Vector2 anchorMin = new Vector2(finalRect.x / Screen.width, finalRect.y / Screen.height);
			Vector2 anchorMax = new Vector2((finalRect.x + finalRect.width) / Screen.width, (finalRect.y + finalRect.height) / Screen.height);

			// NaN/무한대 검사
			if (anchorMin.x >= 0 && anchorMin.y >= 0 && anchorMax.x >= 0 && anchorMax.y >= 0 &&
				!float.IsNaN(anchorMin.x) && !float.IsNaN(anchorMin.y) &&
				!float.IsNaN(anchorMax.x) && !float.IsNaN(anchorMax.y) &&
				!float.IsInfinity(anchorMin.x) && !float.IsInfinity(anchorMin.y) &&
				!float.IsInfinity(anchorMax.x) && !float.IsInfinity(anchorMax.y))
			{
				if (m_DebugMode)
				{
					Debug.LogFormat("[UIController] 적용될 앵커 값: Min=({0:F3}, {1:F3}), Max=({2:F3}, {3:F3})",
						anchorMin.x, anchorMin.y, anchorMax.x, anchorMax.y);
				}
				m_SafeAreaRect.anchorMin = anchorMin;
				m_SafeAreaRect.anchorMax = anchorMax;
				m_SafeAreaRect.offsetMin = Vector2.zero;
				m_SafeAreaRect.offsetMax = Vector2.zero;
			}
			else if (m_DebugMode)
			{
				Debug.LogWarning("[UIController] 앵커 값에 유효하지 않은 값이 포함되어 있어 SafeArea가 적용되지 않았습니다.");
			}

			if (m_DebugMode)
			{
				Debug.LogFormat("[UIController] Safe Area 적용 완료: 화면 크기: w={0}, h={1}", Screen.width, Screen.height);
				if (m_LeftMargin > 0 || m_RightMargin > 0 || m_TopMargin > 0 || m_BottomMargin > 0)
				{
					Debug.LogFormat("[UIController] 추가 마진: Left={0}, Right={1}, Top={2}, Bottom={3}",
						m_LeftMargin, m_RightMargin, m_TopMargin, m_BottomMargin);
				}
			}
		}

#if UNITY_EDITOR
		/// <summary>
		/// OnValidate: 에디터에서 속성이 변경되었을 때 SafeArea를 업데이트합니다.
		/// OnValidate는 SendMessage 호출 제한 문제가 있으므로, delayCall을 사용하여 OnValidate 완료 후에 실행합니다.
		/// </summary>
		private void OnValidate()
		{
			if (m_SafeAreaRect == null && m_SafeArea != null)
			{
				// m_SafeAreaRect가 아직 할당되지 않았다면 시도
				m_SafeAreaRect = m_SafeArea.GetComponent<RectTransform>();
			}
			// 에디터 모드에서 OnValidate 호출 시, 즉각적인 변경 대신 delayCall을 사용
			EditorApplication.delayCall += () =>
			{
				if (this != null && Application.isPlaying)
				{
					ApplySafeArea();
				}
			};
		}
#endif

		#region Gizmo Debug (Editor 전용)
#if UNITY_EDITOR
		private void OnDrawGizmosSelected()
		{
			if (!m_DebugMode || m_SafeAreaRect == null)
				return;

			Rect currentSafeArea = Screen.safeArea;
			Gizmos.color = Color.green;
			DrawRectGizmo(currentSafeArea);

			float leftInset = currentSafeArea.x;
			float rightInset = Screen.width - (currentSafeArea.x + currentSafeArea.width);
			float bottomInset = currentSafeArea.y;
			float topInset = Screen.height - (currentSafeArea.y + currentSafeArea.height);

			float appliedLeftInset = m_ApplyLeftSafeArea ? leftInset + m_LeftMargin : 0;
			float appliedRightInset = m_ApplyRightSafeArea ? rightInset + m_RightMargin : 0;
			float appliedBottomInset = m_ApplyBottomSafeArea ? bottomInset + m_BottomMargin : 0;
			float appliedTopInset = m_ApplyTopSafeArea ? topInset + m_TopMargin : 0;

			Rect adjustedRect = new Rect(
				appliedLeftInset,
				appliedBottomInset,
				Screen.width - (appliedLeftInset + appliedRightInset),
				Screen.height - (appliedBottomInset + appliedTopInset)
			);

			Gizmos.color = Color.yellow;
			DrawRectGizmo(adjustedRect);
		}

		private void DrawRectGizmo(Rect rect)
		{
			Vector3 worldPos = m_SafeAreaRect.TransformPoint(new Vector3(rect.x, rect.y, 0));
			Vector3 worldSize = m_SafeAreaRect.TransformPoint(new Vector3(rect.width, rect.height, 0)) - worldPos;
			Gizmos.DrawWireCube(worldPos + worldSize * 0.5f, worldSize);
		}
#endif
		#endregion
	}
}
