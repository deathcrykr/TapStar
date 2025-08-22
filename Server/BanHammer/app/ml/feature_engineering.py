import numpy as np
import pandas as pd
from typing import List, Dict, Any, Tuple, Optional
from collections import deque, defaultdict
from datetime import datetime, timedelta
import logging
from dataclasses import dataclass

logger = logging.getLogger(__name__)

@dataclass
class PlayerFeatures:
    """플레이어 행동 특징을 담는 클래스"""
    player_id: str
    
    # 기본 통계
    total_actions: int = 0
    session_duration: float = 0.0
    avg_action_interval: float = 0.0
    action_variance: float = 0.0
    
    # 행동 패턴
    action_type_distribution: Dict[str, float] = None
    peak_activity_hours: List[int] = None
    activity_consistency: float = 0.0
    
    # 값 분포 특징
    value_mean: float = 0.0
    value_std: float = 0.0
    value_skewness: float = 0.0
    value_kurtosis: float = 0.0
    
    # 시계열 특징
    trend_coefficient: float = 0.0
    seasonality_score: float = 0.0
    autocorrelation: float = 0.0
    
    # 이상치 지표
    outlier_ratio: float = 0.0
    spike_frequency: float = 0.0
    
    # 상호작용 특징
    sequence_entropy: float = 0.0
    pattern_repetition: float = 0.0
    
    def __post_init__(self):
        if self.action_type_distribution is None:
            self.action_type_distribution = {}
        if self.peak_activity_hours is None:
            self.peak_activity_hours = []

