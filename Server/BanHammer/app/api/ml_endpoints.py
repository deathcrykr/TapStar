from fastapi import APIRouter, Depends, HTTPException, BackgroundTasks
from sqlalchemy.orm import Session
from typing import Dict, Any, List, Optional
import asyncio
import logging

from ..ml.training_pipeline import ModelTrainingPipeline, MLAntiCheatEngine
from ..ml.models import ModelPrediction
from ..dependencies import get_db
from ..schemas import PlayerActionCreate

logger = logging.getLogger(__name__)

router = APIRouter()

# 글로벌 인스턴스
_training_pipeline = None
_ml_engine = None

def get_training_pipeline() -> ModelTrainingPipeline:
    """훈련 파이프라인 인스턴스 반환"""
    global _training_pipeline
    if _training_pipeline is None:
        _training_pipeline = ModelTrainingPipeline()
    return _training_pipeline

def get_ml_engine() -> MLAntiCheatEngine:
    """ML 엔진 인스턴스 반환"""
    global _ml_engine
    if _ml_engine is None:
        _ml_engine = MLAntiCheatEngine()
    return _ml_engine

@router.post("/train/all")
async def train_all_models(
    retrain: bool = False,
    background_tasks: BackgroundTasks = BackgroundTasks(),
    db: Session = Depends(get_db)
):
    """모든 머신러닝 모델 훈련"""
    pipeline = get_training_pipeline()
    
    # 백그라운드에서 훈련 실행
    async def training_task():
        try:
            logger.info("모델 훈련 시작...")
            results = await pipeline.train_all_models(db, retrain=retrain)
            logger.info(f"모델 훈련 완료: {results}")
            return results
        except Exception as e:
            logger.error(f"모델 훈련 실패: {e}")
            raise
    
    background_tasks.add_task(training_task)
    
    return {
        "message": "모델 훈련이 백그라운드에서 시작되었습니다.",
        "retrain": retrain
    }

@router.get("/models/status")
async def get_model_status():
    """모델 상태 확인"""
    try:
        ml_engine = get_ml_engine()
        status = ml_engine.get_model_status()
        
        return {
            "models": status,
            "ml_engine_available": ml_engine is not None,
            "total_trained": sum(status.values()),
            "training_pipeline_ready": _training_pipeline is not None
        }
    except Exception as e:
        logger.error(f"모델 상태 확인 중 오류: {e}")
        return {
            "error": str(e),
            "models": {},
            "ml_engine_available": False
        }

@router.post("/predict/single")
async def predict_single_action(
    action_data: PlayerActionCreate,
    ml_engine: MLAntiCheatEngine = Depends(get_ml_engine)
):
    """단일 행동에 대한 ML 예측"""
    try:
        action_dict = {
            'player_id': action_data.player_id,
            'action_type': action_data.action_type,
            'timestamp': action_data.timestamp if hasattr(action_data, 'timestamp') else None,
            'value': action_data.value,
            'metadata': action_data.metadata or {}
        }
        
        prediction = await ml_engine.analyze_player_ml(action_data.player_id, action_dict)
        
        if prediction is None:
            return {
                "prediction": None,
                "message": "분석에 충분한 데이터가 없습니다."
            }
        
        return {
            "player_id": prediction.player_id,
            "cheat_probability": prediction.prediction,
            "confidence": prediction.confidence,
            "model_type": prediction.model_type,
            "risk_level": _get_risk_level(prediction.prediction),
            "recommendation": _get_recommendation(prediction.prediction),
            "timestamp": prediction.timestamp.isoformat()
        }
        
    except Exception as e:
        logger.error(f"ML 예측 중 오류: {e}")
        raise HTTPException(status_code=500, detail=f"예측 실패: {str(e)}")

@router.get("/analytics/model-performance")
async def get_model_performance():
    """모델 성능 분석"""
    try:
        import json
        from pathlib import Path
        
        # 훈련 결과 로드
        results_path = Path("models/training_results.json")
        if results_path.exists():
            with open(results_path, 'r') as f:
                training_results = json.load(f)
            
            return {
                "last_training": training_results.get('training_timestamp'),
                "models_performance": {
                    "random_forest": {
                        "accuracy": training_results.get('random_forest', {}).get('accuracy'),
                        "precision": training_results.get('random_forest', {}).get('precision'),
                        "recall": training_results.get('random_forest', {}).get('recall'),
                        "f1_score": training_results.get('random_forest', {}).get('f1_score')
                    },
                    "regression": {
                        "r2_score": training_results.get('regression', {}).get('r2_score'),
                        "rmse": training_results.get('regression', {}).get('rmse')
                    },
                    "cnn": {
                        "final_accuracy": training_results.get('cnn', {}).get('final_accuracy'),
                        "final_val_accuracy": training_results.get('cnn', {}).get('final_val_accuracy')
                    }
                }
            }
        else:
            return {
                "message": "훈련 결과가 없습니다. 먼저 모델을 훈련하세요.",
                "models_performance": {}
            }
            
    except Exception as e:
        logger.error(f"성능 분석 중 오류: {e}")
        raise HTTPException(status_code=500, detail=f"성능 분석 실패: {str(e)}")

