using DG.Tweening;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.UI;

namespace TapStar.UI
{
	public class Game : MonoBehaviour
	{
		[Header("Page Variables")]
		[Tooltip("현재 페이지 활성화 상태를 나타내는 변수")]
		[SerializeField] private BoolVariable _isPageActive;

		[Header("UI Components")]
		[Tooltip("상단 UI 영역 활성화 상태")]
		[SerializeField] private BoolVariable _isTopActive;
		[Tooltip("사이드바 UI 영역 활성화 상태")]
		[SerializeField] private BoolVariable _isSidebarActive;
		[Tooltip("하단 UI 영역 활성화 상태")]
		[SerializeField] private BoolVariable _isFooterActive;

		private bool m_IsAnimationComplete = false;

		private void Awake()
		{
			if (_isPageActive != null)
			{
				// _isPageActive 값 변화 감지 이벤트 등록
				_isPageActive.OnValueChanged += OnPageActiveChanged;
				if (!_isPageActive.Value)
				{
					_isTopActive.Value = false;
					_isSidebarActive.Value = false;
					_isFooterActive.Value = false;
					gameObject.SetActive(false);
				}
			}
		}
		/// <summary>
		/// 게임 페이지 초기화 및 이벤트 등록
		/// </summary>
		private void Start()
		{
			InitializeGamePage();
		}

		/// <summary>
		/// 컴포넌트가 파괴될 때 이벤트 해제
		/// </summary>
		private void OnDestroy()
		{
			if (_isPageActive != null)
			{
				_isPageActive.OnValueChanged -= OnPageActiveChanged;
			}
		}

		/// <summary>
		/// 게임 페이지 초기화 및 이벤트 등록
		/// </summary>
		private void InitializeGamePage()
		{
			// 현재 값에 따라 초기 상태 설정
			OnPageActiveChanged(_isPageActive.Value);
		}

		/// <summary>
		/// _isPageActive 값이 변경될 때 호출되는 메서드
		/// </summary>
		/// <param name="isActive">페이지 활성화 상태</param>
		private void OnPageActiveChanged(bool isActive)
		{
			if (isActive)
			{
				gameObject.SetActive(true);
				// 페이지가 활성화되면 모든 UI 요소 활성화
				_isTopActive.Value = true;
				_isSidebarActive.Value = true;
				_isFooterActive.Value = true;

				// 애니메이션 완료 후 GameManager를 통해 노래 호출
				if (!m_IsAnimationComplete)
				{
					m_IsAnimationComplete = true;
					CallSongFromGameManager();
				}
			}
			else
			{
				gameObject.SetActive(false);
				// 페이지가 비활성화되면 모든 UI 요소 비활성화
				_isTopActive.Value = false;
				_isSidebarActive.Value = false;
				_isFooterActive.Value = false;
				m_IsAnimationComplete = false;
			}
		}

		/// <summary>
		/// GameManager를 통해 노래를 호출
		/// </summary>
		private void CallSongFromGameManager()
		{
			// TODO: GameManager가 구현되면 여기서 노래 호출
			GameManager.Instance.StartGame();
			Debug.Log("UI 활성화 완료 - GameManager를 통해 노래 호출 준비 완료");
		}

	}
}
