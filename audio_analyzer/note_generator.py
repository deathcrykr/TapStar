#!/usr/bin/env python3
"""
음악 파일 분석을 통한 리듬게임 노트 생성기
MP3 파일을 분석하여 박자 기반 노트와 보컬 외침을 감지합니다.
"""

import librosa
import numpy as np
import json
import os
import sys
from scipy.signal import find_peaks

class RhythmGameNoteGenerator:
    def __init__(self, sample_rate=44100):
        self.sample_rate = sample_rate
        
    def generate_notes(self, mp3_file_path):
        """MP3 파일에서 리듬게임 노트를 생성합니다."""
        
        if not os.path.exists(mp3_file_path):
            raise FileNotFoundError(f"MP3 파일을 찾을 수 없습니다: {mp3_file_path}")
        
        print(f"🎵 음악 분석 시작: {os.path.basename(mp3_file_path)}")
        
        # 음악 로드
        y, sr = librosa.load(mp3_file_path, duration=None, sr=self.sample_rate)
        duration = len(y) / sr
        print(f"   곡 길이: {duration:.1f}초")
        
        # 템포와 비트 분석
        tempo, beats = self._analyze_tempo_and_beats(y, sr)
        print(f"   BPM: {tempo:.1f}, 비트: {len(beats)}개")
        
        # 기본 박자 노트 생성
        rhythm_notes = self._generate_rhythm_notes(y, sr, beats, tempo)
        print(f"   박자 노트: {len(rhythm_notes)}개")
        
        # 보컬 외침 감지
        shout_notes = self._detect_vocal_shouts(y, sr, rhythm_notes)
        print(f"   외침 노트: {len(shout_notes)}개")
        
        # 전체 노트 결합 및 정렬
        all_notes = rhythm_notes + shout_notes
        all_notes.sort(key=lambda x: x["time_seconds"])
        
        # 차트 데이터 구성
        mp3_filename = os.path.basename(mp3_file_path)
        chart = self._create_chart_data(tempo, sr, all_notes, len(rhythm_notes), len(shout_notes), mp3_filename)
        
        # 파일 저장
        output_file = self._get_output_filename(mp3_file_path)
        self._save_chart(chart, output_file)
        
        print(f"✅ 완료: {len(all_notes)}개 노트 -> {output_file}")
        return output_file
    
    def _analyze_tempo_and_beats(self, y, sr):
        """템포와 비트 분석"""
        tempo, beats = librosa.beat.beat_track(y=y, sr=sr, units='time')
        tempo_val = float(tempo[0]) if hasattr(tempo, '__len__') else float(tempo)
        return tempo_val, beats
    
    def _generate_rhythm_notes(self, y, sr, beats, tempo):
        """전문적인 리듬 게임 디자인 원칙 적용한 노트 생성"""
        # 곡 길이 계산 (130개 노트 목표를 위한 동적 조정)
        song_duration = len(y) / sr
        target_notes = 130
        target_density = target_notes / song_duration
        
        # 1. 강한 비트 강조를 위한 다중 음향 분석
        onset_strength = librosa.onset.onset_strength(y=y, sr=sr)
        onset_times = librosa.frames_to_time(np.arange(len(onset_strength)), sr=sr)
        
        # 드럼/베이스 강조를 위한 저주파 분석
        bass_strength = self._analyze_bass_frequencies(y, sr)
        bass_times = librosa.frames_to_time(np.arange(len(bass_strength)), sr=sr)
        
        # 각 비트의 종합적 강도 계산
        beat_strengths = []
        beat_bass_strengths = []
        for beat_time in beats:
            # 일반 onset 강도
            onset_idx = np.argmin(np.abs(onset_times - beat_time))
            onset_str = onset_strength[onset_idx] if onset_idx < len(onset_strength) else 0
            
            # 베이스 강도
            bass_idx = np.argmin(np.abs(bass_times - beat_time))
            bass_str = bass_strength[bass_idx] if bass_idx < len(bass_strength) else 0
            
            beat_strengths.append(onset_str)
            beat_bass_strengths.append(bass_str)
        
        # 정규화
        if beat_strengths:
            max_strength = np.max(beat_strengths)
            beat_strengths = [s / max_strength for s in beat_strengths]
            
        if beat_bass_strengths:
            max_bass = np.max(beat_bass_strengths)
            beat_bass_strengths = [s / max_bass for s in beat_bass_strengths]
        
        # 2. 과도한 노트 방지 및 음악성 강조
        notes = []
        downbeat_positions = []  # 강박 추적
        
        for i, (beat_time, strength, bass_strength) in enumerate(zip(beats, beat_strengths, beat_bass_strengths)):
            beat_in_measure = i % 4
            
            # 3. 강한 비트 우선 배치 (1박, 3박 강조)
            is_strong_beat = beat_in_measure in [0, 2]  # 1박, 3박
            is_downbeat = beat_in_measure == 0  # 1박 (최강박)
            
            # 종합 강도 계산 (베이스 + 일반, 강박 보정)
            combined_strength = (strength * 0.6 + bass_strength * 0.4)
            if is_downbeat:
                combined_strength *= 1.3  # 1박 강화
            elif is_strong_beat:
                combined_strength *= 1.1  # 3박 강화
            
            # 시간대별 임계값 (130개 노트 목표)
            threshold = self._get_adaptive_threshold(beat_time, is_strong_beat, tempo, target_density)
            
            # 4. 플로우 고려한 노트 배치
            if combined_strength >= threshold:
                # 연속된 노트 간격 체크 (플레이어 부담 감소)
                if self._is_good_note_spacing(beat_time, notes):
                    note = self._create_rhythm_note(beat_time, combined_strength, i, sr, is_downbeat)
                    notes.append(note)
                    
                    if is_downbeat:
                        downbeat_positions.append(len(notes) - 1)
        
        # 5. 홀드 노트로 지속음 표현
        self._add_hold_notes_for_sustains(notes, downbeat_positions, sr)
        
        return notes
    
    def _analyze_bass_frequencies(self, y, sr):
        """저주파(드럼/베이스) 강도 분석"""
        # STFT로 주파수 분석
        stft = librosa.stft(y, hop_length=512)
        magnitude = np.abs(stft)
        freqs = librosa.fft_frequencies(sr=sr)
        
        # 베이스 주파수 대역 (60-250Hz)
        bass_freq_mask = (freqs >= 60) & (freqs <= 250)
        bass_strength = np.mean(magnitude[bass_freq_mask], axis=0)
        
        return bass_strength
    
    def _get_musical_threshold(self, time, is_strong_beat, tempo):
        """음악적 맥락을 고려한 임계값 (130개 노트 목표)"""
        # 목표 노트 밀도: 130개 노트 / (곡 길이/60초) ≈ 1.1 notes/second
        target_density = 1.1
        
        # 현재 시간대의 기본 임계값
        if time < 20:
            base_threshold = 0.40  # 인트로 (적당히 조심스럽게)
        elif time < 40:
            base_threshold = 0.32  # 메인 부분 (더 활발하게)
        else:
            base_threshold = 0.36  # 후반부 (균형있게)
        
        # BPM 고려 (빠른 곡은 임계값 높임 - 과도한 노트 방지)
        if tempo > 140:
            base_threshold *= 1.3
        elif tempo < 100:
            base_threshold *= 0.85
            
        # 강박은 임계값 낮춤 (더 쉽게 선택)
        if is_strong_beat:
            base_threshold *= 0.8
            
        return base_threshold
    
    def _get_adaptive_threshold(self, time, is_strong_beat, tempo, target_density):
        """130개 노트 목표를 위한 적응형 임계값"""
        # 기본 임계값 (곡의 강도에 따라 조정) - 130개 목표로 최종 조정
        if target_density < 1.0:  # 짧은 곡
            base_threshold = 0.50
        elif target_density < 1.2:  # 표준 길이  
            base_threshold = 0.46
        else:  # 긴 곡
            base_threshold = 0.52
        
        # 시간대별 미세 조정
        if time < 20:
            time_factor = 1.1  # 인트로 약간 억제
        elif time < 40:
            time_factor = 0.9  # 메인 부분 활발
        else:
            time_factor = 1.0  # 후반부 균형
            
        # BPM 고려
        if tempo > 140:
            bpm_factor = 1.2
        elif tempo < 100:
            bpm_factor = 0.85
        else:
            bpm_factor = 1.0
            
        # 강박 보정
        strong_beat_factor = 0.8 if is_strong_beat else 1.0
        
        return base_threshold * time_factor * bpm_factor * strong_beat_factor
    
    def _is_good_note_spacing(self, current_time, existing_notes, min_gap=0.15):
        """적절한 노트 간격 확인 (플레이어 부담 감소)"""
        if not existing_notes:
            return True
            
        last_note_time = existing_notes[-1]["time_seconds"]
        return (current_time - last_note_time) >= min_gap
    
    def _add_hold_notes_for_sustains(self, notes, downbeat_positions, sr):
        """지속음 구간에 홀드 노트 추가"""
        for pos in downbeat_positions:
            if pos < len(notes):
                note = notes[pos]
                # 강한 다운비트 중 일부를 홀드로 변환
                if note["intensity"] > 0.8 and note["beat_position"] == 1:
                    note["type"] = "hold"
                    note.update({
                        "duration_seconds": 0.6,
                        "duration_milliseconds": 600,
                        "duration_samples": int(0.6 * sr)
                    })
    
    def _get_threshold_for_time(self, time):
        """시간대별 임계값 반환 (전문적 디자인 + 130개 정도 노트 균형)"""
        if time < 20:
            return 0.32  # 인트로/초반부 (낮춤 - 더 많은 노트)
        elif time < 40:
            return 0.26  # 랩/활발한 부분 (낮춤 - 더 많은 노트)
        else:
            return 0.30  # 후반부 (낮춤 - 더 많은 노트)
    
    def _create_rhythm_note(self, beat_time, strength, beat_index, sr, is_downbeat=False):
        """전문적인 리듬 노트 생성 (음악성 강조)"""
        beat_in_measure = beat_index % 4
        
        # 향상된 레인 배치 전략 (패턴 복잡도 조절)
        lane, intensity = self._get_musical_lane_placement(beat_time, beat_in_measure, strength, is_downbeat)
        
        # 기본 노트 타입 (홀드는 별도 함수에서 처리)
        note_type = "tap"
        
        # 노트 레벨 결정 (난이도)
        note_level = self._determine_note_level(strength, beat_in_measure, is_downbeat)
        
        note = {
            "time_seconds": round(float(beat_time), 3),
            "time_milliseconds": round(float(beat_time) * 1000),
            "time_samples": round(float(beat_time) * sr),
            "lane": lane,
            "type": note_type,
            "intensity": round(float(strength), 2),
            "level": note_level,
            "beat_position": beat_in_measure + 1,
            "source": "rhythm",
            "is_strong_beat": beat_in_measure in [0, 2],  # 분석용
            "is_downbeat": is_downbeat
        }
        
        return note
    
    def _determine_note_level(self, strength, beat_in_measure, is_downbeat):
        """노트 레벨(난이도) 결정"""
        # 1 = Easy (기본 박자만), 2 = Medium (보강), 3 = Hard (복잡한 패턴)
        
        if is_downbeat:
            # 강박은 항상 Easy에 포함
            return 1
        elif beat_in_measure in [0, 2]:
            # 1박, 3박은 Medium 레벨
            return 2 if strength > 0.6 else 1
        elif strength > 0.8:
            # 매우 강한 음은 Hard 레벨
            return 3
        elif strength > 0.5:
            # 중간 강도는 Medium 레벨
            return 2
        else:
            # 약한 음은 Easy 레벨
            return 1
    
    def _get_musical_lane_placement(self, beat_time, beat_in_measure, strength, is_downbeat):
        """음악적 맥락을 고려한 레인 배치"""
        # 1. 다운비트는 중앙 레인 선호 (시각적 강조)
        if is_downbeat and strength > 0.6:
            lane = 1 if strength > 0.8 else 2  # 중앙 레인 우선
            intensity = min(1.0, strength * 1.2)  # 다운비트 강화
            
        # 2. 시간대별 패턴 (단순화 → 복잡화)
        elif beat_time < 30:  # 초반부: 단순 패턴
            if beat_in_measure == 0:  # 1박
                lane = 1
                intensity = 1.0
            elif beat_in_measure == 2:  # 3박
                lane = 2
                intensity = 0.8
            else:  # 2,4박
                lane = 0 if beat_in_measure == 1 else 3
                intensity = 0.6
                
        else:  # 후반부: 더 복잡한 패턴
            # 교대 패턴 (플레이어 근육 기억 활용)
            pattern_cycle = (beat_time // 2) % 4  # 2초마다 패턴 변화
            lane_patterns = [
                [1, 0, 2, 3],  # 중앙 시작
                [0, 2, 1, 3],  # 좌우 교대
                [2, 1, 3, 0],  # 우측 중심
                [1, 2, 0, 3]   # 혼합 패턴
            ]
            lane = lane_patterns[int(pattern_cycle)][beat_in_measure]
            
            # 강도 조절 (과도한 노트 방지)
            if beat_in_measure == 0:
                intensity = min(1.0, strength * 1.1)
            elif beat_in_measure == 2:
                intensity = min(0.9, strength)
            else:
                intensity = min(0.7, strength * 0.9)
        
        return lane, intensity
    
    def _get_lane_for_early_section(self, beat_in_measure, strength):
        """전반부 레인 배치"""
        if beat_in_measure == 0:  # 1박
            return 1, 1.0
        elif beat_in_measure == 2:  # 3박
            return 2, 0.8
        else:  # 2박, 4박
            lane = 0 if beat_in_measure == 1 else 3
            return lane, 0.6
    
    def _get_lane_for_late_section(self, beat_index, beat_in_measure, strength):
        """후반부 레인 배치 (지그재그 패턴)"""
        lane_patterns = [0, 1, 2, 3, 2, 1]
        lane = lane_patterns[beat_index % len(lane_patterns)]
        
        if beat_in_measure == 0:
            intensity = 1.0
        elif beat_in_measure == 2:
            intensity = 0.9
        else:
            intensity = 0.7
        
        return lane, intensity
    
    def _detect_vocal_shouts(self, y, sr, existing_notes):
        """보컬 외침 감지 (Hey! Busy! 등)"""
        # 고주파 에너지 분석
        stft = librosa.stft(y, hop_length=512)
        magnitude = np.abs(stft)
        freqs = librosa.fft_frequencies(sr=sr)
        
        # 보컬 외침 주파수 대역 (2-6kHz)
        vocal_freq_mask = (freqs >= 2000) & (freqs <= 6000)
        high_freq_energy = np.mean(magnitude[vocal_freq_mask], axis=0)
        
        # 스펙트럴 센트로이드
        spectral_centroid = librosa.feature.spectral_centroid(y=y, sr=sr, hop_length=512)[0]
        
        # 시간 축
        shout_times = librosa.frames_to_time(np.arange(len(high_freq_energy)), sr=sr, hop_length=512)
        
        # 정규화 및 점수 계산
        high_freq_norm = high_freq_energy / np.max(high_freq_energy)
        centroid_norm = spectral_centroid / np.max(spectral_centroid)
        shout_score = (high_freq_norm * 0.7 + centroid_norm * 0.3)
        
        # 피크 감지 (균형잡힌 외침 노트 생성)
        shout_peaks, _ = find_peaks(
            shout_score, 
            height=0.55,  # 0.45 -> 0.55 (약간 높임)
            distance=int(sr/512 * 0.8),  # 0.7초 -> 0.8초 간격
            prominence=0.18  # 0.15 -> 0.18
        )
        
        # 외침 노트 생성
        shout_notes = []
        for peak_idx in shout_peaks:
            peak_time = shout_times[peak_idx]
            peak_strength = shout_score[peak_idx]
            
            # 기존 노트와 충돌 확인
            if self._is_too_close_to_existing_notes(peak_time, existing_notes, 0.3):
                continue
            
            if peak_strength > 0.6:  # 0.5 -> 0.6 (다시 높임)
                note = self._create_shout_note(peak_time, peak_strength, sr)
                shout_notes.append(note)
        
        return shout_notes
    
    def _is_too_close_to_existing_notes(self, time, existing_notes, threshold):
        """기존 노트와 너무 가까운지 확인"""
        return any(abs(note["time_seconds"] - time) < threshold for note in existing_notes)
    
    def _create_shout_note(self, peak_time, peak_strength, sr):
        """외침 노트 생성"""
        lane = 1 if peak_strength > 0.8 else 2  # 중앙 레인 우선
        
        # 보컬 노트 레벨 결정 (보컬은 일반적으로 Medium~Hard)
        shout_level = self._determine_shout_level(peak_strength)
        
        return {
            "time_seconds": round(float(peak_time), 3),
            "time_milliseconds": round(float(peak_time) * 1000),
            "time_samples": round(float(peak_time) * sr),
            "lane": lane,
            "type": "tap",
            "intensity": round(float(peak_strength), 2),
            "level": shout_level,
            "source": "vocal_shout"
        }
    
    def _determine_shout_level(self, strength):
        """보컬 외침 노트의 레벨 결정"""
        if strength > 0.85:
            return 3  # Hard - 매우 강한 외침
        elif strength > 0.7:
            return 2  # Medium - 일반 외침
        else:
            return 2  # Medium - 보컬은 기본적으로 Medium 이상
    
    def _create_chart_data(self, tempo, sr, notes, rhythm_count, shout_count, mp3_filename):
        """차트 데이터 구성"""
        # 보컬 구역 분석 추가
        vocal_sections = self._analyze_vocal_sections(notes)
        
        return {
            "metadata": {
                "title": "Generated Rhythm Chart",
                "description": "박자 기반 노트 + 보컬 외침 감지",
                "generator": "RhythmGameNoteGenerator v1.0",
                "audio_file": mp3_filename
            },
            "timing": {
                "bpm": tempo,
                "sample_rate": sr,
                "beat_interval_ms": round(60000 / tempo)
            },
            "notes": notes,
            "stats": {
                "rhythm_notes": rhythm_count,
                "vocal_shout_notes": shout_count,
                "total_notes": len(notes)
            },
            "vocal_sections": vocal_sections
        }
    
    def _get_output_filename(self, mp3_file_path):
        """출력 파일명 생성 (MP3 파일명.json)"""
        base_name = os.path.splitext(os.path.basename(mp3_file_path))[0]
        return f"{base_name}.json"
    
    def _analyze_vocal_sections(self, notes):
        """보컬 구역 분석 (실제 노트 패턴 기반 동적 구간)"""
        if not notes:
            return []
        
        sections = []
        
        # 전체 곡 길이 계산
        max_time = max(note["time_seconds"] for note in notes)
        
        # 10초 단위로 동적 구간 분석
        section_duration = 10
        current_time = 0
        section_index = 0
        
        while current_time < max_time:
            end_time = min(current_time + section_duration, max_time)
            
            # 해당 구간의 노트들 분석
            section_notes = [n for n in notes if current_time <= n["time_seconds"] < end_time]
            
            if section_notes:
                # 구간별 특성 분석
                rhythm_notes = [n for n in section_notes if n.get("source") == "rhythm"]
                shout_notes = [n for n in section_notes if n.get("source") == "vocal_shout"]
                
                # 노트 밀도 계산
                actual_duration = end_time - current_time
                note_density = len(section_notes) / actual_duration
                
                # 평균 강도 계산
                avg_intensity = sum(n.get("intensity", 0) for n in section_notes) / len(section_notes)
                
                # 보컬 타입 추정 (패턴 기반)
                vocal_type = self._determine_vocal_type(
                    note_density, avg_intensity, len(shout_notes), 
                    len(rhythm_notes), section_index
                )
                
                # 구간명 생성
                section_name = self._generate_section_name(section_index, vocal_type, note_density)
                
                sections.append({
                    "start_time": round(current_time, 1),
                    "end_time": round(end_time, 1),
                    "section_name": section_name,
                    "vocal_type": vocal_type,
                    "note_count": len(section_notes),
                    "note_density": round(note_density, 2),
                    "avg_intensity": round(avg_intensity, 2),
                    "rhythm_notes": len(rhythm_notes),
                    "vocal_shouts": len(shout_notes)
                })
            
            current_time = end_time
            section_index += 1
        
        return sections
    
    def _determine_vocal_type(self, note_density, avg_intensity, shout_count, rhythm_count, section_index):
        """노트 패턴으로 보컬 타입 추정"""
        # 인트로 (첫 구간)
        if section_index == 0:
            if note_density < 1.0:
                return "instrumental"
            elif shout_count > 0:
                return "sub_vocal"
            else:
                return "intro"
        
        # 외침이 많은 구간
        if shout_count >= 2:
            return "sub_vocal"
        
        # 고밀도 + 고강도 = 랩
        if note_density > 2.0 and avg_intensity > 0.7:
            return "rap"
        
        # 중밀도 + 고강도 = 메인 보컬
        if note_density > 1.0 and avg_intensity > 0.6:
            return "main_vocal"
        
        # 저밀도 = 인스트루멘탈
        if note_density < 0.8:
            return "instrumental"
        
        # 기본값
        return "main_vocal"
    
    def _generate_section_name(self, section_index, vocal_type, note_density):
        """구간명 생성"""
        base_names = ["intro", "verse1", "pre_chorus", "chorus", "verse2", 
                     "chorus2", "bridge", "outro", "instrumental"]
        
        if section_index < len(base_names):
            base_name = base_names[section_index]
        else:
            base_name = f"section_{section_index + 1}"
        
        # 보컬 타입에 따른 접미사
        if vocal_type == "rap" and not base_name.startswith("verse"):
            base_name += "_rap"
        elif vocal_type == "instrumental":
            base_name += "_inst"
        
        return base_name
    
    def _save_chart(self, chart, filename):
        """차트 데이터 저장"""
        with open(filename, 'w', encoding='utf-8') as f:
            json.dump(chart, f, indent=2, ensure_ascii=False)

def main():
    """메인 함수"""
    if len(sys.argv) != 2:
        print("사용법: python note_generator.py <mp3_file_path>")
        sys.exit(1)
    
    mp3_file = sys.argv[1]
    
    try:
        generator = RhythmGameNoteGenerator()
        output_file = generator.generate_notes(mp3_file)
        print(f"\n🎯 생성된 파일: {output_file}")
    except Exception as e:
        print(f"❌ 오류: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()