@router.get("/analytics/feature-importance")
async def get_feature_importance():
    """특징 중요도 분석"""
    try:
        pipeline = get_training_pipeline()
        
        if not pipeline.rf_model.is_trained:
            raise HTTPException(status_code=404, detail="Random Forest 모델이 훈련되지 않았습니다.")
        
        # 특징 중요도 추출
        feature_importance = dict(zip(
            pipeline.rf_model.feature_names,
            pipeline.rf_model.model.feature_importances_
        ))
        
        # 중요도 순으로 정렬
        sorted_features = sorted(feature_importance.items(), key=lambda x: x[1], reverse=True)
        
        return {
            "feature_importance": dict(sorted_features[:20]),  # 상위 20개
            "total_features": len(feature_importance),
            "model_type": "Random Forest"
        }
        
    except Exception as e:
        logger.error(f"특징 중요도 분석 중 오류: {e}")
        raise HTTPException(status_code=500, detail=f"특징 중요도 분석 실패: {str(e)}")

@router.post("/train/random-forest")
async def train_random_forest_only(
    background_tasks: BackgroundTasks,
    db: Session = Depends(get_db)
):
    """Random Forest 모델만 훈련"""
    pipeline = get_training_pipeline()
    
    async def rf_training_task():
        try:
            from ..ml.training_pipeline import TrainingDataGenerator
            
            data_generator = TrainingDataGenerator(db)
            labeled_data = data_generator.generate_labeled_data()
            
            if len(labeled_data) < 100:
                logger.warning("Random Forest 훈련에 충분한 데이터가 없습니다.")
                return {"error": "훈련 데이터 부족"}
            
            metrics = pipeline.rf_model.train(labeled_data)
            pipeline.rf_model.save_model("models/random_forest.pkl")
            
            logger.info(f"Random Forest 훈련 완료: {metrics}")
            return metrics
        except Exception as e:
            logger.error(f"Random Forest 훈련 실패: {e}")
            return {"error": str(e)}
    
    background_tasks.add_task(rf_training_task)
    
    return {"message": "Random Forest 모델 훈련이 시작되었습니다."}

@router.get("/debug/player-features/{player_id}")
async def get_player_features(
    player_id: str,
    ml_engine: MLAntiCheatEngine = Depends(get_ml_engine)
):
    """플레이어의 현재 특징 확인 (디버깅용)"""
    try:
        features = ml_engine.feature_buffer.get_features(player_id)
        
        if features is None:
            raise HTTPException(status_code=404, detail="플레이어 데이터가 없습니다.")
        
        # PlayerFeatures를 딕셔너리로 변환
        from dataclasses import asdict
        features_dict = asdict(features)
        
        return {
            "player_id": player_id,
            "features": features_dict,
            "recent_actions_count": len(ml_engine.feature_buffer.player_buffers.get(player_id, [])),
            "buffer_status": "active" if player_id in ml_engine.feature_buffer.player_buffers else "inactive"
        }
        
    except Exception as e:
        logger.error(f"플레이어 특징 조회 중 오류: {e}")
        raise HTTPException(status_code=500, detail=f"특징 조회 실패: {str(e)}")

def _get_risk_level(probability: float) -> str:
    """확률을 위험도 레벨로 변환"""
    if probability >= 0.8:
        return "매우 높음"
    elif probability >= 0.6:
        return "높음"
    elif probability >= 0.4:
        return "보통"
    elif probability >= 0.2:
        return "낮음"
    else:
        return "매우 낮음"

def _get_recommendation(probability: float) -> str:
    """확률에 따른 권장 조치"""
    if probability >= 0.8:
        return "즉시 차단 검토 필요"
    elif probability >= 0.6:
        return "주의 깊은 모니터링 필요"
    elif probability >= 0.4:
        return "정기적인 관찰 권장"
    else:
        return "정상적인 플레이어로 판단"