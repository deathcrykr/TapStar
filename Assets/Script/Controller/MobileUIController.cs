using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TapStar.Controller
{
	/// <summary>
	/// 모바일 리듬 게임 UI 관리 클래스
	/// </summary>
	public class MobileUIController : MonoBehaviour
	{
		#region Constants
		/// <summary>
		/// 모바일 UI 기준 해상도 (가로x세로)
		/// </summary>
		private static readonly Vector2 REFERENCE_RESOLUTION = new Vector2(1080, 1920);

		/// <summary>
		/// UI 스케일링 매치 비율 (0=가로 기준, 1=세로 기준, 0.5=혼합)
		/// </summary>
		private const float MATCH_WIDTH_HEIGHT = 0.5f;

		/// <summary>
		/// 중앙 타겟의 기본 색상 (RGBA)
		/// </summary>
		private static readonly Color TARGET_COLOR = new Color(1f, 1f, 1f, 0.8f);
		#endregion

		#region Serialized Fields
		[Header("UI References")]
		/// <summary>
		/// 게임 시작/재시작 버튼
		/// </summary>
		[SerializeField] private Button StartButton;

		/// <summary>
		/// 점수 표시 텍스트
		/// </summary>
		[SerializeField] private Text ScoreText;

		/// <summary>
		/// 게임 안내 텍스트
		/// </summary>
		[SerializeField] private Text InstructionText;

		/// <summary>
		/// 중앙 타겟 오브젝트
		/// </summary>
		[SerializeField] private GameObject CenterTarget;

		/// <summary>
		/// 게임 캔버스
		/// </summary>
		[SerializeField] private Canvas GameCanvas;

		[Header("Mobile Settings")]
		/// <summary>
		/// 목표 프레임 레이트
		/// </summary>
		[SerializeField] private float TargetFrameRate = 60f;
		#endregion

		#region Unity Lifecycle
		/// <summary>
		/// Unity Start 메서드 - UI 초기화
		/// </summary>
		private void Start()
		{
			SetupMobile();
			SetupUI();
		}
		#endregion

		#region Public Methods
		/// <summary>
		/// 점수 업데이트 및 시각적 효과 표시
		/// </summary>
		/// <param name="score">업데이트할 점수</param>
		public void UpdateScore(int score)
		{
			// 펀치 애니메이션 설정
			const float SCORE_PUNCH_SCALE = 0.15f;
			const float SCORE_PUNCH_DURATION = 0.3f;
			const int SCORE_PUNCH_VIBRATO = 5;
			const float SCORE_PUNCH_ELASTICITY = 0.3f;

			// 색상 애니메이션 설정
			const float SCORE_COLOR_DURATION = 0.1f;
			const float SCORE_COLOR_RETURN_DURATION = 0.2f;

			if (ScoreText != null)
			{
				ScoreText.text = "점수: " + score.ToString("N0");

				// DOTween 점수 업데이트 효과
				ScoreText.transform.DOKill();
				ScoreText.transform.DOPunchScale(Vector3.one * SCORE_PUNCH_SCALE, SCORE_PUNCH_DURATION,
												 SCORE_PUNCH_VIBRATO, SCORE_PUNCH_ELASTICITY);

				// 색상 효과
				Color originalColor = ScoreText.color;
				ScoreText.DOColor(Color.yellow, SCORE_COLOR_DURATION).OnComplete(() =>
				{
					ScoreText.DOColor(originalColor, SCORE_COLOR_RETURN_DURATION);
				});
			}
		}

		/// <summary>
		/// 게임 오버 UI 표시
		/// </summary>
		/// <param name="finalScore">최종 점수</param>
		public void ShowGameOver(int finalScore)
		{
			// 애니메이션 설정
			const float BUTTON_SCALE_DURATION = 0.5f;
			const float TEXT_FADE_DURATION = 0.8f;
			const float TEXT_FADE_DELAY = 0.3f;

			if (StartButton != null)
			{
				StartButton.gameObject.SetActive(true);
				StartButton.GetComponentInChildren<Text>().text = "다시 시작";

				// DOTween 버튼 등장 효과
				StartButton.transform.localScale = Vector3.zero;
				StartButton.transform.DOScale(1f, BUTTON_SCALE_DURATION).SetEase(Ease.OutBounce);
			}

			if (InstructionText != null)
			{
				InstructionText.gameObject.SetActive(true);
				InstructionText.text = $"게임 종료! 최종 점수: {finalScore:N0}\n다시 시작하려면 버튼을 터치하세요";

				// DOTween 텍스트 페이드인 효과
				InstructionText.color = new Color(InstructionText.color.r, InstructionText.color.g,
												  InstructionText.color.b, 0f);
				InstructionText.DOFade(1f, TEXT_FADE_DURATION).SetDelay(TEXT_FADE_DELAY);
			}
		}
		#endregion

		#region Private Methods
		/// <summary>
		/// 모바일 최적화 설정
		/// </summary>
		private void SetupMobile()
		{
			// 모바일 최적화 설정
			Application.targetFrameRate = (int)TargetFrameRate;

			// 화면 해상도 최적화
			if (Application.isMobilePlatform)
			{
				Screen.sleepTimeout = SleepTimeout.NeverSleep;

				// UI 스케일링
				CanvasScaler scaler = GameCanvas.GetComponent<CanvasScaler>();
				if (scaler == null)
				{
					scaler = GameCanvas.gameObject.AddComponent<CanvasScaler>();
				}

				scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
				scaler.referenceResolution = REFERENCE_RESOLUTION;
				scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
				scaler.matchWidthOrHeight = MATCH_WIDTH_HEIGHT;
			}
		}

		/// <summary>
		/// UI 컴포넌트들 초기 설정
		/// </summary>
		private void SetupUI()
		{
			SetupStartButton();
			SetupInstructionText();
			SetupCenterTarget();
		}

		/// <summary>
		/// 시작 버튼 설정
		/// </summary>
		private void SetupStartButton()
		{
			if (StartButton != null)
			{
				StartButton.onClick.AddListener(() =>
				{
					// GameManager 유효성 확인
					if (GameManager.Instance == null)
					{
						Debug.LogWarning("GameManager Instance is null!");
						return;
					}

					// 이미 게임이 실행중인지 확인
					if (GameManager.Instance.IsPlaying)
					{
						Debug.Log("🎮 Game is already playing - Start button ignored");
						return;
					}

					Debug.Log("🎮 Start button clicked - Starting game...");
					GameManager.Instance.StartGame();
					StartButton.gameObject.SetActive(false);
					if (InstructionText != null)
						InstructionText.gameObject.SetActive(false);
				});
			}
		}

		/// <summary>
		/// 안내 텍스트 설정
		/// </summary>
		private void SetupInstructionText()
		{
			if (InstructionText != null)
			{
				if (Application.isMobilePlatform)
				{
					InstructionText.text = "화면을 터치하여 리듬에 맞춰 연주하세요!";
				}
				else
				{
					InstructionText.text = "스페이스바나 마우스 클릭으로 연주하세요!";
				}
			}
		}

		/// <summary>
		/// 중앙 타겟 설정
		/// </summary>
		private void SetupCenterTarget()
		{
			if (CenterTarget != null)
			{
				Image targetImage = CenterTarget.GetComponent<Image>();
				if (targetImage == null)
				{
					targetImage = CenterTarget.AddComponent<Image>();
				}

				targetImage.sprite = CreateTargetSprite();
				targetImage.color = TARGET_COLOR;
			}
		}

		/// <summary>
		/// 타겟 스프라이트 생성 (원형 링과 중앙점)
		/// </summary>
		/// <returns>생성된 타겟 스프라이트</returns>
		private Sprite CreateTargetSprite()
		{
			// 타겟 스프라이트 설정
			const int TARGET_SPRITE_SIZE = 64;
			const float TARGET_RING_WIDTH = 4f;
			const float TARGET_CENTER_SIZE = 4f;

			Texture2D texture = new Texture2D(TARGET_SPRITE_SIZE, TARGET_SPRITE_SIZE);

			Vector2 center = new Vector2(TARGET_SPRITE_SIZE / 2f, TARGET_SPRITE_SIZE / 2f);
			float radius = TARGET_SPRITE_SIZE / 2f - 2f;

			for (int x = 0; x < TARGET_SPRITE_SIZE; x++)
			{
				for (int y = 0; y < TARGET_SPRITE_SIZE; y++)
				{
					float distance = Vector2.Distance(new Vector2(x, y), center);

					if (distance <= radius && distance >= radius - TARGET_RING_WIDTH)
					{
						texture.SetPixel(x, y, Color.white);
					}
					else if (distance <= TARGET_CENTER_SIZE)
					{
						texture.SetPixel(x, y, Color.white);
					}
					else
					{
						texture.SetPixel(x, y, Color.clear);
					}
				}
			}

			texture.Apply();
			return Sprite.Create(texture, new Rect(0, 0, TARGET_SPRITE_SIZE, TARGET_SPRITE_SIZE), new Vector2(0.5f, 0.5f));
		}
		#endregion
	}
}