class FeatureExtractor:
    """플레이어 행동으로부터 머신러닝 특징을 추출하는 클래스"""
    
    def __init__(self, window_size: int = 100):
        self.window_size = window_size
        
    def extract_features(self, player_actions: List[Dict[str, Any]]) -> PlayerFeatures:
        """플레이어 행동 데이터로부터 특징을 추출합니다."""
        if not player_actions:
            return PlayerFeatures(player_id="unknown")
        
        df = pd.DataFrame(player_actions)
        player_id = df['player_id'].iloc[0] if 'player_id' in df else "unknown"
        
        features = PlayerFeatures(player_id=player_id)
        
        # 기본 통계 계산
        features.total_actions = len(df)
        features = self._extract_temporal_features(df, features)
        features = self._extract_behavioral_patterns(df, features)
        features = self._extract_value_features(df, features)
        features = self._extract_time_series_features(df, features)
        features = self._extract_anomaly_features(df, features)
        features = self._extract_sequence_features(df, features)
        
        return features
    
    def _extract_temporal_features(self, df: pd.DataFrame, features: PlayerFeatures) -> PlayerFeatures:
        """시간 관련 특징 추출"""
        if 'timestamp' in df.columns:
            timestamps = pd.to_datetime(df['timestamp'])
            
            # 세션 지속 시간
            if len(timestamps) > 1:
                features.session_duration = (timestamps.max() - timestamps.min()).total_seconds() / 60  # 분 단위
                
                # 행동 간격 통계
                intervals = timestamps.diff().dt.total_seconds().dropna()
                if len(intervals) > 0:
                    features.avg_action_interval = intervals.mean()
                    features.action_variance = intervals.var()
                
                # 활동 시간대 분석
                hours = timestamps.dt.hour.value_counts()
                features.peak_activity_hours = hours.nlargest(3).index.tolist()
                
                # 활동 일관성 (시간대별 활동 분산의 역수)
                hourly_counts = timestamps.dt.hour.value_counts()
                features.activity_consistency = 1.0 / (hourly_counts.std() + 1e-6)
        
        return features
    
    def _extract_behavioral_patterns(self, df: pd.DataFrame, features: PlayerFeatures) -> PlayerFeatures:
        """행동 패턴 특징 추출"""
        if 'action_type' in df.columns:
            action_counts = df['action_type'].value_counts()
            total_actions = len(df)
            
            # 행동 유형 분포
            features.action_type_distribution = (action_counts / total_actions).to_dict()
            
        return features
    
    def _extract_value_features(self, df: pd.DataFrame, features: PlayerFeatures) -> PlayerFeatures:
        """값 관련 특징 추출"""
        if 'value' in df.columns and df['value'].dtype in ['float64', 'int64']:
            values = df['value']
            
            # 기본 통계
            features.value_mean = values.mean()
            features.value_std = values.std()
            
            # 분포 형태
            if len(values) > 3:
                features.value_skewness = values.skew()
                features.value_kurtosis = values.kurtosis()
        
        return features
    
    def _extract_time_series_features(self, df: pd.DataFrame, features: PlayerFeatures) -> PlayerFeatures:
        """시계열 특징 추출"""
        if 'timestamp' in df.columns and 'value' in df.columns:
            df_sorted = df.sort_values('timestamp')
            values = df_sorted['value'].values
            
            if len(values) > 10:
                # 트렌드 계수 (선형 회귀의 기울기)
                x = np.arange(len(values))
                if len(values) > 1:
                    trend_coef = np.polyfit(x, values, 1)[0]
                    features.trend_coefficient = trend_coef
                
                # 자기상관
                if len(values) > 5:
                    features.autocorrelation = self._calculate_autocorrelation(values, lag=1)
        
        return features
    
    def _extract_anomaly_features(self, df: pd.DataFrame, features: PlayerFeatures) -> PlayerFeatures:
        """이상치 관련 특징 추출"""
        if 'value' in df.columns:
            values = df['value']
            
            if len(values) > 3:
                # IQR 기반 이상치 비율
                Q1 = values.quantile(0.25)
                Q3 = values.quantile(0.75)
                IQR = Q3 - Q1
                
                if IQR > 0:
                    outliers = values[(values < Q1 - 1.5 * IQR) | (values > Q3 + 1.5 * IQR)]
                    features.outlier_ratio = len(outliers) / len(values)
                
                # 급등 빈도 (평균의 2배 이상인 값들의 비율)
                mean_val = values.mean()
                if mean_val > 0:
                    spikes = values[values > mean_val * 2]
                    features.spike_frequency = len(spikes) / len(values)
        
        return features
    
    def _extract_sequence_features(self, df: pd.DataFrame, features: PlayerFeatures) -> PlayerFeatures:
        """시퀀스 관련 특징 추출"""
        if 'action_type' in df.columns:
            actions = df['action_type'].tolist()
            
            # 시퀀스 엔트로피 (다양성 측정)
            features.sequence_entropy = self._calculate_entropy(actions)
            
            # 패턴 반복도
            features.pattern_repetition = self._calculate_pattern_repetition(actions)
        
        return features
    
    def _calculate_autocorrelation(self, series: np.ndarray, lag: int = 1) -> float:
        """자기상관 계산"""
        try:
            if len(series) <= lag:
                return 0.0
            
            n = len(series)
            mean = np.mean(series)
            c0 = np.sum((series - mean) ** 2) / n
            
            if c0 == 0:
                return 0.0
                
            c_lag = np.sum((series[:-lag] - mean) * (series[lag:] - mean)) / (n - lag)
            return c_lag / c0
        except:
            return 0.0
    
    def _calculate_entropy(self, sequence: List[str]) -> float:
        """시퀀스의 엔트로피 계산"""
        if not sequence:
            return 0.0
        
        counts = pd.Series(sequence).value_counts()
        probabilities = counts / len(sequence)
        
        entropy = -np.sum(probabilities * np.log2(probabilities + 1e-10))
        return entropy
    
    def _calculate_pattern_repetition(self, sequence: List[str], pattern_length: int = 3) -> float:
        """패턴 반복도 계산"""
        if len(sequence) < pattern_length * 2:
            return 0.0
        
        patterns = {}
        total_patterns = 0
        
        for i in range(len(sequence) - pattern_length + 1):
            pattern = tuple(sequence[i:i + pattern_length])
            patterns[pattern] = patterns.get(pattern, 0) + 1
            total_patterns += 1
        
        if total_patterns == 0:
            return 0.0
        
        # 가장 많이 반복된 패턴의 비율
        max_repetitions = max(patterns.values())
        return max_repetitions / total_patterns

