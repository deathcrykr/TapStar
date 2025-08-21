using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.UI;
using FMODUnity;
using DG.Tweening;
using UnityEngine.InputSystem;

[System.Serializable]
public class Note
{
    public float time_seconds;
    public int lane;
    public string type;
    public float intensity;
    public int level = 1; // 1=Easy, 2=Medium, 3=Hard
}

[System.Serializable]
public class VocalSection
{
    public float start_time;
    public float end_time;
    public string type; // vocal, sub_vocal, rap, ttaechang
}

[System.Serializable]
public class NoteData
{
    public List<Note> notes = new List<Note>();
}

[System.Serializable]
public class FullNoteDataWithSections
{
    public List<Note> notes = new List<Note>();
    public List<VocalSection> vocal_sections = new List<VocalSection>();
}

public class RhythmGameManager : MonoBehaviour
{
    [Header("Game Settings")]
    public float noteSpeed = 2f;
    public float hitWindow = 0.05f; // 50ms - 전문 리듬게임 수준의 정확도
    
    [Header("Audio Calibration")]
    public float audioLatencyOffset = 0.020f; // 정밀한 리듬 게임용 오디오 지연 보정 (20ms)
    
    [Header("Difficulty")]
    public int difficultyLevel = 3; // 1=Easy, 2=Medium, 3=Hard (includes all lower levels)
    
    [Header("Debug")]
    public bool showSectionLines = false; // 디버그 모드: 보컬 섹션 라인 표시
    
    [Header("UI Elements")]
    public Transform centerTarget;
    public GameObject notePrefab;
    public GameObject sectionLinePrefab;
    public Canvas gameCanvas;
    
    [Header("Audio")]
    public EventReference musicEventPath;
    public EventReference hitSoundEventPath;
    public string musicFileName = "disco-train";
    
    private FMOD.Studio.EventInstance musicInstance;
    
    private NoteData currentNoteData;
    private List<VocalSection> currentVocalSections = new List<VocalSection>();
    private List<GameObject> activeNotes = new List<GameObject>();
    private List<GameObject> activeSectionLines = new List<GameObject>();
    
    public float currentTime = 0f;
    private int nextNoteIndex = 0;
    private int nextSectionIndex = 0;
    private bool isPlaying = false;
    
    
    [Header("Scoring")]
    public int score = 0;
    public Text scoreText;
    
    public static RhythmGameManager Instance { get; private set; }
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        Debug.Log("🎮 RhythmGameManager starting...");
        
        // FMOD 필수 체크
        if (!CheckFMODAvailability())
        {
            Debug.LogError("❌ FMOD is not available! RhythmGameManager disabled.");
            enabled = false;
            return;
        }
        
        // 필수 컴포넌트들 미리 설정
        SetupRequiredComponents();
        
        LoadMusicAndNotes();
        
