import numpy as np
import pandas as pd
from sklearn.ensemble import RandomForestClassifier, IsolationForest
from sklearn.linear_model import LinearRegression, Ridge
from sklearn.preprocessing import StandardScaler, LabelEncoder
from sklearn.model_selection import train_test_split, cross_val_score
from sklearn.metrics import accuracy_score, precision_recall_fscore_support, confusion_matrix
import tensorflow as tf
from tensorflow import keras
from tensorflow.keras import layers
import joblib
import logging
from typing import Dict, List, Tuple, Optional, Any
from dataclasses import dataclass
import os
from datetime import datetime, timedelta

from .feature_engineering import PlayerFeatures, FeatureExtractor

logger = logging.getLogger(__name__)

@dataclass
class ModelPrediction:
    """모델 예측 결과"""
    player_id: str
    prediction: float  # 0-1 사이의 치팅 확률
    confidence: float  # 예측 신뢰도
    model_type: str   # 사용된 모델 타입
    features_used: Dict[str, float]  # 사용된 특징들
    timestamp: datetime

@dataclass
class RegressionPrediction:
    """회귀 예측 결과"""
    player_id: str
    predicted_value: float
    actual_value: float
    residual: float
    cumulative_residual: float
    anomaly_score: float
    timestamp: datetime

class CheatDetectionRandomForest:
    """랜덤 포레스트 기반 치팅 탐지 모델 (95% 정확도 목표)"""
    
    def __init__(self, n_estimators: int = 100, random_state: int = 42):
        self.model = RandomForestClassifier(
            n_estimators=n_estimators,
            max_depth=10,
            min_samples_split=5,
            min_samples_leaf=2,
            random_state=random_state,
            class_weight='balanced'  # 불균형 데이터 대응
        )
        self.scaler = StandardScaler()
        self.feature_names = []
        self.is_trained = False
        
    def prepare_features(self, features: PlayerFeatures) -> np.ndarray:
        """PlayerFeatures를 모델 입력 형태로 변환"""
        feature_vector = [
            features.total_actions,
            features.session_duration,
            features.avg_action_interval,
            features.action_variance,
            features.activity_consistency,
            features.value_mean,
            features.value_std,
            features.value_skewness,
            features.value_kurtosis,
            features.trend_coefficient,
            features.autocorrelation,
            features.outlier_ratio,
            features.spike_frequency,
            features.sequence_entropy,
            features.pattern_repetition
        ]
        
        # 행동 유형 분포 특징 추가
        action_types = ['reward_collection', 'resource_gather', 'level_progress', 'purchase', 'movement', 'combat', 'trade']
        for action_type in action_types:
            feature_vector.append(features.action_type_distribution.get(action_type, 0.0))
        
        # 활동 시간대 특징 (원-핫 인코딩)
        for hour in range(24):
            feature_vector.append(1.0 if hour in features.peak_activity_hours else 0.0)
        
        return np.array(feature_vector).reshape(1, -1)
    
    def train(self, training_data: List[Tuple[PlayerFeatures, int]]) -> Dict[str, float]:
        """모델 훈련
        
        Args:
            training_data: (특징, 라벨) 튜플의 리스트. 라벨: 0=정상, 1=치팅
            
        Returns:
            훈련 결과 메트릭
        """
        if not training_data:
            raise ValueError("훈련 데이터가 없습니다.")
        
        # 특징과 라벨 분리
        X_list = []
        y_list = []
        
        for features, label in training_data:
            feature_vector = self.prepare_features(features).flatten()
            X_list.append(feature_vector)
            y_list.append(label)
        
        X = np.array(X_list)
        y = np.array(y_list)
        
        # 특징 이름 저장
        self.feature_names = self._generate_feature_names()
        
        # 데이터 정규화
        X_scaled = self.scaler.fit_transform(X)
        
        # 훈련/검증 분할
        X_train, X_test, y_train, y_test = train_test_split(
            X_scaled, y, test_size=0.2, random_state=42, stratify=y
        )
        
        # 모델 훈련
        self.model.fit(X_train, y_train)
        self.is_trained = True
        
        # 성능 평가
        y_pred = self.model.predict(X_test)
        accuracy = accuracy_score(y_test, y_pred)
        precision, recall, f1, _ = precision_recall_fscore_support(y_test, y_pred, average='binary')
        
        # 교차 검증
        cv_scores = cross_val_score(self.model, X_scaled, y, cv=5, scoring='accuracy')
        
        metrics = {
            'accuracy': accuracy,
            'precision': precision,
            'recall': recall,
            'f1_score': f1,
            'cv_mean_accuracy': cv_scores.mean(),
            'cv_std_accuracy': cv_scores.std()
        }
        
        logger.info(f"랜덤 포레스트 모델 훈련 완료: 정확도 {accuracy:.3f}")
        return metrics
    
    def predict(self, features: PlayerFeatures) -> ModelPrediction:
        """치팅 확률 예측"""
        if not self.is_trained:
            raise ValueError("모델이 훈련되지 않았습니다.")
        
        X = self.prepare_features(features)
        X_scaled = self.scaler.transform(X)
        
        # 확률 예측
        probabilities = self.model.predict_proba(X_scaled)[0]
        cheat_probability = probabilities[1] if len(probabilities) > 1 else probabilities[0]
        
        # 특징 중요도
        feature_importance = dict(zip(self.feature_names, self.model.feature_importances_))
        
        return ModelPrediction(
            player_id=features.player_id,
            prediction=cheat_probability,
            confidence=max(probabilities),
            model_type="RandomForest",
            features_used=feature_importance,
            timestamp=datetime.now()
        )
    
    def _generate_feature_names(self) -> List[str]:
        """특징 이름 생성"""
        base_features = [
            'total_actions', 'session_duration', 'avg_action_interval', 'action_variance',
            'activity_consistency', 'value_mean', 'value_std', 'value_skewness',
            'value_kurtosis', 'trend_coefficient', 'autocorrelation', 'outlier_ratio',
            'spike_frequency', 'sequence_entropy', 'pattern_repetition'
        ]
        
        action_types = ['reward_collection', 'resource_gather', 'level_progress', 'purchase', 'movement', 'combat', 'trade']
        action_features = [f'action_type_{at}' for at in action_types]
        
        hour_features = [f'peak_hour_{h}' for h in range(24)]
        
        return base_features + action_features + hour_features
    
    def save_model(self, filepath: str):
        """모델 저장"""
        model_data = {
            'model': self.model,
            'scaler': self.scaler,
            'feature_names': self.feature_names,
            'is_trained': self.is_trained
        }
        joblib.dump(model_data, filepath)
        logger.info(f"모델이 {filepath}에 저장되었습니다.")
    
    def load_model(self, filepath: str):
        """모델 로드"""
        model_data = joblib.load(filepath)
        self.model = model_data['model']
        self.scaler = model_data['scaler']
        self.feature_names = model_data['feature_names']
        self.is_trained = model_data['is_trained']
        logger.info(f"모델이 {filepath}에서 로드되었습니다.")

