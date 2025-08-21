import librosa
import numpy as np
import json
import os

def analyze_audio_and_generate_notes(audio_path, output_path):
    """오디오 파일을 분석해서 리듬게임 노트 데이터를 생성"""
    print(f"🎵 Analyzing audio: {audio_path}")
    
    # 오디오 로드
    y, sr = librosa.load(audio_path)
    duration = librosa.get_duration(y=y, sr=sr)
    
    print(f"📊 Duration: {duration:.2f} seconds")
    print(f"📊 Sample rate: {sr} Hz")
    
    # BPM 감지
    tempo, beats = librosa.beat.beat_track(y=y, sr=sr)
    beat_times = librosa.beat.beat_track(y=y, sr=sr, units='time')[1]
    
    print(f"🥁 Detected BPM: {tempo:.2f}")
    print(f"🎯 Found {len(beat_times)} beats")
    
    # 음성/보컬 감지를 위한 스펙트럼 분석
    stft = librosa.stft(y)
    spectral_centroids = librosa.feature.spectral_centroid(y=y, sr=sr)[0]
    
    # 에너지 감지
    rms_energy = librosa.feature.rms(y=y)[0]
    
    # Onset 감지 (음의 시작점)
    onset_frames = librosa.onset.onset_detect(y=y, sr=sr, units='time')
    print(f"🎶 Found {len(onset_frames)} onsets")
    
    # 노트 생성
    notes = []
    
    # 1. 기본 박자 기반 노트 (주 리듬)
    for i, beat_time in enumerate(beat_times):
        if beat_time < duration - 0.5:  # 끝부분 여유
            # 강박과 약박 구분
            is_strong_beat = i % 4 == 0  # 4/4박자 가정
            intensity = 0.8 if is_strong_beat else 0.6
            
            notes.append({
                "time_seconds": float(beat_time),
                "lane": 0,
                "type": "beat",
                "intensity": float(intensity)
            })
    
    # 2. Onset 기반 추가 노트 (멜로디/효과음)
    onset_threshold = np.percentile(rms_energy, 70)  # 상위 30% 에너지만
    
    for onset_time in onset_frames:
        if onset_time < duration - 0.5:
            # 기존 박자와 너무 가까운 onset은 제외
            too_close = any(abs(onset_time - note["time_seconds"]) < 0.1 
                          for note in notes)
            
            if not too_close:
                # 해당 시간의 에너지 레벨 확인
                frame_idx = int(onset_time * sr / 512)  # hop_length=512 기본값
                if frame_idx < len(rms_energy):
                    energy = rms_energy[frame_idx]
                    if energy > onset_threshold:
                        notes.append({
                            "time_seconds": float(onset_time),
                            "lane": 1,
                            "type": "melody",
                            "intensity": float(min(energy * 2, 1.0))
                        })
    
    # 3. 고주파 이벤트 감지 (심벌, 하이햇 등)
    high_freq_onsets = librosa.onset.onset_detect(
        y=y, sr=sr, units='time', 
        fmin=2000, fmax=8000  # 고주파 대역
    )
    
    for onset_time in high_freq_onsets:
        if onset_time < duration - 0.5:
            # 다른 노트와 겹치지 않는지 확인
            too_close = any(abs(onset_time - note["time_seconds"]) < 0.15 
                          for note in notes)
            
            if not too_close:
                notes.append({
                    "time_seconds": float(onset_time),
                    "lane": 2,
                    "type": "high",
                    "intensity": 0.7
                })
    
    # 시간순 정렬
    notes.sort(key=lambda x: x["time_seconds"])
    
    # 노트 데이터 구조
    note_data = {
        "metadata": {
            "title": "pre stg_re_128 Rhythm Chart",
            "description": "박자 기반 노트 + 멜로디 감지",
            "generator": "RhythmGameNoteGenerator v1.0",
            "audio_file": "pre stg_re_128.mp3"
        },
        "timing": {
            "bpm": float(tempo),
            "sample_rate": int(sr),
            "duration_seconds": float(duration)
        },
        "notes": notes
    }
    
    # JSON 파일 저장
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(note_data, f, indent=2, ensure_ascii=False)
    
    print(f"✅ Generated {len(notes)} notes")
    print(f"💾 Saved to: {output_path}")
    
    # 통계 출력
    beat_notes = [n for n in notes if n["type"] == "beat"]
    melody_notes = [n for n in notes if n["type"] == "melody"]
    high_notes = [n for n in notes if n["type"] == "high"]
    
    print(f"📊 Note breakdown:")
    print(f"   - Beat notes: {len(beat_notes)}")
    print(f"   - Melody notes: {len(melody_notes)}")
    print(f"   - High freq notes: {len(high_notes)}")
    
    return note_data

if __name__ == "__main__":
    audio_file = "/Users/deathcry/Work/RockStarter/audio_analyzer/pre stg_re_128.mp3"
    output_file = "/Users/deathcry/Work/RockStarter/pre-stg-re-128.json"
    
    if not os.path.exists(audio_file):
        print(f"❌ Audio file not found: {audio_file}")
        exit(1)
    
    try:
        note_data = analyze_audio_and_generate_notes(audio_file, output_file)
        print("🎮 Note generation complete!")
        
    except Exception as e:
        print(f"❌ Error: {e}")
        exit(1)