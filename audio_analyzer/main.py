#!/usr/bin/env python3
"""
리듬게임 노트 생성 메인 스크립트
사용법: python main.py <mp3_file_path>
"""

import sys
import os
from note_generator import RhythmGameNoteGenerator

def main():
    if len(sys.argv) != 2:
        print("사용법: python main.py <mp3_file_path>")
        print("예시: python main.py song.mp3")
        sys.exit(1)
    
    mp3_file = sys.argv[1]
    
    if not os.path.exists(mp3_file):
        print(f"❌ 파일을 찾을 수 없습니다: {mp3_file}")
        sys.exit(1)
    
    if not mp3_file.lower().endswith(('.mp3', '.wav', '.flac', '.m4a')):
        print("⚠️ 지원되지 않는 파일 형식일 수 있습니다.")
    
    try:
        generator = RhythmGameNoteGenerator()
        output_file = generator.generate_notes(mp3_file)
        print(f"\n🎯 성공! 생성된 파일: {output_file}")
    except Exception as e:
        print(f"❌ 오류 발생: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()