class ResourcePredictionModel:
    """회귀 기반 자원 축적 예측 모델 - 잔차 분석으로 미세한 치팅 탐지"""
    
    def __init__(self):
        self.model = Ridge(alpha=1.0)  # 정규화된 선형 회귀
        self.scaler = StandardScaler()
        self.is_trained = False
        self.player_residuals = {}  # 플레이어별 누적 잔차
        self.residual_threshold = 2.0  # 이상 잔차 임계값
        
    def prepare_regression_features(self, player_data: List[Dict[str, Any]]) -> Tuple[np.ndarray, np.ndarray]:
        """회귀 분석을 위한 특징과 타겟 준비"""
        if len(player_data) < 2:
            return np.array([]), np.array([])
        
        features = []
        targets = []
        
        for i in range(1, len(player_data)):
            current = player_data[i]
            previous = player_data[i-1]
            
            # 시간 간격
            time_diff = (pd.to_datetime(current['timestamp']) - pd.to_datetime(previous['timestamp'])).total_seconds()
            
            # 특징: 시간 간격, 이전 값, 행동 유형
            feature_vector = [
                time_diff,
                previous['value'],
                self._encode_action_type(current['action_type']),
                self._encode_action_type(previous['action_type']),
                i  # 시퀀스 위치
            ]
            
            features.append(feature_vector)
            targets.append(current['value'])
        
        return np.array(features), np.array(targets)
    
    def train(self, training_data: List[List[Dict[str, Any]]]) -> Dict[str, float]:
        """회귀 모델 훈련
        
        Args:
            training_data: 각 플레이어의 행동 시퀀스 리스트
        """
        all_features = []
        all_targets = []
        
        for player_sequence in training_data:
            features, targets = self.prepare_regression_features(player_sequence)
            if len(features) > 0:
                all_features.append(features)
                all_targets.append(targets)
        
        if not all_features:
            raise ValueError("훈련 데이터가 없습니다.")
        
        X = np.vstack(all_features)
        y = np.hstack(all_targets)
        
        # 정규화
        X_scaled = self.scaler.fit_transform(X)
        
        # 모델 훈련
        self.model.fit(X_scaled, y)
        self.is_trained = True
        
        # 성능 평가
        y_pred = self.model.predict(X_scaled)
        mse = np.mean((y - y_pred) ** 2)
        r2 = self.model.score(X_scaled, y)
        
        metrics = {
            'mse': mse,
            'rmse': np.sqrt(mse),
            'r2_score': r2
        }
        
        logger.info(f"회귀 모델 훈련 완료: R² {r2:.3f}, RMSE {np.sqrt(mse):.3f}")
        return metrics
    
    def predict_and_analyze(self, player_id: str, player_data: List[Dict[str, Any]]) -> List[RegressionPrediction]:
        """예측 및 잔차 분석"""
        if not self.is_trained:
            raise ValueError("모델이 훈련되지 않았습니다.")
        
        if len(player_data) < 2:
            return []
        
        features, actual_values = self.prepare_regression_features(player_data)
        if len(features) == 0:
            return []
        
        X_scaled = self.scaler.transform(features)
        predicted_values = self.model.predict(X_scaled)
        
        # 잔차 계산
        residuals = actual_values - predicted_values
        
        # 누적 잔차 업데이트
        if player_id not in self.player_residuals:
            self.player_residuals[player_id] = 0.0
        
        results = []
        for i, (pred, actual, residual) in enumerate(zip(predicted_values, actual_values, residuals)):
            self.player_residuals[player_id] += residual
            
            # 이상 점수 계산 (누적 잔차의 절댓값 기반)
            anomaly_score = abs(self.player_residuals[player_id]) / (i + 1)
            
            result = RegressionPrediction(
                player_id=player_id,
                predicted_value=pred,
                actual_value=actual,
                residual=residual,
                cumulative_residual=self.player_residuals[player_id],
                anomaly_score=anomaly_score,
                timestamp=datetime.now()
            )
            
            results.append(result)
        
        return results
    
    def _encode_action_type(self, action_type: str) -> float:
        """행동 유형 인코딩"""
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
    
    def is_anomalous(self, prediction: RegressionPrediction) -> bool:
        """이상치 여부 판단"""
        return abs(prediction.residual) > self.residual_threshold or prediction.anomaly_score > 3.0