        // 자동 게임 시작 (테스트용)
        if (currentNoteData != null && currentNoteData.notes.Count > 0)
        {
            Debug.Log("🚀 Auto-starting game for testing...");
            Invoke(nameof(StartGame), 1f); // 1초 후 자동 시작
        }
        else
        {
            Debug.LogError("❌ Cannot start game - no note data available!");
            if (currentNoteData == null) Debug.LogError("   - currentNoteData is null");
            else Debug.LogError($"   - currentNoteData has {currentNoteData.notes.Count} notes");
        }
    }
    
    bool CheckFMODAvailability()
    {
        try
        {
            // FMOD Studio 시스템이 초기화되었는지 확인
            if (!RuntimeManager.HasBankLoaded("Master"))
            {
                Debug.LogError("❌ FMOD Master Bank not loaded!");
                Debug.LogError("💡 Make sure FMOD is properly configured and banks are built.");
                return false;
            }
            
            if (musicEventPath.IsNull)
            {
                Debug.LogError("❌ FMOD Event Path is not set in Inspector!");
                Debug.LogError("💡 Please assign a valid FMOD event to 'Music Event Path'.");
                return false;
            }
            
            Debug.Log("✅ FMOD is available and ready.");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ FMOD availability check failed: {e.Message}");
            return false;
        }
    }
    
    void SetupRequiredComponents()
    {
        // NotePrefab 미리 생성
        if (notePrefab == null)
        {
            Debug.Log("🔧 Creating default note prefab...");
            CreateDefaultNotePrefab();
        }
        
        // Canvas 미리 찾기
        if (gameCanvas == null)
        {
            gameCanvas = FindFirstObjectByType<Canvas>();
            if (gameCanvas == null)
            {
                Debug.LogError("❌ No Canvas found in scene!");
            }
            else
            {
                Debug.Log("✅ Canvas found and assigned");
            }
        }
        
        // CenterTarget 미리 설정
        if (centerTarget == null)
        {
            var go = new GameObject("CenterTarget");
            go.transform.SetParent(gameCanvas.transform);
            centerTarget = go.transform;
            var rectTransform = go.AddComponent<RectTransform>();
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = new Vector2(100, 100); // 노트 타겟 크기와 맞춤
            
            var image = go.AddComponent<Image>();
            image.color = new Color(1f, 0.5f, 0f, 0.9f); // 주황색으로 명확한 타이밍 표시
            image.sprite = CreateDefaultCircleSprite();
            
            // 중앙 타겟 펄스 효과로 타이밍 강조
            image.transform.DOScale(1.1f, 0.8f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
            
            Debug.Log("✅ CenterTarget created and positioned");
        }
    }
    
    void Update()
    {
        if (isPlaying)
        {
            UpdateGameTime();
            SpawnNotes();
            if (showSectionLines) // 디버그 모드에서만 섹션 라인 표시
            {
                SpawnSectionLines();
            }
            UpdateNotes();
            if (showSectionLines) // 디버그 모드에서만 섹션 라인 업데이트
            {
                UpdateSectionLines();
            }
            CheckInput();
        }
        
        // 새로운 Input System 사용
        if (Keyboard.current?.spaceKey.wasPressedThisFrame == true && !isPlaying)
        {
            StartGame();
        }
    }
    
    void LoadMusicAndNotes()
    {
        // Resources 폴더에서 JSON 파일 로드
        string resourcePath = "Music/" + musicFileName;
        Debug.Log($"🔍 Trying to load: {resourcePath}");
        
        TextAsset jsonAsset = Resources.Load<TextAsset>(resourcePath);
        
        if (jsonAsset != null)
        {
            Debug.Log($"✅ JSON file loaded successfully: {jsonAsset.name}");
            try
            {
                var fullData = JsonUtility.FromJson<FullNoteDataWithSections>(jsonAsset.text);
                
                // 난이도에 따른 노트 필터링
                var filteredNotes = FilterNotesByDifficulty(fullData.notes, difficultyLevel);
                currentNoteData = new NoteData { notes = filteredNotes };
                
                // 보컬 구역 정보 로드
                currentVocalSections = fullData.vocal_sections ?? new List<VocalSection>();
                
                Debug.Log($"✅ Loaded {fullData.notes.Count} total notes, filtered to {currentNoteData.notes.Count} for difficulty level {difficultyLevel}");
                Debug.Log($"✅ Loaded {currentVocalSections.Count} vocal sections");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Failed to parse JSON: {e.Message}");
            }
        }
        else
        {
            Debug.LogError($"❌ Note file not found: Resources/{resourcePath}");
            Debug.LogError($"❌ Make sure disco-train.json exists in Assets/Resources/Music/");
            
            // 모든 Resources/Music 파일 목록 출력
            TextAsset[] allAssets = Resources.LoadAll<TextAsset>("Music");
            Debug.Log($"📁 Found {allAssets.Length} files in Resources/Music:");
            foreach (var asset in allAssets)
            {
                Debug.Log($"   - {asset.name}");
            }
        }
        
        // FMOD 이벤트 경로 필수 확인
        if (musicEventPath.IsNull)
        {
            Debug.LogError("❌ FMOD Event Path is required! Game cannot start without FMOD.");
            Debug.LogError("💡 Please set 'Music Event Path' in Inspector and configure FMOD properly.");
            enabled = false; // 컴포넌트 비활성화
            return;
        }
    }
    
    
    private List<Note> FilterNotesByDifficulty(List<Note> allNotes, int maxLevel)
    {
        if (allNotes == null) return new List<Note>();
        
        // 선택된 난이도 레벨 이하의 노트들만 포함
        // 예: Hard(3) = 레벨 1, 2, 3 모두 포함
        // 예: Medium(2) = 레벨 1, 2만 포함 
        // 예: Easy(1) = 레벨 1만 포함
        var filteredNotes = new List<Note>();
        
        foreach (var note in allNotes)
        {
            // 레벨이 설정되지 않은 노트는 레벨 1로 간주
            int noteLevel = note.level > 0 ? note.level : 1;
            
            if (noteLevel <= maxLevel)
            {
                filteredNotes.Add(note);
            }
        }
        
        Debug.Log($"🎯 Difficulty filtering: {allNotes.Count} → {filteredNotes.Count} notes (max level: {maxLevel})");
        return filteredNotes;
    }
    
    public void StartGame()
    {
        if (currentNoteData == null)
        {
            Debug.LogError("❌ No note data loaded! Check Resources/Music folder.");
            return;
        }
        
        // FMOD 음악 재생 (필수)
        if (musicEventPath.IsNull)
        {
            Debug.LogError("❌ FMOD Event Path is required! Cannot start game.");
            return;
        }
        
        try
        {
            musicInstance = RuntimeManager.CreateInstance(musicEventPath);
            musicInstance.start();
            Debug.Log($"🎵 Started FMOD music: {musicEventPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Failed to start FMOD music: {e.Message}");
            Debug.LogError("❌ Game cannot start without FMOD working properly.");
            return;
        }
        
        isPlaying = true;
        currentTime = 0f;
        nextNoteIndex = 0;
        nextSectionIndex = 0;
        score = 0;
        
        Debug.Log($"✅ Rhythm game started! Notes: {currentNoteData.notes.Count}, Vocal sections: {currentVocalSections.Count}");
    }
    
    public void StopGame()
    {
        isPlaying = false;
        
        // FMOD 음악 정지
        if (musicInstance.isValid())
        {
            musicInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            musicInstance.release();
            Debug.Log("Stopped FMOD music");
        }
        
        ClearAllNotes();
        ClearAllSectionLines();
        
        // UI에 게임 종료 알림
        MobileRhythmUI ui = FindFirstObjectByType<MobileRhythmUI>();
        if (ui != null)
        {
            ui.ShowGameOver(score);
        }
        
        Debug.Log($"Game ended! Final Score: {score}");
    }
    
    void ResetGame()
    {
        score = 0;
        currentTime = 0f;
        nextNoteIndex = 0;
        nextSectionIndex = 0;
        isPlaying = false;
        
        ClearAllNotes();
        ClearAllSectionLines();
    }
    
    void UpdateGameTime()
    {
        // FMOD 타임라인만 사용 (필수)
        if (!musicInstance.isValid())
        {
            Debug.LogError("❌ FMOD music instance is invalid! Stopping game.");
            StopGame();
            return;
        }
        
        musicInstance.getPlaybackState(out FMOD.Studio.PLAYBACK_STATE playbackState);
        
        if (playbackState == FMOD.Studio.PLAYBACK_STATE.PLAYING)
        {
            // FMOD의 실제 재생 위치를 밀리초로 가져와서 초로 변환
            musicInstance.getTimelinePosition(out int position);
            float rawTime = position / 1000f; // 밀리초를 초로 변환
            
            // 오디오 지연 보정 적용 (글에서 제시한 캘리브레이션 원칙)
            currentTime = rawTime + audioLatencyOffset;
        }
        else if (playbackState == FMOD.Studio.PLAYBACK_STATE.STOPPED && currentTime > 2f)
        {
            Debug.Log("🎵 FMOD music finished, stopping game.");
            StopGame();
        }
        else if (playbackState == FMOD.Studio.PLAYBACK_STATE.STOPPING)
        {
            // 음악이 끝나가고 있음
            musicInstance.getTimelinePosition(out int position);
            float rawTime = position / 1000f;
            currentTime = rawTime + audioLatencyOffset;
        }
    }
    
    void SpawnNotes()
    {
        if (currentNoteData == null || currentNoteData.notes == null)
        {
            Debug.LogError("❌ Cannot spawn notes - no note data!");
            return;
        }
        
        float spawnTime = 2f; // 2초 전에 스폰 (더 정확한 타이밍)
        
        while (nextNoteIndex < currentNoteData.notes.Count)
        {
            Note note = currentNoteData.notes[nextNoteIndex];
            
            if (note.time_seconds - currentTime <= spawnTime)
            {
                Debug.Log($"🎯 Attempting to spawn note {nextNoteIndex} at time {note.time_seconds}s");
                SpawnNote(note);
                nextNoteIndex++;
            }
            else
            {
                break;
            }
        }
    }
    
    void SpawnNote(Note note)
    {
        // 필수 컴포넌트 확인 (이미 Start()에서 설정되었어야 함)
        if (notePrefab == null || gameCanvas == null || centerTarget == null)
        {
            Debug.LogError("❌ Required components not set up! Skipping note spawn.");
            return;
        }
        
        GameObject noteObj = Instantiate(notePrefab, gameCanvas.transform);
        
        // 세로 라인 노트 설정
        var controller = noteObj.GetComponent<LineNoteController>() ?? noteObj.AddComponent<LineNoteController>();
        controller.Initialize(note, this, gameCanvas, centerTarget);
        
        activeNotes.Add(noteObj);
        
        Debug.Log($"📝 Spawned note at time: {note.time_seconds}s, intensity: {note.intensity}");
    }
    
    void SpawnSectionLines()
    {
        if (currentVocalSections == null || currentVocalSections.Count == 0)
        {
            return; // 보컬 구역이 없으면 스킵
        }
        
        float spawnTime = 3f; // 3초 전에 스폰 (노트보다 일찍)
        
        while (nextSectionIndex < currentVocalSections.Count)
        {
            VocalSection section = currentVocalSections[nextSectionIndex];
            
            if (section.start_time - currentTime <= spawnTime)
            {
                Debug.Log($"🎤 Attempting to spawn section line: {section.type} at {section.start_time}s");
                SpawnSectionLine(section);
                nextSectionIndex++;
            }
            else
            {
                break;
            }
        }
    }
    
    void SpawnSectionLine(VocalSection section)
    {
        // 필수 컴포넌트 확인
        if (gameCanvas == null || centerTarget == null)
        {
            Debug.LogError("❌ Required components not set up! Skipping section line spawn.");
            return;
        }
        
        // 구역선 프리팹이 없으면 기본 생성
        if (sectionLinePrefab == null)
        {
            Debug.Log("🔧 Creating default section line prefab...");
            CreateDefaultSectionLinePrefab();
        }
        
        GameObject sectionLineObj = Instantiate(sectionLinePrefab, gameCanvas.transform);
        
        // 구역선을 노트보다 뒤쪽 레이어에 배치 (Z 인덱스 조정)
        sectionLineObj.transform.SetSiblingIndex(0);
        
        // 구역선 컨트롤러 설정
        var controller = sectionLineObj.GetComponent<SectionLineController>() ?? sectionLineObj.AddComponent<SectionLineController>();
        controller.Initialize(section, this, gameCanvas, centerTarget);
        
        activeSectionLines.Add(sectionLineObj);
        
        Debug.Log($"🎨 Spawned section line: {section.type} ({section.start_time}s - {section.end_time}s)");
    }
    
    void CreateDefaultSectionLinePrefab()
    {
        // 기본 구역선 프리팹 생성
        sectionLinePrefab = new GameObject("DefaultSectionLine");
        var image = sectionLinePrefab.AddComponent<Image>();
        var rectTransform = sectionLinePrefab.GetComponent<RectTransform>();
        
        // 기본 색상 (반투명 흰색)
        image.color = new Color(1f, 1f, 1f, 0.5f);
        image.sprite = CreateDefaultHorizontalLineSprite();
        
        // 기본 크기 설정 (얇게)
        rectTransform.sizeDelta = new Vector2(1200, 1);
        
        Debug.Log("✅ Created default section line prefab");
    }
    
    static Sprite CreateDefaultHorizontalLineSprite()
    {
        // 가로선 스프라이트 생성 (매우 얇게)
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
    
    void UpdateSectionLines()
    {
        // 역순으로 순회하여 안전한 제거
        for (int i = activeSectionLines.Count - 1; i >= 0; i--)
        {
            var sectionLineObj = activeSectionLines[i];
            if (sectionLineObj == null)
            {
                activeSectionLines.RemoveAt(i);
                continue;
            }
            
            var controller = sectionLineObj.GetComponent<SectionLineController>();
            if (controller?.IsExpired() == true)
            {
                activeSectionLines.RemoveAt(i);
                Destroy(sectionLineObj);
            }
        }
    }
    
    void CreateDefaultNotePrefab()
    {
        // 세로 라인 노트 프리팹 생성
        notePrefab = new GameObject("DefaultLineNote");
        var image = notePrefab.AddComponent<Image>();
        var rectTransform = notePrefab.GetComponent<RectTransform>();
        
        // 세로 라인 스프라이트 생성
        image.sprite = CreateDefaultLineSprite();
        image.color = Color.cyan;
        
        // 세로 라인 크기 설정 (가로 좁고 세로 적당히)
        rectTransform.sizeDelta = new Vector2(8, 200);
        
        Debug.Log("✅ Created default line note prefab");
    }
    
    static Sprite CreateDefaultLineSprite()
    {
        // 세로 라인 스프라이트 생성 (8x64)
        const int WIDTH = 8;
        const int HEIGHT = 64;
        
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
    
    static Sprite CreateDefaultCircleSprite()
    {
        var texture = new Texture2D(64, 64);
        var center = new Vector2(32, 32);
        var colors = new Color[64 * 64];
        
        for (int i = 0; i < colors.Length; i++)
        {
            int x = i % 64;
            int y = i / 64;
            float distance = Vector2.Distance(new Vector2(x, y), center);
            
            colors[i] = (distance <= 30 && distance >= 25) ? Color.white : Color.clear;
        }
        
        texture.SetPixels(colors);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
    }
    
    void UpdateNotes()
    {
        // 역순으로 순회하여 안전한 제거
        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            var noteObj = activeNotes[i];
            if (noteObj == null)
            {
                activeNotes.RemoveAt(i);
                continue;
            }
            
            var controller = noteObj.GetComponent<LineNoteController>();
            if (controller?.IsExpired() == true)
            {
                activeNotes.RemoveAt(i);
                Destroy(noteObj);
            }
        }
    }
    
    void CheckInput()
    {
        bool inputPressed = false;
        float inputTime = currentTime; // 현재 오디오 시간을 입력 타이밍으로 사용
        
        // 새로운 Input System 사용
        if (Mouse.current?.leftButton.wasPressedThisFrame == true)
            inputPressed = true;
            
        if (Keyboard.current?.spaceKey.wasPressedThisFrame == true)
            inputPressed = true;
        
        // 터치 입력 (새로운 Input System) - 글에서 제시한 터치 타임스탬프 원칙 적용
        if (Touchscreen.current != null)
        {
            foreach (var touch in Touchscreen.current.touches)
            {
                if (touch.press.wasPressedThisFrame)
                {
                    inputPressed = true;
                    // 더 정확한 타이밍을 위해 터치 시작 시간 사용 가능 (고급 구현)
                    break;
                }
            }
        }
        
        if (inputPressed)
        {
            CheckNoteHit(inputTime);
        }
    }
    
    void CheckNoteHit(float inputTime)
    {
        LineNoteController closestNote = null;
        float closestTimeDiff = float.MaxValue;
        
        foreach (GameObject noteObj in activeNotes)
        {
            LineNoteController controller = noteObj.GetComponent<LineNoteController>();
            if (controller != null)
            {
                // 글에서 제시한 원칙: 정확한 입력 시간 기준으로 판정
                float timeDiff = controller.GetTimeDifference(inputTime);
                
                if (Mathf.Abs(timeDiff) < hitWindow && Mathf.Abs(timeDiff) < closestTimeDiff)
                {
                    closestNote = controller;
                    closestTimeDiff = Mathf.Abs(timeDiff);
                }
            }
        }
        
        if (closestNote != null)
        {
            // 정확도 등급 계산 (글에서 제시한 세밀한 판정)
            string accuracy = GetAccuracyGrade(closestTimeDiff);
            OnNoteHit(closestNote, accuracy, closestTimeDiff);
            activeNotes.Remove(closestNote.gameObject);
            Destroy(closestNote.gameObject);
        }
    }
    
    string GetAccuracyGrade(float timeDiff)
    {
        // 글에서 제시한 원칙: 세밀한 판정 등급
        if (timeDiff <= 0.015f) return "PERFECT"; // 15ms 이하
        if (timeDiff <= 0.030f) return "GREAT";   // 30ms 이하  
        if (timeDiff <= 0.050f) return "GOOD";    // 50ms 이하
        return "OK";
    }
    
    void OnNoteHit(LineNoteController note, string accuracy, float timeDiff)
    {
        // 정확도에 따른 점수 (글에서 제시한 세밀한 피드백)
        int baseScore = accuracy switch
        {
            "PERFECT" => 300,
            "GREAT" => 200,
            "GOOD" => 100,
            "OK" => 50,
            _ => 0
        };
        
        score += baseScore;
        
        // 디버그: 정확한 타이밍 분석
        Debug.Log($"🎯 Hit! Accuracy: {accuracy} ({timeDiff*1000:F1}ms), Score: +{baseScore}");
        
        // DOTween 히트 효과 - 중앙 타겟 펀치 애니메이션 및 0.1초 발광 효과
        if (centerTarget != null)
        {
            centerTarget.DOKill();
            centerTarget.DOPunchScale(Vector3.one * 0.2f, 0.3f, 5, 0.5f);
            
            // 0.1초 발광 효과 추가 - 진짜 발광 효과
            var targetImage = centerTarget.GetComponent<Image>();
            if (targetImage != null)
            {
                Color originalColor = targetImage.color;
                // 발광 효과: 밝기를 대폭 증가 (HDR 값 사용)
                Color glowColor = new Color(2f, 2f, 2f, originalColor.a); // RGB 값 2배로 증가하여 발광
                
                // 즉시 발광색으로 변경하고 0.1초 후 원래 색상으로 복귀
                targetImage.DOKill();
                targetImage.color = glowColor;
                targetImage.DOColor(originalColor, 0.1f);
            }
        }
        
        // 노트 히트 효과 - 페이드아웃
        note.transform.DOScale(0f, 0.2f).SetEase(Ease.InBack);
        note.GetComponent<Image>().DOFade(0f, 0.2f);
        
        // FMOD 히트 사운드 재생 (AudioManager를 통해)
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayClickSound();
        }
        else if (!hitSoundEventPath.IsNull)
        {
            RuntimeManager.PlayOneShot(hitSoundEventPath);
        }
        
        // UI 업데이트
        MobileRhythmUI ui = FindFirstObjectByType<MobileRhythmUI>();
        if (ui != null)
        {
            ui.UpdateScore(score);
        }
        
        if (scoreText != null)
        {
            scoreText.text = "Score: " + score;
            scoreText.transform.DOKill();
            scoreText.transform.DOPunchScale(Vector3.one * 0.1f, 0.2f, 3, 0.3f);
        }
        
        Debug.Log($"Hit! Score: {score}");
    }
    
    void ClearAllNotes()
    {
        foreach (GameObject noteObj in activeNotes)
        {
            if (noteObj != null)
                Destroy(noteObj);
        }
        activeNotes.Clear();
    }
    
    void ClearAllSectionLines()
    {
        foreach (GameObject sectionLineObj in activeSectionLines)
        {
            if (sectionLineObj != null)
                Destroy(sectionLineObj);
        }
        activeSectionLines.Clear();
    }
    
    public int GetCurrentScore() => score;
    
    public void SetDifficultyLevel(int level)
    {
        if (level < 1 || level > 3)
        {
            Debug.LogWarning($"❌ Invalid difficulty level: {level}. Must be 1-3.");
            return;
        }
        
        difficultyLevel = level;
        Debug.Log($"🎯 Difficulty level set to: {level} ({GetDifficultyName(level)})");
        
        // 게임이 진행 중이 아니면 노트 데이터 다시 로드
        if (!isPlaying)
        {
            LoadMusicAndNotes();
        }
    }
    
    public string GetDifficultyName(int level)
    {
        switch (level)
        {
            case 1: return "Easy";
            case 2: return "Medium";  
            case 3: return "Hard";
            default: return "Unknown";
        }
    }
    
    public int GetDifficultyLevel() => difficultyLevel;
    
    // 글에서 제시한 캘리브레이션 시스템
    public void SetAudioLatencyOffset(float offsetSeconds)
    {
        audioLatencyOffset = offsetSeconds;
        Debug.Log($"🎚️ Audio latency offset set to: {offsetSeconds*1000:F1}ms");
    }
    
    public float GetAudioLatencyOffset() => audioLatencyOffset;
    
    // 캘리브레이션을 위한 테스트 함수
    public void StartCalibrationMode()
    {
        Debug.Log("🎚️ Starting calibration mode - adjust offset until audio feels in sync");
        // 캘리브레이션 UI나 테스트 패턴 시작 가능
    }
}

