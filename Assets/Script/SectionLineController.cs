using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class SectionLineController : MonoBehaviour
{
    private VocalSection sectionData;
    private RhythmGameManager gameManager;
    private Canvas gameCanvas;
    private Transform centerTarget;
    private Image lineImage;
    private RectTransform rectTransform;
    
    // 시각적 설정
    private float initialWidth = 1200f; // 화면 너비보다 넓게
    private float targetWidth = 50f;    // 중앙에서 축소될 크기
    private float lineHeight = 1f;      // 선의 높이 (매우 얇게)
    
    void Awake()
    {
        lineImage = GetComponent<Image>();
        if (lineImage == null)
        {
            lineImage = gameObject.AddComponent<Image>();
        }
        
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            rectTransform = gameObject.AddComponent<RectTransform>();
        }
    }
    
    public void Initialize(VocalSection section, RhythmGameManager manager, Canvas canvas, Transform center)
    {
        this.sectionData = section;
        this.gameManager = manager;
        this.gameCanvas = canvas;
        this.centerTarget = center;
        
        // 구역 타입별 색상 설정
        SetupSectionAppearance(section.type);
        
        // 가로선 초기 설정
        rectTransform.sizeDelta = new Vector2(initialWidth, lineHeight);
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        
        // 초기 위치 (100px 아래, 노트보다 뒤쪽 레이어)
        rectTransform.anchoredPosition = new Vector2(0, -130f);
        
        // 애니메이션 시간 계산
        float timeToTarget = sectionData.start_time - gameManager.currentTime;
        
        // DOTween 애니메이션: 구역 시작 시점에 축소 시작
        if (timeToTarget > 0)
        {
            // 구역 시작까지 대기 후 축소 애니메이션
            DOVirtual.DelayedCall(timeToTarget, () => {
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
        
        Debug.Log($"🎤 Section line spawned: {section.type} ({section.start_time}s - {section.end_time}s), width: {initialWidth}→{targetWidth}");
    }
    
    void SetupSectionAppearance(string sectionType)
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
        
        lineImage.color = sectionColor;
        lineImage.sprite = CreateHorizontalLineSprite();
        
        Debug.Log($"🎨 Section appearance: {sectionType} = {sectionColor}");
    }
    
    void StartSectionAnimation()
    {
        // 구역 지속 시간 계산
        float sectionDuration = sectionData.end_time - sectionData.start_time;
        
        // 1. 가로선 축소 애니메이션 (양쪽에서 중앙으로)
        rectTransform.DOSizeDelta(new Vector2(targetWidth, lineHeight), sectionDuration)
                     .SetEase(Ease.InOutQuart);
        
        // 2. 점진적 페이드아웃
        lineImage.DOFade(0.3f, sectionDuration * 0.7f)
                 .SetDelay(sectionDuration * 0.3f)
                 .SetEase(Ease.OutQuart);
        
        // 3. 구역 종료 시 완전 페이드아웃
        lineImage.DOFade(0f, 0.5f)
                 .SetDelay(sectionDuration)
                 .OnComplete(() => {
                     if (this != null && gameObject != null)
                     {
                         Destroy(gameObject);
                     }
                 });
        
        // 4. 펄스 효과 (구역 강조)
        rectTransform.DOScaleY(1.3f, 0.8f)
                     .SetLoops(-1, LoopType.Yoyo)
                     .SetEase(Ease.InOutSine);
        
        Debug.Log($"🎬 Section animation started: {sectionData.type}, duration: {sectionDuration}s");
    }
    
    static Sprite CreateHorizontalLineSprite()
    {
        // 가로선 스프라이트 생성
        const int WIDTH = 64;
        const int HEIGHT = 1;
        
        var texture = new Texture2D(WIDTH, HEIGHT);
        var colors = new Color[WIDTH * HEIGHT];
        
        // 전체를 흰색으로 채움
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Color.white;
        }
        
        texture.SetPixels(colors);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, WIDTH, HEIGHT), new Vector2(0.5f, 0.5f));
    }
    
    public bool IsExpired()
    {
        return gameManager.currentTime > sectionData.end_time + 1f; // 1초 여유
    }
    
    public float GetStartTime()
    {
        return sectionData.start_time;
    }
    
    public float GetEndTime()
    {
        return sectionData.end_time;
    }
    
    public string GetSectionType()
    {
        return sectionData.type;
    }
}