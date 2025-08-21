using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 리듬 게임 씬 자동 설정 클래스
/// </summary>
public class AutoRhythmGameSetup : MonoBehaviour
{
	#region Unity Lifecycle
	/// <summary>
	/// Unity Start 메서드 - 리듬 게임 씬 자동 설정 시작
	/// </summary>
	private void Start()
	{
		Setup();
	}
	#endregion

	#region Private Methods
	/// <summary>
	/// 리듬 게임 씬의 모든 필수 컴포넌트들을 자동으로 설정
	/// </summary>
	private void Setup()
	{
#if UNITY_EDITOR
		Debug.Log("🔧 Auto-setting up rhythm game scene...");
#endif

		// 1. Canvas 생성
		Canvas canvas = CreateOrFindCanvas();

		// 2. EventSystem 생성
		CreateEventSystem();

		// 3. RhythmGameManager 생성
		RhythmGameManager rhythmManager = CreateOrFindManager(canvas);

		// 4. AudioManager 생성
		CreateAudioManager();

		// 5. 점수 텍스트 생성
		CreateScoreText(canvas, rhythmManager);

		// 6. 시작 안내 텍스트 생성
		CreateStartText(canvas);

#if UNITY_EDITOR
		Debug.Log("🚀 Rhythm game scene setup complete!");
#endif
	}

	/// <summary>
	/// Canvas를 생성하거나 기존 Canvas를 찾아서 반환
	/// </summary>
	/// <returns>Canvas 컴포넌트</returns>
	private Canvas CreateOrFindCanvas()
	{
		Canvas canvas = FindFirstObjectByType<Canvas>();
		if (canvas == null)
		{
#if UNITY_EDITOR
			Debug.Log("📱 Creating Canvas...");
#endif
			GameObject canvasGO = new GameObject("Canvas");
			canvas = canvasGO.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvasGO.AddComponent<CanvasScaler>();
			canvasGO.AddComponent<GraphicRaycaster>();
		}
		return canvas;
	}

	/// <summary>
	/// EventSystem이 없으면 생성
	/// </summary>
	private void CreateEventSystem()
	{
		if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
		{
#if UNITY_EDITOR
			Debug.Log("🎮 Creating EventSystem...");
#endif
			GameObject eventSystemGO = new GameObject("EventSystem");
			eventSystemGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
			eventSystemGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
		}
	}

	/// <summary>
	/// RhythmGameManager를 생성하거나 기존 매니저를 찾아서 반환
	/// </summary>
	/// <param name="canvas">연결할 Canvas</param>
	/// <returns>RhythmGameManager 컴포넌트</returns>
	private RhythmGameManager CreateOrFindManager(Canvas canvas)
	{
		RhythmGameManager rhythmManager = FindFirstObjectByType<RhythmGameManager>();
		if (rhythmManager == null)
		{
#if UNITY_EDITOR
			Debug.Log("🎵 Creating RhythmGameManager...");
#endif
			GameObject managerGO = new GameObject("RhythmGameManager");
			rhythmManager = managerGO.AddComponent<RhythmGameManager>();

			// Canvas 연결
			rhythmManager.GameCanvas = canvas;

			// 중앙 타겟 생성
			rhythmManager.CenterTarget = CreateCenterTarget(canvas);

#if UNITY_EDITOR
			Debug.Log("✅ RhythmGameManager setup complete!");
#endif
		}
		return rhythmManager;
	}