public class LineNoteController : MonoBehaviour
{
    private Note noteData;
    private RhythmGameManager gameManager;
    private Canvas gameCanvas;
    private Transform centerTarget;
    private float duration = 2f; // 2초 동안 이동
    private float startTime;
    private Image lineImage;
    private RectTransform rectTransform;
    private bool hasReachedCenter = false; // 중앙 도달 플래그
    
    // 시작/끝 위치
    private float startX;
    private float targetX;
    
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
    
    public void Initialize(Note note, RhythmGameManager manager, Canvas canvas, Transform center)
    {
        this.noteData = note;
        this.gameManager = manager;
        this.gameCanvas = canvas;
        this.centerTarget = center;
        
        // 세로 라인 설정
        lineImage.sprite = CreateLineSprite();
        
        // 레벨별 외관 설정
        SetupNoteAppearanceByLevel(note.level);
        
        // 레벨별 초기 크기 설정
        float initialHeight = noteData.level == 2 ? 10f : 200f; // Level 2는 10px에서 시작
        rectTransform.sizeDelta = new Vector2(8, initialHeight);
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        
        // 시작 위치 결정 (랜덤하게 좌측 또는 우측)
        float canvasWidth = canvas.GetComponent<RectTransform>().rect.width;
        bool fromLeft = Random.Range(0, 2) == 0;
        
        startX = fromLeft ? -canvasWidth * 0.6f : canvasWidth * 0.6f;
        targetX = 0f; // 화면 중앙
        
        // 시작 위치 설정
        rectTransform.anchoredPosition = new Vector2(startX, 0);
        
        // 애니메이션 시간 계산 (정확한 타이밍 보장)
        float timeToTarget = noteData.time_seconds - gameManager.currentTime;
        
        // 타이밍 안전성 검증
        if (timeToTarget <= 0)
        {
            Debug.LogWarning($"⚠️ Note spawn timing issue: timeToTarget={timeToTarget}, noteTime={noteData.time_seconds}, currentTime={gameManager.currentTime}");
            timeToTarget = 0.1f; // 최소 시간 보장
        }
        
        // DOTween 애니메이션들 - Linear easing으로 정확한 타이밍 보장
        // 1. 중앙으로 이동은 아래에서 OnComplete와 함께 처리
        
        // 2. 크기 변화 - 레벨별 애니메이션 (Linear로 정확한 타이밍)
        if (noteData.level == 1) // Level 1: 200px → 100px
        {
            rectTransform.DOSizeDelta(new Vector2(8, 100), timeToTarget).SetEase(Ease.Linear);
        }
        else if (noteData.level == 2) // Level 2: 10px → 100px
        {
            rectTransform.DOSizeDelta(new Vector2(8, 100), timeToTarget).SetEase(Ease.Linear);
        }
        // Level 3은 보이지 않으므로 크기 애니메이션 없음
        
        // 3. 중앙 도달 시점에 흰색으로 변경 후 0.2초 대기 후 사라짐
        if (noteData.level != 3)
        {
            // 이동 애니메이션이 완료되면 (중앙 도달 시) 0.1초간 흰색 유지 후 페이드아웃
            rectTransform.DOAnchorPosX(targetX, timeToTarget).SetEase(Ease.Linear)
                .OnComplete(() => {
                    // Update() 간섭 방지를 위해 플래그 설정
                    hasReachedCenter = true;
                    
                    // 중앙 도달 순간에 흰색으로 변경
                    lineImage.DOKill(); // 기존 애니메이션 중단
                    lineImage.color = Color.white;
                    
                    // 0.1초 대기 후 투명하게 사라짐 (더 자연스러운 페이드아웃)
                    DOVirtual.DelayedCall(0.1f, () => {
                        if (this != null && lineImage != null)
                        {
                            lineImage.DOFade(0f, 0.5f).SetEase(Ease.OutCubic)
                                .OnComplete(() => {
                                    if (this != null && gameObject != null)
                                    {
                                        Destroy(gameObject);
                                    }
                                });
                        }
                    });
                });
        }
        
        // 로그 메시지를 레벨별로 구체화
        string sizeInfo = noteData.level == 1 ? "200px→100px" : 
                         noteData.level == 2 ? "10px→100px" : "invisible";
        Debug.Log($"📏 Line note spawned: Level {noteData.level}, from {startX} to {targetX}, size {sizeInfo}, duration: {timeToTarget}s");
        Debug.Log($"🎯 NOTE TIMING: Will reach center at {noteData.time_seconds}s (current: {gameManager.currentTime}s, offset: {gameManager.audioLatencyOffset}s)");
        Debug.Log($"⏱️  ANIMATION: {timeToTarget}s to complete, spawn-to-hit delay: {timeToTarget}");
    }
    