class AdvancedFeatureExtractor(FeatureExtractor):
    """고급 특징 추출기 - 딥러닝을 위한 추가 특징들"""
    
    def extract_cnn_features(self, player_actions: List[Dict[str, Any]], sequence_length: int = 50) -> np.ndarray:
        """CNN 입력을 위한 시퀀스 특징 추출"""
        if not player_actions:
            return np.zeros((sequence_length, 5))
        
        df = pd.DataFrame(player_actions)
        
        # 수치형 특징들 추출
        features = []
        for _, row in df.iterrows():
            feature_vector = [
                self._encode_action_type(row.get('action_type', '')),
                row.get('value', 0.0),
                self._extract_time_features(row.get('timestamp')),
                self._extract_metadata_features(row.get('metadata', {})),
                1.0  # 패딩을 위한 마스크
            ]
            features.append(feature_vector)
        
        # 시퀀스 길이 맞추기 (패딩 또는 트랜케이션)
        if len(features) < sequence_length:
            # 패딩
            padding = [[0.0, 0.0, 0.0, 0.0, 0.0] for _ in range(sequence_length - len(features))]
            features.extend(padding)
        else:
            # 최신 데이터만 사용
            features = features[-sequence_length:]
        
        return np.array(features, dtype=np.float32)
    
    def _encode_action_type(self, action_type: str) -> float:
        """행동 유형을 수치로 인코딩"""
        action_mapping = {
            'reward_collection': 1.0,
            'resource_gather': 2.0,
            'level_progress': 3.0,
            'purchase': 4.0,
            'movement': 5.0,
            'combat': 6.0,
            'trade': 7.0
        }
        return action_mapping.get(action_type, 0.0)
    
    def _extract_time_features(self, timestamp) -> float:
        """타임스탬프로부터 시간 특징 추출"""
        if timestamp is None:
            return 0.0
        
        try:
            if isinstance(timestamp, str):
                dt = pd.to_datetime(timestamp)
            else:
                dt = timestamp
            
            # 하루 중 시간을 0-1 사이의 값으로 변환
            hour_ratio = dt.hour / 24.0
            return hour_ratio
        except:
            return 0.0
    
    def _extract_metadata_features(self, metadata: Dict[str, Any]) -> float:
        """메타데이터로부터 특징 추출"""
        if not metadata:
            return 0.0
        
        # 메타데이터의 복잡성을 수치로 표현
        complexity = len(str(metadata)) / 100.0  # 정규화
        return min(complexity, 1.0)

class RealTimeFeatureBuffer:
    """실시간 특징 추출을 위한 버퍼"""
    
    def __init__(self, buffer_size: int = 1000):
        self.buffer_size = buffer_size
        self.player_buffers: Dict[str, deque] = defaultdict(lambda: deque(maxlen=buffer_size))
        self.feature_extractor = FeatureExtractor()
    
    def add_action(self, player_id: str, action_data: Dict[str, Any]):
        """새로운 행동을 버퍼에 추가"""
        self.player_buffers[player_id].append(action_data)
    
    def get_features(self, player_id: str) -> Optional[PlayerFeatures]:
        """플레이어의 현재 특징을 계산"""
        if player_id not in self.player_buffers:
            return None
        
        actions = list(self.player_buffers[player_id])
        if not actions:
            return None
        
        return self.feature_extractor.extract_features(actions)
    
    def get_recent_features(self, player_id: str, recent_count: int = 50) -> Optional[PlayerFeatures]:
        """최근 N개 행동에 대한 특징만 계산"""
        if player_id not in self.player_buffers:
            return None
        
        actions = list(self.player_buffers[player_id])[-recent_count:]
        if not actions:
            return None
        
        return self.feature_extractor.extract_features(actions)
    
    def cleanup_old_data(self, hours_threshold: int = 24):
        """오래된 데이터 정리"""
        cutoff_time = datetime.now() - timedelta(hours=hours_threshold)
        
        for player_id in list(self.player_buffers.keys()):
            buffer = self.player_buffers[player_id]
            
            # 최근 데이터만 유지
            filtered_actions = []
            for action in buffer:
                if 'timestamp' in action:
                    try:
                        action_time = pd.to_datetime(action['timestamp'])
                        if action_time > cutoff_time:
                            filtered_actions.append(action)
                    except:
                        # 타임스탬프 파싱 실패시 유지
                        filtered_actions.append(action)
                else:
                    # 타임스탬프 없는 경우 유지
                    filtered_actions.append(action)
            
            if filtered_actions:
                self.player_buffers[player_id] = deque(filtered_actions, maxlen=self.buffer_size)
            else:
                del self.player_buffers[player_id]