import numpy as np
import pandas as pd
from typing import List, Dict, Tuple, Any, Optional
import asyncio
import logging
from datetime import datetime, timedelta
from pathlib import Path
import json
from dataclasses import asdict

from .models import (
    CheatDetectionRandomForest, ResourcePredictionModel, CNNCheatDetector,
    EnsembleCheatDetector, ModelPrediction
)
from .feature_engineering import (
    FeatureExtractor, AdvancedFeatureExtractor, PlayerFeatures, RealTimeFeatureBuffer
)
from ..dependencies import get_db
from ..models.database import Player, PlayerAction, Violation
from sqlalchemy.orm import Session

logger = logging.getLogger(__name__)

class TrainingDataGenerator:
    """훈련 데이터 생성기 - 데이터베이스에서 라벨링된 데이터를 생성"""
    
    def __init__(self, db: Session):
        self.db = db
        self.feature_extractor = FeatureExtractor()
        self.advanced_extractor = AdvancedFeatureExtractor()
    
    def generate_labeled_data(self, days_back: int = 30) -> List[Tuple[PlayerFeatures, int]]:
        """라벨링된 훈련 데이터 생성
        
        Args:
            days_back: 며칠 전까지의 데이터를 사용할지
            
        Returns:
            (특징, 라벨) 튜플의 리스트. 라벨: 0=정상, 1=치팅
        """
        cutoff_date = datetime.now() - timedelta(days=days_back)
        
        # 모든 플레이어 조회
        players = self.db.query(Player).filter(Player.created_at >= cutoff_date).all()
        
        labeled_data = []
        
        for player in players:
            try:
                # 플레이어의 행동 데이터 조회
                actions = self.db.query(PlayerAction).filter(
                    PlayerAction.player_id == player.id,
                    PlayerAction.timestamp >= cutoff_date
                ).order_by(PlayerAction.timestamp).all()
                
                if len(actions) < 10:  # 최소 10개 행동 필요
                    continue
                
                # 행동 데이터를 딕셔너리로 변환
                action_data = []
                for action in actions:
                    action_dict = {
                        'player_id': action.player_id,
                        'action_type': action.action_type,
                        'timestamp': action.timestamp,
                        'value': action.value,
                        'metadata': action.get_metadata()
                    }
                    action_data.append(action_dict)
                
                # 특징 추출
                features = self.feature_extractor.extract_features(action_data)
                
                # 라벨 결정 (차단되었거나 고위험 점수면 치팅으로 간주)
                label = self._determine_label(player)
                
                labeled_data.append((features, label))
                
            except Exception as e:
                logger.warning(f"플레이어 {player.id} 데이터 처리 중 오류: {e}")
                continue
        
        logger.info(f"라벨링된 데이터 {len(labeled_data)}개 생성 완료")
        return labeled_data
    
    def generate_regression_data(self, days_back: int = 30) -> List[List[Dict[str, Any]]]:
        """회귀 모델을 위한 시계열 데이터 생성"""
        cutoff_date = datetime.now() - timedelta(days=days_back)
        
        # 정상 플레이어들의 데이터만 사용 (회귀 모델 훈련용)
        normal_players = self.db.query(Player).filter(
            Player.created_at >= cutoff_date,
            Player.is_banned == False,
            Player.risk_score < 3.0  # 낮은 위험도
        ).all()
        
        regression_data = []
        
        for player in normal_players:
            actions = self.db.query(PlayerAction).filter(
                PlayerAction.player_id == player.id,
                PlayerAction.timestamp >= cutoff_date,
                PlayerAction.value > 0  # 값이 있는 행동만
            ).order_by(PlayerAction.timestamp).all()
            
            if len(actions) < 5:
                continue
            
            action_sequence = []
            for action in actions:
                action_dict = {
                    'player_id': action.player_id,
                    'action_type': action.action_type,
                    'timestamp': action.timestamp,
                    'value': action.value,
                    'metadata': action.get_metadata()
                }
                action_sequence.append(action_dict)
            
            regression_data.append(action_sequence)
        
        logger.info(f"회귀 데이터 {len(regression_data)}개 시퀀스 생성 완료")
        return regression_data
    
    def generate_cnn_data(self, days_back: int = 30, sequence_length: int = 50) -> Tuple[np.ndarray, np.ndarray]:
        """CNN을 위한 시퀀스 데이터 생성"""
        cutoff_date = datetime.now() - timedelta(days=days_back)
        
        players = self.db.query(Player).filter(Player.created_at >= cutoff_date).all()
        
        sequences = []
        labels = []
        
        for player in players:
            actions = self.db.query(PlayerAction).filter(
                PlayerAction.player_id == player.id,
                PlayerAction.timestamp >= cutoff_date
            ).order_by(PlayerAction.timestamp).all()
            
            if len(actions) < sequence_length:
                continue
            
            # 행동 데이터를 딕셔너리로 변환
            action_data = []
            for action in actions:
                action_dict = {
                    'player_id': action.player_id,
                    'action_type': action.action_type,
                    'timestamp': action.timestamp,
                    'value': action.value,
                    'metadata': action.get_metadata()
                }
                action_data.append(action_dict)
            
            # CNN 특징 추출
            cnn_features = self.advanced_extractor.extract_cnn_features(action_data, sequence_length)
            sequences.append(cnn_features)
            
            # 라벨 결정
            label = self._determine_label(player)
            labels.append(label)
        
        X = np.array(sequences)
        y = np.array(labels)
        
        logger.info(f"CNN 데이터 생성 완료: {X.shape}, 라벨 분포: {np.bincount(y)}")
        return X, y
    
    def _determine_label(self, player: Player) -> int:
        """플레이어의 라벨 결정 (0: 정상, 1: 치팅)"""
        # 명시적으로 차단된 경우
        if player.is_banned:
            return 1
        
        # 고위험 점수
        if player.risk_score > 6.0:
            return 1
        
        # 심각한 위반 기록 확인
        severe_violations = self.db.query(Violation).filter(
            Violation.player_id == player.id,
            Violation.severity >= 4.0
        ).count()
        
        if severe_violations >= 3:
            return 1
        
        return 0