class CNNCheatDetector:
    """CNN 기반 시퀀스 패턴 분석 모델"""
    
    def __init__(self, sequence_length: int = 50, feature_dim: int = 5):
        self.sequence_length = sequence_length
        self.feature_dim = feature_dim
        self.model = None
        self.is_trained = False
        
    def build_model(self) -> keras.Model:
        """CNN 모델 구축"""
        model = keras.Sequential([
            # 1D Convolutional layers
            layers.Conv1D(filters=32, kernel_size=3, activation='relu', 
                         input_shape=(self.sequence_length, self.feature_dim)),
            layers.MaxPooling1D(pool_size=2),
            layers.Dropout(0.2),
            
            layers.Conv1D(filters=64, kernel_size=3, activation='relu'),
            layers.MaxPooling1D(pool_size=2),
            layers.Dropout(0.2),
            
            layers.Conv1D(filters=128, kernel_size=3, activation='relu'),
            layers.GlobalMaxPooling1D(),
            
            # Dense layers
            layers.Dense(128, activation='relu'),
            layers.Dropout(0.3),
            layers.Dense(64, activation='relu'),
            layers.Dropout(0.2),
            
            # Output layer
            layers.Dense(1, activation='sigmoid')
        ])
        
        model.compile(
            optimizer='adam',
            loss='binary_crossentropy',
            metrics=['accuracy', 'precision', 'recall']
        )
        
        return model
    
    def train(self, X_sequences: np.ndarray, y_labels: np.ndarray, 
              validation_split: float = 0.2, epochs: int = 50) -> Dict[str, Any]:
        """CNN 모델 훈련"""
        self.model = self.build_model()
        
        # 조기 종료 콜백
        early_stopping = keras.callbacks.EarlyStopping(
            monitor='val_loss',
            patience=10,
            restore_best_weights=True
        )
        
        # 모델 훈련
        history = self.model.fit(
            X_sequences, y_labels,
            validation_split=validation_split,
            epochs=epochs,
            batch_size=32,
            callbacks=[early_stopping],
            verbose=1
        )
        
        self.is_trained = True
        
        # 최종 성능
        final_metrics = {
            'final_accuracy': history.history['accuracy'][-1],
            'final_val_accuracy': history.history['val_accuracy'][-1],
            'final_loss': history.history['loss'][-1],
            'final_val_loss': history.history['val_loss'][-1],
            'training_history': history.history
        }
        
        logger.info(f"CNN 모델 훈련 완료: 검증 정확도 {final_metrics['final_val_accuracy']:.3f}")
        return final_metrics
    
    def predict(self, X_sequence: np.ndarray) -> ModelPrediction:
        """시퀀스에 대한 치팅 확률 예측"""
        if not self.is_trained:
            raise ValueError("모델이 훈련되지 않았습니다.")
        
        if X_sequence.shape != (1, self.sequence_length, self.feature_dim):
            X_sequence = X_sequence.reshape(1, self.sequence_length, self.feature_dim)
        
        prediction = self.model.predict(X_sequence, verbose=0)[0][0]
        
        return ModelPrediction(
            player_id="unknown",
            prediction=float(prediction),
            confidence=abs(prediction - 0.5) * 2,  # 0.5에서 얼마나 떨어져있는지
            model_type="CNN",
            features_used={},
            timestamp=datetime.now()
        )
    
    def save_model(self, filepath: str):
        """모델 저장"""
        if self.model:
            self.model.save(f"{filepath}.h5")
            
            # 메타데이터 저장
            metadata = {
                'sequence_length': self.sequence_length,
                'feature_dim': self.feature_dim,
                'is_trained': self.is_trained
            }
            joblib.dump(metadata, f"{filepath}_metadata.pkl")
            
            logger.info(f"CNN 모델이 {filepath}에 저장되었습니다.")
    
    def load_model(self, filepath: str):
        """모델 로드"""
        self.model = keras.models.load_model(f"{filepath}.h5")
        
        metadata = joblib.load(f"{filepath}_metadata.pkl")
        self.sequence_length = metadata['sequence_length']
        self.feature_dim = metadata['feature_dim'] 
        self.is_trained = metadata['is_trained']
        
        logger.info(f"CNN 모델이 {filepath}에서 로드되었습니다.")