    void SetupNoteAppearanceByLevel(int level)
    {
        switch (level)
        {
            case 1: // Easy - 기존 노트 (100% 불투명, cyan)
                lineImage.color = Color.cyan;
                gameObject.SetActive(true);
                Debug.Log($"🟦 Level 1 (Easy) note: Full opacity, cyan color");
                break;
                
            case 2: // Medium - 반투명 노트 (20% 불투명, cyan, 10px→100px 애니메이션)
                lineImage.color = new Color(0f, 1f, 1f, 0.2f); // cyan with 20% alpha
                gameObject.SetActive(true);
                Debug.Log($"🟦 Level 2 (Medium) note: 20% opacity, cyan color, 10px→100px animation");
                break;
                
            case 3: // Hard - 보이지 않는 노트 (판정만 존재)
                lineImage.color = new Color(0f, 1f, 1f, 0f); // 완전 투명
                gameObject.SetActive(true); // 판정을 위해 GameObject는 활성화 유지
                Debug.Log($"👻 Level 3 (Hard) note: Invisible (judgment only)");
                break;
                
            default:
                lineImage.color = Color.cyan; // 기본값
                gameObject.SetActive(true);
                Debug.LogWarning($"⚠️ Unknown note level: {level}, using default appearance");
                break;
        }
    }
    