class ModelTrainingPipeline:
    """모델 훈련 파이프라인"""
    
    def __init__(self, model_save_dir: str = "models"):
        self.model_save_dir = Path(model_save_dir)
        self.model_save_dir.mkdir(exist_ok=True)
        
        self.rf_model = CheatDetectionRandomForest()
        self.regression_model = ResourcePredictionModel()
        self.cnn_model = CNNCheatDetector()
        self.ensemble_model = EnsembleCheatDetector()
        
    async def train_all_models(self, db: Session, retrain: bool = False) -> Dict[str, Any]:
        """모든 모델 훈련"""
        results = {}
        
        # 기존 모델 로드 (재훈련이 아닌 경우)
        if not retrain:
            self._load_existing_models()
        
        # 훈련 데이터 생성
        logger.info("훈련 데이터 생성 시작...")
        data_generator = TrainingDataGenerator(db)
        
        # Random Forest 훈련
        if retrain or not self.rf_model.is_trained:
            logger.info("Random Forest 모델 훈련 시작...")
            labeled_data = data_generator.generate_labeled_data()
            
            if len(labeled_data) >= 100:  # 최소 훈련 데이터 필요
                rf_metrics = self.rf_model.train(labeled_data)
                results['random_forest'] = rf_metrics
                
                # 모델 저장
                self.rf_model.save_model(str(self.model_save_dir / "random_forest.pkl"))
            else:
                logger.warning("Random Forest 훈련에 충분한 데이터가 없습니다.")
        
        # 회귀 모델 훈련
        if retrain or not self.regression_model.is_trained:
            logger.info("회귀 모델 훈련 시작...")
            regression_data = data_generator.generate_regression_data()
            
            if len(regression_data) >= 50:
                reg_metrics = self.regression_model.train(regression_data)
                results['regression'] = reg_metrics
            else:
                logger.warning("회귀 모델 훈련에 충분한 데이터가 없습니다.")
        
        # CNN 모델 훈련
        if retrain or not self.cnn_model.is_trained:
            logger.info("CNN 모델 훈련 시작...")
            X_cnn, y_cnn = data_generator.generate_cnn_data()
            
            if len(X_cnn) >= 200:  # 딥러닝은 더 많은 데이터 필요
                cnn_metrics = self.cnn_model.train(X_cnn, y_cnn)
                results['cnn'] = cnn_metrics
                
                # 모델 저장
                self.cnn_model.save_model(str(self.model_save_dir / "cnn_model"))
            else:
                logger.warning("CNN 모델 훈련에 충분한 데이터가 없습니다.")
        
        # 앙상블 모델 설정
        self.ensemble_model.rf_model = self.rf_model
        self.ensemble_model.regression_model = self.regression_model
        self.ensemble_model.cnn_model = self.cnn_model
        
        # 훈련 결과 저장
        results['training_timestamp'] = datetime.now().isoformat()
        results['models_trained'] = list(results.keys())
        
        with open(self.model_save_dir / "training_results.json", "w") as f:
            json.dump(results, f, indent=2, default=str)
        
        logger.info(f"모델 훈련 완료. 결과: {results}")
        return results
    
    def _load_existing_models(self):
        """기존 모델 로드"""
        try:
            rf_path = self.model_save_dir / "random_forest.pkl"
            if rf_path.exists():
                self.rf_model.load_model(str(rf_path))
                logger.info("Random Forest 모델 로드됨")
        except Exception as e:
            logger.warning(f"Random Forest 모델 로드 실패: {e}")
        
        try:
            cnn_path = self.model_save_dir / "cnn_model"
            if (self.model_save_dir / "cnn_model.h5").exists():
                self.cnn_model.load_model(str(cnn_path))
                logger.info("CNN 모델 로드됨")
        except Exception as e:
            logger.warning(f"CNN 모델 로드 실패: {e}")

