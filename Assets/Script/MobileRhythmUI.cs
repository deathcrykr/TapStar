using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class MobileRhythmUI : MonoBehaviour
{
    [Header("UI References")]
    public Button startButton;
    public Text scoreText;
    public Text instructionText;
    public GameObject centerTarget;
    public Canvas gameCanvas;
    
    [Header("Mobile Settings")]
    public float targetFrameRate = 60f;
    
    void Start()
    {
        SetupMobileOptimizations();
        SetupUI();
    }
    
    void SetupMobileOptimizations()
    {
        // 모바일 최적화 설정
        Application.targetFrameRate = (int)targetFrameRate;
        
        // 화면 해상도 최적화
        if (Application.isMobilePlatform)
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            
            // UI 스케일링
            CanvasScaler scaler = gameCanvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameCanvas.gameObject.AddComponent<CanvasScaler>();
            }
            
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }
    }
    
    void SetupUI()
    {
        if (startButton != null)
        {
            startButton.onClick.AddListener(() => {
                RhythmGameManager.Instance?.StartGame();
                startButton.gameObject.SetActive(false);
                if (instructionText != null)
                    instructionText.gameObject.SetActive(false);
            });
        }
        
        if (instructionText != null)
        {
            if (Application.isMobilePlatform)
            {
                instructionText.text = "화면을 터치하여 리듬에 맞춰 연주하세요!";
            }
            else
            {
                instructionText.text = "스페이스바나 마우스 클릭으로 연주하세요!";
            }
        }
        
        // 중앙 타겟 설정
        if (centerTarget != null)
        {
            Image targetImage = centerTarget.GetComponent<Image>();
            if (targetImage == null)
            {
                targetImage = centerTarget.AddComponent<Image>();
            }
            
            targetImage.sprite = CreateTargetSprite();
            targetImage.color = new Color(1f, 1f, 1f, 0.8f);
        }
    }
    
    Sprite CreateTargetSprite()
    {
        int size = 64;
        Texture2D texture = new Texture2D(size, size);
        
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 2f;
        
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                
                if (distance <= radius && distance >= radius - 4f)
                {
                    texture.SetPixel(x, y, Color.white);
                }
                else if (distance <= 4f)
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
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
    
    public void UpdateScore(int score)
    {
        if (scoreText != null)
        {
            scoreText.text = "점수: " + score.ToString("N0");
            
            // DOTween 점수 업데이트 효과
            scoreText.transform.DOKill();
            scoreText.transform.DOPunchScale(Vector3.one * 0.15f, 0.3f, 5, 0.3f);
            
            // 색상 효과
            Color originalColor = scoreText.color;
            scoreText.DOColor(Color.yellow, 0.1f).OnComplete(() => {
                scoreText.DOColor(originalColor, 0.2f);
            });
        }
    }
    
    public void ShowGameOver(int finalScore)
    {
        if (startButton != null)
        {
            startButton.gameObject.SetActive(true);
            startButton.GetComponentInChildren<Text>().text = "다시 시작";
            
            // DOTween 버튼 등장 효과
            startButton.transform.localScale = Vector3.zero;
            startButton.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBounce);
        }
        
        if (instructionText != null)
        {
            instructionText.gameObject.SetActive(true);
            instructionText.text = $"게임 종료! 최종 점수: {finalScore:N0}\n다시 시작하려면 버튼을 터치하세요";
            
            // DOTween 텍스트 페이드인 효과
            instructionText.color = new Color(instructionText.color.r, instructionText.color.g, instructionText.color.b, 0f);
            instructionText.DOFade(1f, 0.8f).SetDelay(0.3f);
        }
    }
}