    void Update()
    {
        // 중앙에 도달했으면 Update에서 색상 조정하지 않음
        if (hasReachedCenter)
        {
            return;
        }
        
        // Level 3 (Hard) 노트는 완전히 보이지 않음
        if (noteData.level == 3)
        {
            return; // 알파값 조정하지 않음
        }
        
        // 타이밍에 따른 알파값 조정 (Level 1, 2만)
        float targetTime = noteData.time_seconds - gameManager.currentTime;
        float absTargetTime = Mathf.Abs(targetTime);
        
        // 레벨별 기본 알파값
        float baseAlpha = noteData.level == 1 ? 1f : 0.2f; // Level 1: 100%, Level 2: 20%
        
        if (absTargetTime < 0.3f)
        {
            // 타이밍이 가까워지면 기본 알파값으로
            var color = lineImage.color;
            if (Mathf.Abs(color.a - baseAlpha) > 0.01f)
            {
                color.a = baseAlpha;
                lineImage.color = color;
            }
        }
        else
        {
            // 거리에 따른 페이드 (기본 알파값 기준)
            float alpha = Mathf.Clamp01((1f - absTargetTime / duration) * baseAlpha);
            var color = lineImage.color;
            if (Mathf.Abs(color.a - alpha) > 0.01f)
            {
                color.a = alpha;
                lineImage.color = color;
            }
        }
    }
    
    public float GetTimeDifference(float currentTime)
    {
        return noteData.time_seconds - currentTime;
    }
    
    public bool IsExpired()
    {
        return GetTimeDifference(gameManager.currentTime) < -1f;
    }
    
    private static Sprite _lineSprite; // 정적 캐싱으로 성능 향상
    
    Sprite CreateLineSprite()
    {
        // 이미 생성된 스프라이트가 있으면 재사용
        if (_lineSprite != null) return _lineSprite;
        
        const int WIDTH = 8;
        const int HEIGHT = 64;
        
        var texture = new Texture2D(WIDTH, HEIGHT);
        var colors = new Color[WIDTH * HEIGHT];
        
        // 전체를 흰색으로 채움
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Color.white;
        }
        
        texture.SetPixels(colors);
        texture.Apply();
        
        _lineSprite = Sprite.Create(texture, new Rect(0, 0, WIDTH, HEIGHT), new Vector2(0.5f, 0.5f));
        return _lineSprite;
    }
}