class MLAntiCheatEngine:
    """머신러닝 기반 치팅 탐지 엔진"""
    
    def __init__(self, model_dir: str = "models"):
        self.model_dir = Path(model_dir)
        self.ensemble_model = EnsembleCheatDetector()
        self.feature_buffer = RealTimeFeatureBuffer()
        self.feature_extractor = FeatureExtractor()
        self.advanced_extractor = AdvancedFeatureExtractor()
        
        # 모델 로드
        self._load_models()
    
    def _load_models(self):
        """저장된 모델들 로드"""
        try:
            rf_path = self.model_dir / "random_forest.pkl"
            if rf_path.exists():
                self.ensemble_model.rf_model.load_model(str(rf_path))
        except Exception as e:
            logger.warning(f"Random Forest 모델 로드 실패: {e}")
        
        try:
            cnn_path = self.model_dir / "cnn_model"
            if (self.model_dir / "cnn_model.h5").exists():
                self.ensemble_model.cnn_model.load_model(str(cnn_path))
        except Exception as e:
            logger.warning(f"CNN 모델 로드 실패: {e}")
    
    async def analyze_player_ml(self, player_id: str, action_data: Dict[str, Any]) -> Optional[ModelPrediction]:
        """머신러닝 기반 플레이어 분석"""
        # 행동 데이터를 버퍼에 추가
        self.feature_buffer.add_action(player_id, action_data)
        
        # 특징 추출
        features = self.feature_buffer.get_features(player_id)
        if not features:
            return None
        
        # 플레이어 데이터 가져오기
        player_data = list(self.feature_buffer.player_buffers[player_id])
        
        # CNN용 시퀀스 데이터 생성
        sequence_data = self.advanced_extractor.extract_cnn_features(player_data)
        
        # 앙상블 예측
        try:
            prediction = self.ensemble_model.predict(features, player_data, sequence_data)
            return prediction
        except Exception as e:
            logger.error(f"ML 예측 중 오류: {e}")
            return None
    
    def get_model_status(self) -> Dict[str, bool]:
        """모델 상태 확인"""
        return {
            'random_forest': self.ensemble_model.rf_model.is_trained,
            'regression': self.ensemble_model.regression_model.is_trained,
            'cnn': self.ensemble_model.cnn_model.is_trained
        }

class AutoMLTrainer:
    """자동 모델 재훈련 시스템"""
    
    def __init__(self, training_pipeline: ModelTrainingPipeline):
        self.pipeline = training_pipeline
        self.last_training = None
        self.training_interval = timedelta(days=7)  # 주간 재훈련
        
    async def should_retrain(self, db: Session) -> bool:
        """재훈련 필요 여부 판단"""
        # 시간 기반 재훈련
        if self.last_training is None or datetime.now() - self.last_training > self.training_interval:
            return True
        
        # 성능 기반 재훈련 (새로운 위반 사례가 많은 경우)
        recent_violations = db.query(Violation).filter(
            Violation.timestamp >= datetime.now() - timedelta(days=1)
        ).count()
        
        if recent_violations > 100:  # 하루에 100개 이상 위반
            return True
        
        return False
    
    async def auto_train(self, db: Session):
        """자동 훈련 실행"""
        if await self.should_retrain(db):
            logger.info("자동 재훈련 시작...")
            try:
                results = await self.pipeline.train_all_models(db, retrain=True)
                self.last_training = datetime.now()
                logger.info(f"자동 재훈련 완료: {results}")
            except Exception as e:
                logger.error(f"자동 재훈련 실패: {e}")

async def setup_ml_training_task():
    """ML 훈련 백그라운드 태스크 설정"""
    pipeline = ModelTrainingPipeline()
    auto_trainer = AutoMLTrainer(pipeline)
    
    while True:
        try:
            # 매일 자동 훈련 체크
            from ..dependencies import get_db
            db = next(get_db())
            await auto_trainer.auto_train(db)
            db.close()
            
        except Exception as e:
            logger.error(f"ML 훈련 태스크 오류: {e}")
        
        # 24시간 대기
        await asyncio.sleep(86400)