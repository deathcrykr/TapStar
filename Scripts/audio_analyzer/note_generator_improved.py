#!/usr/bin/env python3
"""
전문적인 리듬 게임 노트 생성기 - 개선 버전
리듬 게임 커뮤니티 가이드라인 및 전문 원칙 적용:
1. 강한 비트(드럼/베이스) 중심 노트 배치
2. 오버차팅 방지 및 음악성 강조  
3. 홀드 노트로 지속음 표현
4. 난이도별 패턴 복잡도 조절
5. 이지 모드에서도 충분한 리듬감 보장
6. 보컬 구역화: vocal, sub_vocal, rap, ttaechang(때창) 자동 분류
"""

import librosa
import numpy as np
import json
import os
import sys
from scipy.signal import find_peaks
from sklearn.cluster import KMeans
from sklearn.preprocessing import StandardScaler

class ProfessionalRhythmNoteGenerator:
    def __init__(self, sample_rate=44100):
        self.sample_rate = sample_rate
        
    def analyze_vocal_sections(self, y, sr):
        """음성학적 특성을 분석해서 보컬 구역을 분류합니다."""
        print("   🎤 Analyzing vocal sections...")
        
        # 1. 보컬 분리 (전경/배경)
        S_full, phase = librosa.magphase(librosa.stft(y))
        S_filter = librosa.decompose.nn_filter(S_full, 
                                               aggregate=np.median, 
                                               metric='cosine',
                                               width=int(librosa.time_to_frames(2, sr=sr)))
        S_filter = np.minimum(S_full, S_filter)
        
        # 보컬 마스크 생성
        margin_v = 10
        power = 2
        mask_v = librosa.util.softmask(S_full - S_filter, margin_v * S_filter, power=power)
        S_vocal = mask_v * S_full
        y_vocal = librosa.istft(S_vocal * phase)
        
        # 2. 음성 특성 분석을 위한 특징 추출
        hop_length = 512
        frame_length = 2048
        
        # 기본 특징들
        mfcc = librosa.feature.mfcc(y=y_vocal, sr=sr, n_mfcc=13, hop_length=hop_length)
        chroma = librosa.feature.chroma_stft(y=y_vocal, sr=sr, hop_length=hop_length)
        spectral_centroid = librosa.feature.spectral_centroid(y=y_vocal, sr=sr, hop_length=hop_length)
        zero_crossing_rate = librosa.feature.zero_crossing_rate(y_vocal, frame_length=frame_length, hop_length=hop_length)
        spectral_flatness = librosa.feature.spectral_flatness(y=y_vocal, hop_length=hop_length)
        rms_energy = librosa.feature.rms(y=y_vocal, hop_length=hop_length)
        
        # 피치 안정성 분석
        pitches, magnitudes = librosa.piptrack(y=y_vocal, sr=sr, hop_length=hop_length)
        pitch_values = []
        for t in range(pitches.shape[1]):
            index = magnitudes[:, t].argmax()
            pitch = pitches[index, t] if magnitudes[index, t] > 0.1 else 0
            pitch_values.append(pitch)
        pitch_stability = np.std(pitch_values)
        
        # 템포그램 (리듬 분석)  
        tempo, beats = librosa.beat.beat_track(y=y_vocal, sr=sr)
        tempogram = librosa.feature.tempogram(y=y_vocal, sr=sr)
        
        # 3. 시간 축 생성
        times = librosa.frames_to_time(np.arange(chroma.shape[1]), sr=sr, hop_length=hop_length)
        
        # 4. 구간별 특징 벡터 생성 (2초 윈도우)
        window_size = int(2 * sr / hop_length)  # 2초
        sections = []
        
        for i in range(0, len(times), window_size // 2):  # 50% 오버랩
            if i + window_size >= len(times):
                break
                
            start_idx = i
            end_idx = min(i + window_size, len(times))
            start_time = times[start_idx]
            end_time = times[end_idx - 1]
            
            # 구간별 특징 계산
            section_features = self._extract_section_features(
                mfcc[:, start_idx:end_idx],
                chroma[:, start_idx:end_idx], 
                spectral_centroid[:, start_idx:end_idx],
                zero_crossing_rate[:, start_idx:end_idx],
                spectral_flatness[:, start_idx:end_idx],
                rms_energy[:, start_idx:end_idx]
            )
            
            # 구간 분류
            section_type = self._classify_vocal_section(section_features)
            
            sections.append({
                'start_time': float(start_time),
                'end_time': float(end_time),
                'type': section_type,
                'features': section_features
            })
        
        print(f"   ✅ Detected {len(sections)} vocal sections")
        return sections
    
    def _extract_section_features(self, mfcc, chroma, spectral_centroid, zcr, flatness, rms):
        """구간별 음성 특징을 추출합니다."""
        features = {}
        
        # MFCC 통계
        features['mfcc_mean'] = float(np.mean(mfcc))
        features['mfcc_std'] = float(np.std(mfcc))
        features['mfcc_delta_mean'] = float(np.mean(np.diff(mfcc, axis=1)))
        
        # 크로마 (화성/멜로디)
        features['chroma_strength'] = float(np.mean(np.max(chroma, axis=0)))
        features['chroma_std'] = float(np.std(chroma))
        
        # 스펙트럴 특성
        features['spectral_centroid_mean'] = float(np.mean(spectral_centroid))
        features['spectral_centroid_std'] = float(np.std(spectral_centroid))
        
        # 제로 크로싱 비율 (언어적 특성)
        features['zcr_mean'] = float(np.mean(zcr))
        features['zcr_std'] = float(np.std(zcr))
        
        # 스펙트럴 평탄도 (화성성)
        features['flatness_mean'] = float(np.mean(flatness))
        features['flatness_std'] = float(np.std(flatness))
        
        # RMS 에너지
        features['rms_mean'] = float(np.mean(rms))
        features['rms_std'] = float(np.std(rms))
        
        # 리듬감 (에너지 변화)
        features['energy_variation'] = float(np.std(rms) / (np.mean(rms) + 1e-8))
        
        return features
    
    def _classify_vocal_section(self, features):
        """음성학적 특징으로 보컬 섹션을 분류합니다."""
        
        # 실제 음악 데이터 기반 조정된 분류 기준
        
        # 랩 판별: 높은 ZCR, 높은 스펙트럴 센트로이드, 높은 에너지 변화
        is_rap = (features['zcr_mean'] > 0.18 and 
                  features['spectral_centroid_mean'] > 5000 and
                  features['energy_variation'] > 0.95 and
                  features['mfcc_delta_mean'] > 0.05)  # 빠른 음성 변화
        
        # 때창/코러스 판별: 높은 에너지, 높은 화성성, 안정된 피치
        is_ttaechang = (features['rms_mean'] > 0.02 and
                        features['chroma_strength'] >= 0.95 and
                        features['flatness_mean'] < 0.0002 and
                        features['energy_variation'] > 0.9 and
                        features['zcr_std'] < 0.15)  # 안정된 음성
        
        # 서브 보컬 판별: 낮은 에너지, 낮은 스펙트럴 센트로이드, 안정성
        is_sub_vocal = (features['rms_mean'] < 0.014 and
                        features['spectral_centroid_mean'] < 4500 and
                        features['zcr_mean'] < 0.17 and
                        features['energy_variation'] < 0.90 and
                        features['mfcc_delta_mean'] < 0.05)  # 느린 음성 변화
        
        # 분류 우선순위 (더 구체적인 것부터)
        if is_rap:
            return 'rap'
        elif is_ttaechang:
            return 'ttaechang'
        elif is_sub_vocal:
            return 'sub_vocal'
        else:
            return 'vocal'  # 기본 보컬
        
    def generate_notes(self, mp3_file_path):
        """MP3 파일에서 전문적인 리듬게임 노트를 생성합니다."""
        
        if not os.path.exists(mp3_file_path):
            raise FileNotFoundError(f"MP3 파일을 찾을 수 없습니다: {mp3_file_path}")
        
        print(f"🎵 Professional Rhythm Analysis: {os.path.basename(mp3_file_path)}")
        
        # 음악 로드
        y, sr = librosa.load(mp3_file_path, duration=None, sr=self.sample_rate)
        duration = len(y) / sr
        print(f"   Duration: {duration:.1f}s")
        
        # 템포와 비트 분석
        tempo, beats = self._analyze_tempo_and_beats(y, sr)
        print(f"   BPM: {tempo:.1f}, Beats: {len(beats)}")
        
        # 음악 구조 분석 (Principle 1: 강한 비트 강조)
        musical_elements = self._analyze_musical_elements(y, sr, beats, tempo)
        print(f"   Musical Elements Analyzed")
        
        # 보컬 구역 분석 (새로운 기능)
        vocal_sections = self.analyze_vocal_sections(y, sr)
        musical_elements['vocal_sections'] = vocal_sections
        
        # 전문적인 노트 생성 (모든 원칙 통합)
        all_notes = self._generate_professional_notes(y, sr, beats, tempo, musical_elements, duration)
        print(f"   Generated {len(all_notes)} notes with professional design")
        
        # 노트 정보 분석 및 요약 출력
        self._print_note_analysis_summary(all_notes, tempo, duration, vocal_sections)
        
        # 차트 데이터 구성
        mp3_filename = os.path.basename(mp3_file_path)
        chart = self._create_professional_chart_data(tempo, sr, all_notes, mp3_filename, musical_elements)
        
        # 파일 저장
        output_file = self._get_output_filename(mp3_file_path)
        self._save_chart(chart, output_file)
        
        print(f"✅ Professional Chart Created: {output_file}")
        return output_file
    
    def _analyze_tempo_and_beats(self, y, sr):
        """정확한 템포와 비트 분석"""
        tempo, beats = librosa.beat.beat_track(y=y, sr=sr, units='time', hop_length=512)
        tempo_val = float(tempo[0]) if hasattr(tempo, '__len__') else float(tempo)
        return tempo_val, beats
    
    def _analyze_musical_elements(self, y, sr, beats, tempo):
        """음악의 핵심 요소 분석 (Principle 1: 강한 비트 강조)"""
        print("   🎯 Analyzing Core Musical Elements...")
        
        # 1. 드럼/퍼커션 분석 (20-250Hz)
        drum_strength = self._analyze_frequency_band(y, sr, 20, 250, "drums")
        
        # 2. 베이스 라인 분석 (60-300Hz) 
        bass_strength = self._analyze_frequency_band(y, sr, 60, 300, "bass")
        
        # 3. 멜로디 분석 (200-2000Hz)
        melody_strength = self._analyze_frequency_band(y, sr, 200, 2000, "melody")
        
        # 4. 보컬/상위 주파수 분석 (1000-8000Hz)
        vocal_strength = self._analyze_frequency_band(y, sr, 1000, 8000, "vocal")
        
        # 5. 동적 분석 (RMS 에너지)
        rms_energy = librosa.feature.rms(y=y, hop_length=512)[0]
        
        return {
            'drum_strength': drum_strength,
            'bass_strength': bass_strength, 
            'melody_strength': melody_strength,
            'vocal_strength': vocal_strength,
            'rms_energy': rms_energy,
            'tempo': tempo
        }
    
    def _analyze_frequency_band(self, y, sr, freq_min, freq_max, band_name):
        """특정 주파수 대역 분석"""
        stft = librosa.stft(y, hop_length=512)
        magnitude = np.abs(stft)
        freqs = librosa.fft_frequencies(sr=sr)
        
        freq_mask = (freqs >= freq_min) & (freqs <= freq_max)
        band_strength = np.mean(magnitude[freq_mask], axis=0)
        
        # 정규화
        if np.max(band_strength) > 0:
            band_strength = band_strength / np.max(band_strength)
            
        return band_strength
    
    def _generate_professional_notes(self, y, sr, beats, tempo, musical_elements, duration):
        """전문적인 원칙에 따른 노트 생성 - 차팅 가이드 기반"""
        print("   🎼 Applying Advanced Charting Principles...")
        
        notes = []
        
        # 음악 구조 분석 (섹션별 처리)
        sections = self._analyze_music_structure(y, sr, beats, duration)
        
        # 점진적 레이어링 (Progressive Layering)
        # 1. 메인 하모닉 리드 (Pitch Relevancy 기반)
        main_harmonic_notes = self._create_main_harmonic_layer(beats, musical_elements, sections)
        notes.extend(main_harmonic_notes)
        
        # 2. 드럼 & 퍼커션 레이어 (킥/스네어)
        drum_notes = self._create_drum_layer(beats, musical_elements, sections)
        notes.extend(drum_notes)
        
        # 3. 보컬 레이어
        vocal_notes = self._create_vocal_layer(y, sr, musical_elements, sections)
        notes.extend(vocal_notes)
        
        # 4. 세컨더리 멜로디 (Medium/Hard용)
        secondary_notes = self._create_secondary_melody_layer(musical_elements, sections)
        notes.extend(secondary_notes)
        
        # 정렬 및 중복 제거
        notes.sort(key=lambda x: x["time_seconds"])
        notes = self._remove_overlapping_notes(notes)
        
        # 레벨별 노트 개수 계산
        easy_count = len([n for n in notes if n['level']==1])
        medium_count = len([n for n in notes if n['level']==2]) 
        hard_count = len([n for n in notes if n['level']==3])
        
        print(f"     📊 레벨별 노트 개수:")
        print(f"        Level 1 (Easy)  : {easy_count}개")
        print(f"        Level 2 (Medium): {medium_count}개") 
        print(f"        Level 3 (Hard)  : {hard_count}개")
        print(f"        총합            : {easy_count + medium_count + hard_count}개")
        
        return notes
    
    def _analyze_music_structure(self, y, sr, beats, duration):
        """음악 구조 분석 - 인트로/벌스/코러스/브릿지 등 섹션 구분"""
        sections = []
        rms_energy = librosa.feature.rms(y=y, hop_length=512)[0]
        times = librosa.frames_to_time(np.arange(len(rms_energy)), sr=sr, hop_length=512)
        
        # 에너지 레벨로 섹션 구분 (단순화된 버전)
        energy_threshold = np.mean(rms_energy) + np.std(rms_energy) * 0.5
        
        section_start = 0
        current_energy_high = rms_energy[0] > energy_threshold
        
        for i, energy in enumerate(rms_energy):
            is_high_energy = energy > energy_threshold
            
            # 에너지 레벨 변화 감지
            if is_high_energy != current_energy_high:
                section_end = times[i]
                section_type = "chorus" if current_energy_high else "verse"
                sections.append({
                    'start': section_start,
                    'end': section_end,
                    'type': section_type,
                    'energy_level': np.mean(rms_energy[max(0, i-50):i+1])
                })
                section_start = section_end
                current_energy_high = is_high_energy
        
        # 마지막 섹션 추가
        sections.append({
            'start': section_start,
            'end': duration,
            'type': "outro" if len(sections) > 0 else "full",
            'energy_level': np.mean(rms_energy[-50:])
        })
        
        print(f"     🎵 Detected {len(sections)} music sections")
        return sections
    
    def _create_main_harmonic_layer(self, beats, musical_elements, sections):
        """메인 하모닉 리드 생성 - 피치 연관성(PR) 기반"""
        print("     🎼 Creating Main Harmonic Layer (Pitch Relevancy)...")
        
        notes = []
        melody_strength = musical_elements['melody_strength']
        times = librosa.frames_to_time(np.arange(len(melody_strength)), sr=self.sample_rate, hop_length=512)
        
        for i, beat_time in enumerate(beats):
            # 현재 섹션 찾기
            current_section = self._find_current_section(beat_time, sections)
            section_modifier = 1.2 if current_section['type'] == 'chorus' else 0.8
            
            time_idx = np.argmin(np.abs(times - beat_time))
            if time_idx < len(melody_strength):
                melody_power = melody_strength[time_idx] * section_modifier
                
                # 피치 연관성을 위한 레인 결정 (4레인: 0=낮음, 3=높음)
                lane = self._determine_pitch_relevant_lane(melody_power, beat_time, i)
                
                # 메인 하모닉은 Easy 레벨, 강한 멜로디만
                if melody_power > 0.6:
                    note = self._create_note(
                        beat_time, melody_power, i,
                        note_type="tap", level=1, source="main_harmonic",
                        lane=lane
                    )
                    notes.append(note)
        
        print(f"     ✅ Main Harmonic: {len(notes)} notes with pitch relevancy")
        return notes
    
    def _create_drum_layer(self, beats, musical_elements, sections):
        """드럼 & 퍼커션 레이어 - 킥/스네어 중심"""
        print("     🥁 Creating Drum Layer (Kicks & Snares)...")
        
        notes = []
        drum_strength = musical_elements['drum_strength']
        bass_strength = musical_elements['bass_strength']
        times = librosa.frames_to_time(np.arange(len(drum_strength)), sr=self.sample_rate, hop_length=512)
        
        for i, beat_time in enumerate(beats):
            beat_in_measure = i % 4
            time_idx = np.argmin(np.abs(times - beat_time))
            
            if time_idx < len(drum_strength):
                drum_power = drum_strength[time_idx]
                bass_power = bass_strength[time_idx]
                combined_power = drum_power * 0.8 + bass_power * 0.2
                
                # 킥/스네어 패턴 생성 (인간 공학적 제약 고려)
                should_place = False
                level = 1
                note_type = "tap"
                
                # 강한 다운비트 (킥)
                if beat_in_measure == 0 and combined_power > 0.7:
                    should_place = True
                    lane = 1  # 중앙-좌측 (킥)
                    
                # 스네어 (3박)
                elif beat_in_measure == 2 and combined_power > 0.6:
                    should_place = True
                    lane = 2  # 중앙-우측 (스네어)
                    level = 2  # Medium 난이도
                
                # 복잡한 드럼 패턴 (Hard)
                elif combined_power > 0.8:
                    should_place = True
                    lane = 0 if beat_in_measure % 2 == 0 else 3  # 양쪽 끝
                    level = 3
                
                if should_place and self._is_good_spacing_advanced(beat_time, notes, lane):
                    note = self._create_note(
                        beat_time, combined_power, i,
                        note_type=note_type, level=level, source="drums",
                        lane=lane
                    )
                    notes.append(note)
        
        print(f"     ✅ Drums: {len(notes)} notes with ergonomic patterns")
        return notes
    
    def _create_vocal_layer(self, y, sr, musical_elements, sections):
        """보컬 레이어 - 보컬 라인 추적"""
        print("     🎤 Creating Vocal Layer...")
        
        notes = []
        vocal_strength = musical_elements['vocal_strength']
        times = librosa.frames_to_time(np.arange(len(vocal_strength)), sr=sr, hop_length=512)
        
        # 보컬 피크 감지
        vocal_peaks, _ = find_peaks(vocal_strength, height=0.65, distance=int(sr/512 * 0.3))
        
        for peak_idx in vocal_peaks:
            if peak_idx < len(times):
                peak_time = times[peak_idx]
                strength = vocal_strength[peak_idx]
                
                # 현재 섹션의 타입에 따라 레벨 조정
                current_section = self._find_current_section(peak_time, sections)
                if current_section['type'] == 'chorus':
                    level = 2  # 코러스에서 보컬 강조
                else:
                    level = 3  # 버스에서는 Hard 레벨
                
                # 보컬은 높은 피치이므로 상위 레인 사용
                lane = 3 if strength > 0.8 else 2
                
                note = self._create_note(
                    peak_time, strength, 0,
                    note_type="tap", level=level, source="vocal",
                    lane=lane
                )
                notes.append(note)
        
        print(f"     ✅ Vocals: {len(notes)} notes following vocal lines")
        return notes
    
    def _create_secondary_melody_layer(self, musical_elements, sections):
        """세컨더리 멜로디 레이어 - Medium/Hard 복잡성 추가"""
        print("     🎹 Creating Secondary Melody Layer...")
        
        notes = []
        melody_strength = musical_elements['melody_strength']
        times = librosa.frames_to_time(np.arange(len(melody_strength)), sr=self.sample_rate, hop_length=512)
        
        # 세컨더리 멜로디 감지 (중간 강도)
        for i, strength in enumerate(melody_strength):
            if 0.4 < strength < 0.65:  # 중간 강도 멜로디
                time_point = times[i]
                
                # 섹션별 처리
                current_section = self._find_current_section(time_point, sections)
                if current_section['type'] == 'chorus':
                    level = 2  # 코러스에서는 Medium
                    lane = 1  # 중앙
                else:
                    level = 3  # 기타 섹션에서는 Hard
                    lane = 0  # 좌측
                
                # 간격 체크 (오버차팅 방지)
                if self._is_good_spacing_advanced(time_point, notes, lane, min_gap=0.2):
                    note = self._create_note(
                        time_point, strength, 0,
                        note_type="tap", level=level, source="secondary_melody",
                        lane=lane
                    )
                    notes.append(note)
        
        print(f"     ✅ Secondary: {len(notes)} notes for complexity")
        return notes
    
    def _find_current_section(self, time_point, sections):
        """현재 시간점에 해당하는 음악 섹션 찾기"""
        for section in sections:
            if section['start'] <= time_point < section['end']:
                return section
        return sections[-1]  # 기본값: 마지막 섹션
    
    def _determine_pitch_relevant_lane(self, melody_power, beat_time, beat_index):
        """피치 연관성 기반 레인 결정 (0=낮음, 3=높음)"""
        # 멜로디 강도와 시간을 조합하여 피치 추정
        pitch_estimate = melody_power + (beat_time % 8) / 8 * 0.3
        
        if pitch_estimate < 0.3:
            return 0  # 낮은 피치 -> 좌측
        elif pitch_estimate < 0.6:
            return 1  # 중저음 -> 중앙 좌
        elif pitch_estimate < 0.8:
            return 2  # 중고음 -> 중앙 우
        else:
            return 3  # 높은 피치 -> 우측
    
    def _is_good_spacing_advanced(self, current_time, existing_notes, target_lane, min_gap=0.15):
        """향상된 간격 체크 - 레인별 간격 고려"""
        if not existing_notes:
            return True
        
        # 같은 레인의 최근 노트들만 체크 (잭 방지)
        same_lane_notes = [n for n in existing_notes[-5:] if n.get("lane") == target_lane]
        
        for note in same_lane_notes:
            if abs(note["time_seconds"] - current_time) < min_gap:
                return False
        
        # 전체 노트와의 최소 간격도 체크
        for note in existing_notes[-3:]:
            if abs(note["time_seconds"] - current_time) < 0.08:
                return False
        
        return True
    
    def _create_core_rhythm_layer(self, beats, tempo, musical_elements, duration):
        """핵심 리듬 레이어 생성 (Easy 모드 기준, 충분한 리듬감 보장)"""
        print("     🥁 Creating Core Rhythm Layer...")
        
        notes = []
        drum_strength = musical_elements['drum_strength']
        bass_strength = musical_elements['bass_strength']
        
        # 시간 프레임 매핑
        times = librosa.frames_to_time(np.arange(len(drum_strength)), sr=self.sample_rate, hop_length=512)
        
        for i, beat_time in enumerate(beats):
            beat_in_measure = i % 4
            is_downbeat = beat_in_measure == 0  # 1박
            is_strong_beat = beat_in_measure in [0, 2]  # 1박, 3박
            
            # 해당 시간의 음악 강도 계산
            time_idx = np.argmin(np.abs(times - beat_time))
            if time_idx < len(drum_strength):
                drum_power = drum_strength[time_idx]
                bass_power = bass_strength[time_idx]
                combined_power = (drum_power * 0.7 + bass_power * 0.3)
                
                # Easy 모드에서도 충분한 리듬감을 위한 스마트 선택 (더 엄격한 기준)
                should_place_note = False
                
                if is_downbeat and combined_power > 0.65:  # 1박은 매우 강할 때만
                    should_place_note = True
                elif is_strong_beat and combined_power > 0.8:  # 3박은 극강할 때만
                    should_place_note = True
                elif combined_power > 0.9:  # 극강 노트만
                    should_place_note = True
                    
                # 이지 모드 리듬감 보장: 6박자마다 최소 1개 노트 (더 여유롭게)
                if not should_place_note and beat_in_measure == 0:
                    recent_notes = [n for n in notes if abs(n["time_seconds"] - beat_time) < 3.0]  # 3초로 확장
                    if len(recent_notes) == 0:  # 최근 3초간 노트가 없으면 강제 배치
                        should_place_note = True
                        combined_power = max(combined_power, 0.6)  # 최소 강도 보장
                
                if should_place_note and self._is_good_spacing(beat_time, notes, min_gap=0.15):
                    note = self._create_note(
                        beat_time, combined_power, i, 
                        note_type="tap", level=1, source="core_rhythm"
                    )
                    notes.append(note)
        
        print(f"     ✅ Core Rhythm: {len(notes)} notes for solid rhythm foundation")
        return notes
    
    def _create_sustain_holds(self, y, sr, beats, musical_elements, duration):
        """지속음을 위한 홀드 노트 생성 (Principle 3)"""
        print("     🎵 Creating Sustain Hold Notes...")
        
        notes = []
        melody_strength = musical_elements['melody_strength'] 
        times = librosa.frames_to_time(np.arange(len(melody_strength)), sr=sr, hop_length=512)
        
        # 지속음 구간 감지
        sustained_regions = []
        current_sustain = None
        threshold = 0.6
        
        for i, time in enumerate(times):
            if i < len(melody_strength) and melody_strength[i] > threshold:
                if current_sustain is None:
                    current_sustain = {'start': time, 'strength': melody_strength[i]}
                else:
                    current_sustain['end'] = time
                    current_sustain['max_strength'] = max(current_sustain.get('max_strength', 0), melody_strength[i])
            else:
                if current_sustain and 'end' in current_sustain:
                    sustain_duration = current_sustain['end'] - current_sustain['start']
                    if sustain_duration >= 0.8:  # 0.8초 이상만 홀드 노트로
                        sustained_regions.append(current_sustain)
                current_sustain = None
        
        # 홀드 노트 생성
        for region in sustained_regions:
            duration_sec = region['end'] - region['start']
            if duration_sec >= 1.0:  # 1초 이상만 홀드로
                note = self._create_note(
                    region['start'], region['max_strength'], 0,
                    note_type="hold", level=1, source="sustain_hold",
                    duration_seconds=min(duration_sec, 3.0)  # 최대 3초
                )
                notes.append(note)
        
        print(f"     ✅ Sustain Holds: {len(notes)} hold notes for musical expression")
        return notes
    
    def _create_enhancement_layers(self, beats, musical_elements, duration):
        """Medium/Hard 난이도용 추가 레이어"""
        print("     🎯 Creating Enhancement Layers (Medium/Hard)...")
        
        notes = []
        melody_strength = musical_elements['melody_strength']
        vocal_strength = musical_elements['vocal_strength']
        times = librosa.frames_to_time(np.arange(len(melody_strength)), sr=self.sample_rate, hop_length=512)
        
        # Medium 레이어: 멜로디 강조
        for i, beat_time in enumerate(beats):
            if i < len(times):
                time_idx = np.argmin(np.abs(times - beat_time))
                if time_idx < len(melody_strength):
                    melody_power = melody_strength[time_idx]
                    
                    # Medium 노트: 멜로디가 강할 때
                    if melody_power > 0.65:
                        note = self._create_note(
                            beat_time, melody_power, i,
                            note_type="tap", level=2, source="melody_accent"
                        )
                        notes.append(note)
        
        # Hard 레이어: 복잡한 패턴과 보컬 액센트
        vocal_peaks, _ = find_peaks(vocal_strength, height=0.7, distance=int(self.sample_rate/512 * 0.5))
        vocal_times = times[vocal_peaks] if len(vocal_peaks) > 0 else []
        
        for vocal_time in vocal_times:
            if vocal_time < duration - 0.5:  # 곡 끝 여유
                strength = vocal_strength[np.argmin(np.abs(times - vocal_time))]
                note = self._create_note(
                    vocal_time, strength, 0,
                    note_type="tap", level=3, source="vocal_accent"
                )
                notes.append(note)
        
        print(f"     ✅ Enhancement: {len(notes)} notes for higher difficulties")
        return notes
    
    def _create_artistic_accents(self, musical_elements, beats, duration):
        """음악적 표현을 위한 특별 노트 (Principle 6: 노트 아트)"""
        print("     🎨 Creating Artistic Accents...")
        
        notes = []
        rms_energy = musical_elements['rms_energy']
        times = librosa.frames_to_time(np.arange(len(rms_energy)), sr=self.sample_rate, hop_length=512)
        
        # 다이나믹 변화 지점 감지 (크레센도, 디미누엔도)
        energy_diff = np.diff(rms_energy)
        crescendo_points = find_peaks(energy_diff, height=0.01, distance=int(self.sample_rate/512 * 2))[0]
        
        for peak_idx in crescendo_points:
            if peak_idx < len(times):
                peak_time = times[peak_idx]
                if peak_time < duration - 0.5:
                    strength = rms_energy[peak_idx]
                    
                    # 음악적 흐름을 표현하는 특별 노트
                    note = self._create_note(
                        peak_time, strength, 0,
                        note_type="tap", level=2, source="dynamic_accent"
                    )
                    notes.append(note)
        
        print(f"     ✅ Artistic: {len(notes)} notes for musical expression")
        return notes
    
    def _create_note(self, time_seconds, intensity, beat_index, note_type="tap", level=1, source="rhythm", duration_seconds=None, lane=None):
        """전문적인 노트 생성 - 피치 연관성 지원"""
        note = {
            "time_seconds": round(float(time_seconds), 6),  # 마이크로초 정밀도로 개선
            "time_milliseconds": round(float(time_seconds) * 1000),
            "time_samples": round(float(time_seconds) * self.sample_rate),
            "lane": lane if lane is not None else self._get_professional_lane_placement(time_seconds, intensity, level, source),
            "type": note_type,
            "intensity": round(float(intensity), 2),
            "level": level,
            "source": source
        }
        
        # 홀드 노트 정보 추가
        if note_type == "hold" and duration_seconds:
            note.update({
                "duration_seconds": round(duration_seconds, 2),
                "duration_milliseconds": round(duration_seconds * 1000),
                "duration_samples": round(duration_seconds * self.sample_rate)
            })
            
        # 박자 정보 (필요시)
        if beat_index >= 0:
            note.update({
                "beat_position": (beat_index % 4) + 1,
                "is_strong_beat": (beat_index % 4) in [0, 2],
                "is_downbeat": (beat_index % 4) == 0
            })
        
        return note
    
    def _get_professional_lane_placement(self, time_seconds, intensity, level, source):
        """전문적인 레인 배치 알고리즘"""
        # 음악적 맥락을 고려한 레인 배치
        
        # 강한 음은 중앙 레인 선호
        if intensity > 0.8:
            return 1  # 중앙
        elif intensity > 0.6:
            return 2  # 중앙 우측
        
        # 시간에 따른 패턴 변화
        time_pattern = int(time_seconds / 8) % 3  # 8초마다 패턴 변화
        
        if time_pattern == 0:  # 초기: 단순 패턴
            return 0 if intensity < 0.5 else 1
        elif time_pattern == 1:  # 중기: 좌우 교대
            return 0 if (int(time_seconds * 2) % 2) == 0 else 3
        else:  # 후기: 다양한 배치
            return int((time_seconds * intensity * 4)) % 4
    
    def _is_good_spacing(self, current_time, existing_notes, min_gap=0.12):
        """적절한 노트 간격 확인 (오버차팅 방지)"""
        if not existing_notes:
            return True
        
        for note in existing_notes[-3:]:  # 최근 3개 노트만 검사
            if abs(note["time_seconds"] - current_time) < min_gap:
                return False
        return True
    
    def _remove_overlapping_notes(self, notes):
        """중복/겹치는 노트 제거"""
        if not notes:
            return notes
            
        filtered_notes = [notes[0]]
        
        for note in notes[1:]:
            # 직전 노트와 시간 차이 확인
            if abs(note["time_seconds"] - filtered_notes[-1]["time_seconds"]) >= 0.08:
                filtered_notes.append(note)
            else:
                # 더 강한 노트를 선택
                if note["intensity"] > filtered_notes[-1]["intensity"]:
                    filtered_notes[-1] = note
        
        return filtered_notes
    
    def _create_professional_chart_data(self, tempo, sr, notes, mp3_filename, musical_elements):
        """전문적인 차트 데이터 생성"""
        
        # 난이도별 통계
        level_stats = {}
        for level in [1, 2, 3]:
            level_notes = [n for n in notes if n["level"] == level]
            level_stats[f"level_{level}_notes"] = len(level_notes)
        
        # 소스별 통계  
        source_stats = {}
        sources = set(note.get("source", "unknown") for note in notes)
        for source in sources:
            source_notes = [n for n in notes if n.get("source") == source]
            source_stats[f"{source}_notes"] = len(source_notes)
        
        return {
            "metadata": {
                "title": "Professional Rhythm Chart",
                "description": "전문 리듬게임 원칙 기반 차트",
                "generator": "ProfessionalRhythmNoteGenerator v2.0",
                "audio_file": mp3_filename,
                "design_principles": [
                    "강한 비트(드럼/베이스) 중심 배치",
                    "오버차팅 방지 및 음악성 강조", 
                    "홀드 노트로 지속음 표현",
                    "난이도별 패턴 복잡도 조절",
                    "이지 모드 리듬감 보장",
                    "음악적 표현 강화",
                    "보컬 구역 자동 분류 (vocal, sub_vocal, rap, ttaechang)"
                ]
            },
            "timing": {
                "bpm": tempo,
                "sample_rate": sr,
                "beat_interval_ms": round(60000 / tempo)
            },
            "vocal_sections": musical_elements.get('vocal_sections', []),
            "notes": notes,
            "stats": {
                "total_notes": len(notes),
                **level_stats,
                **source_stats
            },
            "difficulty_info": {
                "easy": {
                    "description": "핵심 리듬만, 충분한 리듬감 보장",
                    "note_count": level_stats.get("level_1_notes", 0),
                    "focus": "드럼/베이스 강조, 강박 중심"
                },
                "medium": {
                    "description": "멜로디 레이어 추가", 
                    "note_count": level_stats.get("level_1_notes", 0) + level_stats.get("level_2_notes", 0),
                    "focus": "리듬 + 멜로디 강조"
                },
                "hard": {
                    "description": "모든 음악적 요소 포함",
                    "note_count": len(notes),
                    "focus": "복잡한 패턴 + 보컬 액센트"
                }
            }
        }
    
    def _get_output_filename(self, mp3_file_path):
        """출력 파일명 생성 - MP3 파일명과 동일하게"""
        base_name = os.path.splitext(os.path.basename(mp3_file_path))[0]
        return f"{base_name}.json"
    
    def _print_note_analysis_summary(self, notes, tempo, duration, vocal_sections=None):
        """노트 분석 결과 상세 요약 출력"""
        print("\n" + "="*60)
        print("📊 NOTE ANALYSIS SUMMARY")
        print("="*60)
        
        # 기본 통계
        total_notes = len(notes)
        notes_per_second = total_notes / duration if duration > 0 else 0
        print(f"🎵 총 노트 수: {total_notes}개")
        print(f"⏱️  노트 밀도: {notes_per_second:.2f}개/초")
        print(f"🎼 평균 BPM: {tempo:.1f}")
        
        # 난이도별 분석
        level_stats = {}
        for level in [1, 2, 3]:
            level_notes = [n for n in notes if n["level"] == level]
            level_stats[level] = level_notes
        
        print(f"\n📈 난이도별 노트 분포:")
        level_names = {1: "Easy", 2: "Medium", 3: "Hard"}
        for level in [1, 2, 3]:
            count = len(level_stats[level])
            percentage = (count / total_notes * 100) if total_notes > 0 else 0
            print(f"   {level_names[level]:6}: {count:3}개 ({percentage:4.1f}%)")
        
        # 피치 연관성 분석 (레인 분포)
        print(f"\n🛣️  피치 연관성 (레인 분포):")
        lane_names = {0: "Low (왼쪽)", 1: "Mid-Low", 2: "Mid-High", 3: "High (오른쪽)"}
        lanes = {}
        for note in notes:
            lane = note.get("lane", 0)
            lanes[lane] = lanes.get(lane, 0) + 1
        
        for lane in sorted(lanes.keys()):
            count = lanes[lane]
            percentage = (count / total_notes * 100) if total_notes > 0 else 0
            name = lane_names.get(lane, f"Lane {lane}")
            print(f"   {name:12}: {count:3}개 ({percentage:4.1f}%)")
        
        # 노트 타입별 분석
        print(f"\n🎯 노트 타입 분석:")
        tap_notes = [n for n in notes if n.get("type", "tap") == "tap"]
        hold_notes = [n for n in notes if n.get("type") == "hold"]
        print(f"   Tap Notes : {len(tap_notes):3}개 ({len(tap_notes)/total_notes*100:4.1f}%)")
        print(f"   Hold Notes: {len(hold_notes):3}개 ({len(hold_notes)/total_notes*100:4.1f}%)")
        
        # 소스별 분석
        print(f"\n🎼 노트 생성 소스 분석:")
        sources = {}
        for note in notes:
            source = note.get("source", "unknown")
            sources[source] = sources.get(source, 0) + 1
        
        source_names = {
            "core_rhythm": "핵심 리듬",
            "sustain_hold": "지속음 홀드",
            "melody_accent": "멜로디 강조",
            "vocal_accent": "보컬 액센트",
            "dynamic_accent": "다이나믹 강조"
        }
        
        for source, count in sorted(sources.items()):
            name = source_names.get(source, source)
            percentage = (count / total_notes * 100) if total_notes > 0 else 0
            print(f"   {name:12}: {count:3}개 ({percentage:4.1f}%)")
        
        # 레인 분포 분석
        print(f"\n🛣️  레인 분포 분석:")
        lanes = {}
        for note in notes:
            lane = note.get("lane", 0)
            lanes[lane] = lanes.get(lane, 0) + 1
        
        for lane in sorted(lanes.keys()):
            count = lanes[lane]
            percentage = (count / total_notes * 100) if total_notes > 0 else 0
            print(f"   Lane {lane}: {count:3}개 ({percentage:4.1f}%)")
        
        # 보컬 구역 분석 추가
        if vocal_sections:
            print(f"\n🎤 보컬 구역 분석:")
            vocal_types = {}
            total_duration = 0
            for section in vocal_sections:
                section_type = section['type']
                duration = section['end_time'] - section['start_time']
                vocal_types[section_type] = vocal_types.get(section_type, 0) + duration
                total_duration += duration
            
            type_names = {
                'vocal': '메인 보컬',
                'sub_vocal': '서브 보컬', 
                'rap': '랩',
                'ttaechang': '때창/코러스'
            }
            
            for vocal_type, total_time in sorted(vocal_types.items()):
                name = type_names.get(vocal_type, vocal_type)
                percentage = (total_time / total_duration * 100) if total_duration > 0 else 0
                print(f"   {name:12}: {total_time:5.1f}초 ({percentage:4.1f}%)")
            
            print(f"   총 보컬 시간: {total_duration:.1f}초 ({len(vocal_sections)}개 구간)")
        
        # 강도 분석
        intensities = [n.get("intensity", 0) for n in notes]
        if intensities:
            avg_intensity = sum(intensities) / len(intensities)
            max_intensity = max(intensities)
            min_intensity = min(intensities)
            print(f"\n💪 강도 분석:")
            print(f"   평균 강도: {avg_intensity:.3f}")
            print(f"   최대 강도: {max_intensity:.3f}")
            print(f"   최소 강도: {min_intensity:.3f}")
        
        # 타이밍 간격 분석
        if len(notes) > 1:
            time_gaps = []
            for i in range(1, len(notes)):
                gap = notes[i]["time_seconds"] - notes[i-1]["time_seconds"]
                time_gaps.append(gap)
            
            avg_gap = sum(time_gaps) / len(time_gaps)
            min_gap = min(time_gaps)
            max_gap = max(time_gaps)
            
            print(f"\n⏰ 노트 간격 분석:")
            print(f"   평균 간격: {avg_gap:.3f}초")
            print(f"   최소 간격: {min_gap:.3f}초")
            print(f"   최대 간격: {max_gap:.3f}초")
        
        # 전문성 검증
        print(f"\n✅ 전문 원칙 적용 검증:")
        
        # 1. 강한 비트 비율
        strong_beat_notes = [n for n in notes if n.get("is_strong_beat", False) or n.get("is_downbeat", False)]
        strong_beat_ratio = len(strong_beat_notes) / total_notes if total_notes > 0 else 0
        print(f"   강한 비트 비율: {strong_beat_ratio*100:.1f}% ({'✓' if strong_beat_ratio > 0.4 else '⚠️'})")
        
        # 2. 오버차팅 확인 (최소 간격 체크)
        overcharting_safe = min(time_gaps) >= 0.08 if time_gaps else True
        print(f"   오버차팅 방지: {'✓' if overcharting_safe else '⚠️'} (최소간격: {min(time_gaps):.3f}초)" if time_gaps else "   오버차팅 방지: ✓")
        
        # 3. 이지 모드 리듬감 (적정 범위: 0.3~1.0개/초)
        easy_notes = level_stats[1]
        easy_density = len(easy_notes) / duration if duration > 0 else 0
        rhythm_good = 0.3 <= easy_density <= 1.0  # 적정 범위
        density_status = "✓" if rhythm_good else ("⚠️ 너무 많음" if easy_density > 1.0 else "⚠️ 너무 적음")
        print(f"   Easy 리듬감: {density_status} ({easy_density:.2f}개/초)")
        
        # 4. 홀드 노트 활용
        has_holds = len(hold_notes) > 0
        print(f"   홀드 노트 활용: {'✓' if has_holds else '-'} ({len(hold_notes)}개)")
        
        # 5. 인간 공학적 제약 분석 (잭 패턴 체크)
        jack_count = self._analyze_jack_patterns(notes)
        jack_ratio = jack_count / total_notes if total_notes > 0 else 0
        jack_safe = jack_ratio < 0.1  # 10% 미만이면 안전
        print(f"   잭 패턴 최소화: {'✓' if jack_safe else '⚠️'} ({jack_count}개, {jack_ratio*100:.1f}%)")
        
        # 6. 피치 연관성 품질
        pitch_variance = self._analyze_pitch_relevancy_quality(notes)
        pitch_good = pitch_variance > 0.3  # 충분한 레인 다양성
        print(f"   피치 연관성: {'✓' if pitch_good else '⚠️'} (레인 분산도: {pitch_variance:.2f})")
        
        print("="*60)
        print("🎯 Advanced Rhythm Chart Analysis Complete!")
        print("="*60)
    
    def _analyze_jack_patterns(self, notes):
        """잭 패턴 분석 - 같은 레인의 연속 노트"""
        jack_count = 0
        sorted_notes = sorted(notes, key=lambda x: x["time_seconds"])
        
        for i in range(1, len(sorted_notes)):
            prev_note = sorted_notes[i-1]
            curr_note = sorted_notes[i]
            
            # 같은 레인에서 0.3초 이내 연속 노트는 잭으로 간주
            if (prev_note.get("lane") == curr_note.get("lane") and 
                curr_note["time_seconds"] - prev_note["time_seconds"] < 0.3):
                jack_count += 1
        
        return jack_count
    
    def _analyze_pitch_relevancy_quality(self, notes):
        """피치 연관성 품질 분석 - 레인 분산도"""
        lanes = [note.get("lane", 0) for note in notes]
        if not lanes:
            return 0
        
        # 레인 분산도 계산 (0~1, 높을수록 다양함)
        lane_counts = {}
        for lane in lanes:
            lane_counts[lane] = lane_counts.get(lane, 0) + 1
        
        total_notes = len(lanes)
        variance = 0
        for count in lane_counts.values():
            ratio = count / total_notes
            variance += ratio * (1 - ratio)
        
        return variance

    def _save_chart(self, chart, filename):
        """차트 데이터 저장"""
        with open(filename, 'w', encoding='utf-8') as f:
            json.dump(chart, f, indent=2, ensure_ascii=False)

def main():
    """메인 함수"""
    if len(sys.argv) != 2:
        print("Usage: python note_generator_improved.py <mp3_file_path>")
        sys.exit(1)
    
    mp3_file = sys.argv[1]
    
    try:
        generator = ProfessionalRhythmNoteGenerator()
        output_file = generator.generate_notes(mp3_file)
        print(f"\n🎯 Professional Chart Generated: {output_file}")
    except Exception as e:
        print(f"❌ Error: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()