class EnsembleCheatDetector:
    """앙상블 치팅 탐지 시스템 - 여러 모델의 결과를 종합"""
    
    def __init__(self):
        self.rf_model = CheatDetectionRandomForest()
        self.regression_model = ResourcePredictionModel()
        self.cnn_model = CNNCheatDetector()
        self.weights = {'rf': 0.4, 'regression': 0.3, 'cnn': 0.3}
        
    def predict(self, features: PlayerFeatures, player_data: List[Dict[str, Any]], 
                sequence_data: np.ndarray) -> ModelPrediction:
        """앙상블 예측"""
        predictions = {}
        
        # Random Forest 예측
        if self.rf_model.is_trained:
            rf_pred = self.rf_model.predict(features)
            predictions['rf'] = rf_pred.prediction
        
        # 회귀 모델 예측 (이상치 기반)
        if self.regression_model.is_trained and len(player_data) > 1:
            reg_predictions = self.regression_model.predict_and_analyze(features.player_id, player_data)
            if reg_predictions:
                # 최근 예측의 이상 점수를 확률로 변환
                anomaly_score = reg_predictions[-1].anomaly_score
                reg_prob = min(anomaly_score / 5.0, 1.0)  # 정규화
                predictions['regression'] = reg_prob
        
        # CNN 예측
        if self.cnn_model.is_trained:
            cnn_pred = self.cnn_model.predict(sequence_data)
            predictions['cnn'] = cnn_pred.prediction
        
        # 가중 평균 계산
        weighted_sum = 0.0
        total_weight = 0.0
        
        for model_type, prediction in predictions.items():
            weight = self.weights.get(model_type, 0.0)
            weighted_sum += prediction * weight
            total_weight += weight
        
        final_prediction = weighted_sum / total_weight if total_weight > 0 else 0.0
        
        return ModelPrediction(
            player_id=features.player_id,
            prediction=final_prediction,
            confidence=min(total_weight, 1.0),  # 참여 모델 수에 따른 신뢰도
            model_type="Ensemble",
            features_used=predictions,
            timestamp=datetime.now()
        )