	/// <summary>
	/// 중앙 타겟 오브젝트 생성
	/// </summary>
	/// <param name="canvas">부모 Canvas</param>
	/// <returns>생성된 중앙 타겟의 Transform</returns>
	private Transform CreateCenterTarget(Canvas canvas)
	{
		// 중앙 타겟 설정
		const float CENTER_TARGET_SIZE = 50f;
		var CENTER_TARGET_COLOR = new Color(1f, 1f, 1f, 0.8f);

		GameObject centerGO = new GameObject("CenterTarget");
		centerGO.transform.SetParent(canvas.transform);

		var centerRect = centerGO.AddComponent<RectTransform>();
		centerRect.anchoredPosition = Vector2.zero;
		centerRect.sizeDelta = new Vector2(CENTER_TARGET_SIZE, CENTER_TARGET_SIZE);

		var centerImage = centerGO.AddComponent<Image>();
		centerImage.color = CENTER_TARGET_COLOR;

		return centerGO.transform;
	}

	/// <summary>
	/// AudioManager가 없으면 생성
	/// </summary>
	private void CreateAudioManager()
	{
		if (FindFirstObjectByType<AudioManager>() == null)
		{
#if UNITY_EDITOR
			Debug.Log("🔊 Creating AudioManager...");
#endif
			GameObject audioGO = new GameObject("AudioManager");
			audioGO.AddComponent<AudioManager>();
		}
	}

	/// <summary>
	/// 점수 텍스트가 없으면 생성
	/// </summary>
	/// <param name="canvas">부모 Canvas</param>
	/// <param name="rhythmManager">연결할 RhythmGameManager</param>
	private void CreateScoreText(Canvas canvas, RhythmGameManager rhythmManager)
	{
		// 점수 텍스트 설정
		const int SCORE_FONT_SIZE = 24;
		const float SCORE_TEXT_WIDTH = 200f;
		const float SCORE_TEXT_HEIGHT = 50f;
		var SCORE_TEXT_POSITION = new Vector2(100, -50);

		if (GameObject.Find("ScoreText") == null)
		{
#if UNITY_EDITOR
			Debug.Log("📊 Creating Score Text...");
#endif
			GameObject scoreGO = new GameObject("ScoreText");
			scoreGO.transform.SetParent(canvas.transform);

			var scoreRect = scoreGO.AddComponent<RectTransform>();
			scoreRect.anchorMin = new Vector2(0, 1);
			scoreRect.anchorMax = new Vector2(0, 1);
			scoreRect.anchoredPosition = SCORE_TEXT_POSITION;
			scoreRect.sizeDelta = new Vector2(SCORE_TEXT_WIDTH, SCORE_TEXT_HEIGHT);

			var scoreText = scoreGO.AddComponent<Text>();
			scoreText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			scoreText.fontSize = SCORE_FONT_SIZE;
			scoreText.color = Color.white;
			scoreText.text = "Score: 0";

			rhythmManager.ScoreText = scoreText;
		}
	}

	/// <summary>
	/// 시작 안내 텍스트가 없으면 생성
	/// </summary>
	/// <param name="canvas">부모 Canvas</param>
	private void CreateStartText(Canvas canvas)
	{
		// 시작 텍스트 설정
		const int START_TEXT_FONT_SIZE = 18;
		const float START_TEXT_WIDTH = 400f;
		const float START_TEXT_HEIGHT = 100f;
		var START_TEXT_POSITION = new Vector2(0, -100);

		if (GameObject.Find("StartText") == null)
		{
#if UNITY_EDITOR
			Debug.Log("📝 Creating Start Text...");
#endif
			GameObject startGO = new GameObject("StartText");
			startGO.transform.SetParent(canvas.transform);

			var startRect = startGO.AddComponent<RectTransform>();
			startRect.anchoredPosition = START_TEXT_POSITION;
			startRect.sizeDelta = new Vector2(START_TEXT_WIDTH, START_TEXT_HEIGHT);

			var startText = startGO.AddComponent<Text>();
			startText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			startText.fontSize = START_TEXT_FONT_SIZE;
			startText.color = Color.yellow;
			startText.text = "🎵 리듬게임 준비 완료!\n스페이스바로 시작하고 클릭으로 연주하세요!";
			startText.alignment = TextAnchor.MiddleCenter;
		}
	}
	#endregion
}
