using UnityEngine;
using UnityEngine.UI;

public class AutoRhythmGameSetup : MonoBehaviour
{
    void Start()
    {
        SetupRhythmGameScene();
    }
    
    void SetupRhythmGameScene()
    {
        Debug.Log("🔧 Auto-setting up rhythm game scene...");
        
        // 1. Canvas 생성
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.Log("📱 Creating Canvas...");
            GameObject canvasGO = new GameObject("Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }
        
        // 2. EventSystem 생성
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            Debug.Log("🎮 Creating EventSystem...");
            GameObject eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
        
        // 3. RhythmGameManager 생성
        RhythmGameManager rhythmManager = FindFirstObjectByType<RhythmGameManager>();
        if (rhythmManager == null)
        {
            Debug.Log("🎵 Creating RhythmGameManager...");
            GameObject managerGO = new GameObject("RhythmGameManager");
            rhythmManager = managerGO.AddComponent<RhythmGameManager>();
            
            // Canvas 연결
            rhythmManager.gameCanvas = canvas;
            
            // 중앙 타겟 생성
            GameObject centerGO = new GameObject("CenterTarget");
            centerGO.transform.SetParent(canvas.transform);
            var centerRect = centerGO.AddComponent<RectTransform>();
            centerRect.anchoredPosition = Vector2.zero;
            centerRect.sizeDelta = new Vector2(50, 50);
            
            var centerImage = centerGO.AddComponent<Image>();
            centerImage.color = new Color(1f, 1f, 1f, 0.8f);
            
            rhythmManager.centerTarget = centerGO.transform;
            
            Debug.Log("✅ RhythmGameManager setup complete!");
        }
        
        // 4. AudioManager 생성
        if (FindFirstObjectByType<AudioManager>() == null)
        {
            Debug.Log("🔊 Creating AudioManager...");
            GameObject audioGO = new GameObject("AudioManager");
            audioGO.AddComponent<AudioManager>();
        }
        
        // 5. 점수 텍스트 생성
        if (GameObject.Find("ScoreText") == null)
        {
            Debug.Log("📊 Creating Score Text...");
            GameObject scoreGO = new GameObject("ScoreText");
            scoreGO.transform.SetParent(canvas.transform);
            
            var scoreRect = scoreGO.AddComponent<RectTransform>();
            scoreRect.anchorMin = new Vector2(0, 1);
            scoreRect.anchorMax = new Vector2(0, 1);
            scoreRect.anchoredPosition = new Vector2(100, -50);
            scoreRect.sizeDelta = new Vector2(200, 50);
            
            var scoreText = scoreGO.AddComponent<Text>();
            scoreText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            scoreText.fontSize = 24;
            scoreText.color = Color.white;
            scoreText.text = "Score: 0";
            
            rhythmManager.scoreText = scoreText;
        }
        
        // 6. 시작 안내 텍스트 생성
        if (GameObject.Find("StartText") == null)
        {
            Debug.Log("📝 Creating Start Text...");
            GameObject startGO = new GameObject("StartText");
            startGO.transform.SetParent(canvas.transform);
            
            var startRect = startGO.AddComponent<RectTransform>();
            startRect.anchoredPosition = new Vector2(0, -100);
            startRect.sizeDelta = new Vector2(400, 100);
            
            var startText = startGO.AddComponent<Text>();
            startText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            startText.fontSize = 18;
            startText.color = Color.yellow;
            startText.text = "🎵 리듬게임 준비 완료!\n스페이스바로 시작하고 클릭으로 연주하세요!";
            startText.alignment = TextAnchor.MiddleCenter;
        }
        
        Debug.Log("🚀 Rhythm game scene setup complete!");
    }
}