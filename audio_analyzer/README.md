# Audio Analyzer - 리듬게임 노트 생성기

MP3 파일을 분석하여 리듬게임의 노트를 자동으로 생성하는 모듈입니다.

## 특징

- **음악 파형 분석**: librosa를 사용한 고급 오디오 분석
- **박자 기반 노트**: 정확한 리듬 추출과 노트 배치
- **보컬 외침 감지**: "Hey!" "Busy!" 같은 서브보컬 감지
- **FMOD 호환**: 44.1kHz 샘플레이트, 다중 타이밍 형식
- **JSON 출력**: 표준화된 차트 데이터 형식

## 설치

```bash
pip install -r requirements.txt
```

## 사용법

### 명령줄 사용

```bash
# 기본 사용법
python main.py song.mp3

# 노트 생성기 직접 사용
python note_generator.py song.mp3
```

### 프로그램에서 사용

```python
from note_generator import RhythmGameNoteGenerator

generator = RhythmGameNoteGenerator()
output_file = generator.generate_notes("song.mp3")
print(f"생성된 파일: {output_file}")
```

## 출력 형식

생성되는 JSON 파일은 MP3 파일명과 동일한 이름으로 생성됩니다:
- `song.mp3` → `song.json`

```json
{
  "metadata": {
    "title": "Generated Rhythm Chart",
    "description": "박자 기반 노트 + 보컬 외침 감지",
    "generator": "RhythmGameNoteGenerator v1.0"
  },
  "timing": {
    "bpm": 126.0,
    "sample_rate": 44100,
    "beat_interval_ms": 476
  },
  "notes": [
    {
      "time_seconds": 1.234,
      "time_milliseconds": 1234,
      "time_samples": 54398,
      "lane": 1,
      "type": "tap",
      "intensity": 0.8,
      "source": "rhythm"
    }
  ],
  "stats": {
    "rhythm_notes": 137,
    "vocal_shout_notes": 8,
    "total_notes": 145
  }
}
```

## 노트 타입

- **tap**: 기본 탭 노트
- **hold**: 홀드 노트 (duration 속성 포함)

## 노트 소스

- **rhythm**: 박자 기반 노트
- **vocal_shout**: 보컬 외침 노트 (Hey! Busy! 등)

## 알고리즘

1. **템포 분석**: librosa beat tracking으로 BPM 감지
2. **박자 노트**: 음향 강도 기반 리듬 노트 생성
3. **외침 감지**: 고주파 에너지와 스펙트럴 센트로이드 분석
4. **레인 배치**: 시간대별 패턴과 강도 기반 배치
5. **FMOD 호환**: 다중 타이밍 형식 (초/밀리초/샘플)