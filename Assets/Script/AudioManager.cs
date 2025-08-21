using UnityEngine;
using System.Collections.Generic;
using FMODUnity;

[System.Serializable]
public class FMODSound
{
    public string name;
    public EventReference eventReference;
    [Range(0f, 1f)]
    public float volume = 1f;
    public bool is3D = false;
    
    [HideInInspector]
    public FMOD.Studio.EventInstance instance;
}

public class AudioManager : MonoBehaviour
{
    [Header("FMOD Sound Effects")]
    public FMODSound[] sounds;
    
    [Header("Music")]
    public EventReference backgroundMusicEvent;
    public bool playMusicOnStart = true;
    
    private FMOD.Studio.EventInstance musicInstance;
    
    [Header("Settings")]
    [Range(0f, 1f)]
    public float masterVolume = 1f;
    [Range(0f, 1f)]
    public float musicVolume = 1f;
    [Range(0f, 1f)]
    public float soundVolume = 1f;
    
    public bool musicEnabled = true;
    public bool soundEnabled = true;
    public bool vibrationEnabled = true;
    
    public static AudioManager Instance;
    
    private Dictionary<string, FMODSound> soundDictionary = new Dictionary<string, FMODSound>();
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudio();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        if (playMusicOnStart && !backgroundMusicEvent.IsNull)
        {
            PlayMusic(backgroundMusicEvent);
        }
    }
    
    void InitializeAudio()
    {
        SetupSounds();
        CreateDefaultSounds();
        
        if (!backgroundMusicEvent.IsNull)
        {
            musicInstance = RuntimeManager.CreateInstance(backgroundMusicEvent);
        }
    }
    
    void SetupSounds()
    {
        soundDictionary.Clear();
        
        foreach (FMODSound sound in sounds)
        {
            if (!sound.eventReference.IsNull)
            {
                soundDictionary[sound.name] = sound;
            }
        }
    }
    
    void CreateDefaultSounds()
    {
        if (soundDictionary.Count == 0)
        {
            CreateDummySound("click", "event:/SFX/Click", 1f);
            CreateDummySound("hit", "event:/SFX/Hit", 1f);
            CreateDummySound("perfect", "event:/SFX/Perfect", 1.2f);
            CreateDummySound("miss", "event:/SFX/Miss", 0.8f);
        }
    }
    
    void CreateDummySound(string soundName, string eventRef, float volume)
    {
        FMODSound dummySound = new FMODSound
        {
            name = soundName,
            eventReference = EventReference.Find(eventRef),
            volume = volume,
            is3D = false
        };
        
        soundDictionary[soundName] = dummySound;
    }
    
    public void PlaySound(string soundName)
    {
        if (!soundEnabled)
            return;
            
        if (soundDictionary.ContainsKey(soundName))
        {
            FMODSound sound = soundDictionary[soundName];
            if (!sound.eventReference.IsNull)
            {
                FMOD.Studio.EventInstance instance = RuntimeManager.CreateInstance(sound.eventReference);
                
                float finalVolume = sound.volume * soundVolume * masterVolume;
                instance.setVolume(finalVolume);
                
                if (sound.is3D)
                {
                    FMOD.ATTRIBUTES_3D attributes = RuntimeUtils.To3DAttributes(transform.position);
                    instance.set3DAttributes(attributes);
                }
                
                instance.start();
                instance.release();
            }
            else
            {
                PlaySynthesizedSound(soundName);
            }
        }
        else
        {
            Debug.LogWarning($"Sound '{soundName}' not found!");
        }
    }
    
    void PlaySynthesizedSound(string soundName)
    {
        switch (soundName)
        {
            case "click":
            case "hit":
                PlayBeep(800f, 0.1f);
                break;
            case "perfect":
                PlaySuccessSound();
                break;
            case "miss":
                PlayBeep(400f, 0.2f);
                break;
        }
    }
    
    void PlayBeep(float frequency, float duration)
    {
        Debug.Log($"Fallback beep sound: {frequency}Hz for {duration}s");
    }
    
    void PlayChord()
    {
        Debug.Log("Fallback chord sound");
    }
    
    void PlaySuccessSound()
    {
        Debug.Log("Fallback success sound");
    }
    
    public void PlayMusic(EventReference eventReference)
    {
        if (!musicEnabled || eventReference.IsNull)
            return;
            
        StopMusic();
        
        musicInstance = RuntimeManager.CreateInstance(eventReference);
        musicInstance.setVolume(musicVolume * masterVolume);
        musicInstance.start();
    }
    
    public void StopMusic()
    {
        if (musicInstance.isValid())
        {
            musicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            musicInstance.release();
        }
    }
    
    public void PauseMusic()
    {
        if (musicInstance.isValid())
        {
            musicInstance.setPaused(true);
        }
    }
    
    public void ResumeMusic()
    {
        if (musicInstance.isValid())
        {
            musicInstance.setPaused(false);
        }
    }
    
    public void PlayClickSound()
    {
        PlaySound("click");
        if (vibrationEnabled && Application.isMobilePlatform)
        {
            Handheld.Vibrate();
        }
    }
    
    public void PlayHitSound()
    {
        PlaySound("hit");
        if (vibrationEnabled && Application.isMobilePlatform)
        {
            Handheld.Vibrate();
        }
    }
    
    public void PlayPerfectSound()
    {
        PlaySound("perfect");
        if (vibrationEnabled && Application.isMobilePlatform)
        {
            Handheld.Vibrate();
        }
    }
    
    public void PlayMissSound()
    {
        PlaySound("miss");
    }
    
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }
    
    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }
    
    public void SetSoundVolume(float volume)
    {
        soundVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }
    
    void UpdateVolumes()
    {
        if (musicInstance.isValid())
        {
            musicInstance.setVolume(musicVolume * masterVolume);
        }
        
        RuntimeManager.StudioSystem.setParameterByName("MasterVolume", masterVolume);
        RuntimeManager.StudioSystem.setParameterByName("MusicVolume", musicVolume);
        RuntimeManager.StudioSystem.setParameterByName("SFXVolume", soundVolume);
    }
    
    public void ToggleMusic()
    {
        musicEnabled = !musicEnabled;
        
        if (musicEnabled)
        {
            if (!backgroundMusicEvent.IsNull)
            {
                PlayMusic(backgroundMusicEvent);
            }
        }
        else
        {
            StopMusic();
        }
    }
    
    public void ToggleSound()
    {
        soundEnabled = !soundEnabled;
    }
    
    public void ToggleVibration()
    {
        vibrationEnabled = !vibrationEnabled;
    }
    
    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            PauseMusic();
        }
        else
        {
            if (musicEnabled)
                ResumeMusic();
        }
    }
    
    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            PauseMusic();
        }
        else
        {
            if (musicEnabled)
                ResumeMusic();
        }
    }
}