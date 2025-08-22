#!/usr/bin/env python3
"""
BanHammer 머신러닝 치팅 탐지 시스템 고급 예제

이 파일은 랜덤 포레스트, 회귀 모델, CNN을 활용한 
고도화된 치팅 탐지 기능을 시연합니다.
"""

import asyncio
import aiohttp
import time
import random
import numpy as np
from datetime import datetime, timedelta
import json

API_BASE_URL = "http://localhost:8000/api"
ML_API_BASE_URL = "http://localhost:8000/api/ml"

class AdvancedBanHammerClient:
    def __init__(self, base_url: str = API_BASE_URL, ml_url: str = ML_API_BASE_URL):
        self.base_url = base_url
        self.ml_url = ml_url
    
    async def submit_action_with_ml(self, player_id: str, action_type: str, value: float = 0.0,
                                  username: str = None, metadata: dict = None):
        """행동 제출과 동시에 ML 예측 수행"""
        # 기본 행동 제출
        action_data = {
            "player_id": player_id,
            "action_type": action_type,
            "value": value,
            "username": username,
            "metadata": metadata or {}
        }
        
        async with aiohttp.ClientSession() as session:
            # 1. 기본 치팅 탐지
            async with session.post(f"{self.base_url}/action", json=action_data) as response:
                basic_result = await response.json()
            
            # 2. ML 기반 예측
            try:
                async with session.post(f"{self.ml_url}/predict/single", json=action_data) as response:
                    ml_result = await response.json() if response.status == 200 else {}
            except:
                ml_result = {}
        
        return {
            "basic_detection": basic_result,
            "ml_prediction": ml_result
        }
    
    async def train_models(self, retrain: bool = False):
        """모델 훈련 시작"""
        async with aiohttp.ClientSession() as session:
            params = {"retrain": retrain}
            async with session.post(f"{self.ml_url}/train/all", params=params) as response:
                return await response.json()
    
    async def get_model_status(self):
        """모델 상태 확인"""
        async with aiohttp.ClientSession() as session:
            async with session.get(f"{self.ml_url}/models/status") as response:
                return await response.json()
    
    async def get_model_performance(self):
        """모델 성능 확인"""
        async with aiohttp.ClientSession() as session:
            async with session.get(f"{self.ml_url}/analytics/model-performance") as response:
                return await response.json()
    
    async def get_feature_importance(self):
        """특징 중요도 확인"""
        async with aiohttp.ClientSession() as session:
            async with session.get(f"{self.ml_url}/analytics/feature-importance") as response:
                return await response.json()

async def simulate_sophisticated_cheat_patterns(client: AdvancedBanHammerClient):
    """정교한 치팅 패턴 시뮬레이션 - ML이 탐지해야 하는 케이스들"""
    print("🤖 정교한 치팅 패턴 시뮬레이션 시작...")
    
    # 패턴 1: 미세한 자원 축적 이상 (회귀 모델이 탐지해야 함)
    print("\n📊 패턴 1: 미세한 자원 축적 이상")
    player_id = "subtle_cheater_001"
    
    # 정상적인 패턴으로 시작
    base_values = [10, 12, 11, 13, 12, 14, 13, 15, 14, 16]  # 점진적 증가
    
    for i, base_value in enumerate(base_values):
        # 미세하게 비정상적인 증가 (3-5% 더 높음)
        adjusted_value = base_value * random.uniform(1.03, 1.05)
        
        result = await client.submit_action_with_ml(
            player_id=player_id,
            action_type="resource_gather",
            value=adjusted_value,
            username="subtle_cheater"
        )
        
        ml_pred = result.get("ml_prediction", {})
        print(f"  자원 수집 #{i+1}: 값={adjusted_value:.1f}, "
              f"ML 확률={ml_pred.get('cheat_probability', 0):.3f}, "
              f"위험도={ml_pred.get('risk_level', 'N/A')}")
        
        await asyncio.sleep(0.8)
    
    # 패턴 2: 시간 기반 패턴 이상 (CNN이 탐지해야 함)
    print("\n🧠 패턴 2: 복잡한 시퀀스 패턴 이상")
    player_id = "pattern_cheater_001"
    
    # 의심스러운 반복 패턴: A-B-C-A-B-C...
    pattern_sequence = ["reward_collection", "resource_gather", "level_progress"]
    
    for i in range(15):  # 5번 반복
        action_type = pattern_sequence[i % len(pattern_sequence)]
        
        result = await client.submit_action_with_ml(
            player_id=player_id,
            action_type=action_type,
            value=random.uniform(20, 30),
            username="pattern_cheater"
        )
        
        ml_pred = result.get("ml_prediction", {})
        print(f"  시퀀스 #{i+1}: {action_type}, "
              f"ML 확률={ml_pred.get('cheat_probability', 0):.3f}")
        
        await asyncio.sleep(1.2)  # 일정한 간격
    
    # 패턴 3: 다차원 이상 행동 (랜덤 포레스트가 탐지해야 함)
    print("\n🌳 패턴 3: 다차원 행동 이상")
    player_id = "multidim_cheater_001"
    
    # 여러 특징에서 동시에 이상: 높은 값 + 일정한 간격 + 특정 시간대
    for i in range(12):
        result = await client.submit_action_with_ml(
            player_id=player_id,
            action_type="reward_collection",
            value=random.uniform(80, 120),  # 높은 값
            username="multidim_cheater",
            metadata={
                "location": "premium_zone",  # 특별한 위치
                "method": "automated_script"  # 의심스러운 메타데이터
            }
        )
        
        ml_pred = result.get("ml_prediction", {})
        basic_det = result.get("basic_detection", {})
        
        print(f"  복합 행동 #{i+1}: 기본 위반={basic_det.get('violations_detected', 0)}, "
              f"ML 확률={ml_pred.get('cheat_probability', 0):.3f}, "
              f"추천={ml_pred.get('recommendation', 'N/A')}")
        
        await asyncio.sleep(2.0)  # 정확한 간격

async def demonstrate_ml_analytics(client: AdvancedBanHammerClient):
    """ML 분석 기능 시연"""
    print("\n📈 ML 분석 기능 시연")
    
    # 모델 상태 확인
    print("\n🔍 모델 상태:")
    model_status = await client.get_model_status()
    for model_name, is_trained in model_status.get("models", {}).items():
        status = "✅ 훈련됨" if is_trained else "❌ 미훈련"
        print(f"  {model_name}: {status}")
    
    print(f"\nML 엔진 사용 가능: {'✅' if model_status.get('ml_engine_available') else '❌'}")
    print(f"훈련된 모델 수: {model_status.get('total_trained', 0)}")
    
    # 모델 성능 확인
    print("\n📊 모델 성능:")
    try:
        performance = await client.get_model_performance()
        rf_perf = performance.get("models_performance", {}).get("random_forest", {})
        
        if rf_perf.get("accuracy"):
            print(f"  Random Forest 정확도: {rf_perf['accuracy']:.3f}")
            print(f"  Random Forest 정밀도: {rf_perf['precision']:.3f}")
            print(f"  Random Forest 재현율: {rf_perf['recall']:.3f}")
        
        reg_perf = performance.get("models_performance", {}).get("regression", {})
        if reg_perf.get("r2_score"):
            print(f"  회귀 모델 R² 점수: {reg_perf['r2_score']:.3f}")
        
        cnn_perf = performance.get("models_performance", {}).get("cnn", {})
        if cnn_perf.get("final_accuracy"):
            print(f"  CNN 정확도: {cnn_perf['final_accuracy']:.3f}")
    except:
        print("  모델 성능 데이터 없음 (모델 훈련 필요)")
    
    # 특징 중요도 확인
    print("\n🎯 특징 중요도 (상위 10개):")
    try:
        importance = await client.get_feature_importance()
        feature_importance = importance.get("feature_importance", {})
        
        for i, (feature, importance_score) in enumerate(list(feature_importance.items())[:10]):
            print(f"  {i+1:2d}. {feature:<25}: {importance_score:.4f}")
    except:
        print("  특징 중요도 데이터 없음 (Random Forest 훈련 필요)")

async def compare_detection_methods(client: AdvancedBanHammerClient):
    """기존 방식과 ML 방식 비교"""
    print("\n⚖️  탐지 방식 비교")
    
    test_cases = [
        {
            "name": "명백한 속도 위반",
            "player_id": "obvious_cheater",
            "actions": [
                {"type": "reward_collection", "value": 200, "interval": 0.1}
            ] * 15
        },
        {
            "name": "미묘한 패턴 이상",
            "player_id": "subtle_cheater",
            "actions": [
                {"type": "resource_gather", "value": v * 1.03, "interval": 2.0}
                for v in [20, 22, 21, 23, 22, 24, 23, 25]
            ]
        }
    ]
    
    for case in test_cases:
        print(f"\n📋 테스트 케이스: {case['name']}")
        
        basic_violations = 0
        ml_high_risk = 0
        
        for i, action in enumerate(case['actions']):
            result = await client.submit_action_with_ml(
                player_id=case['player_id'],
                action_type=action['type'],
                value=action['value'],
                username=case['player_id']
            )
            
            # 기존 방식 위반 카운트
            basic_violations += result['basic_detection'].get('violations_detected', 0)
            
            # ML 방식 고위험 판정 카운트
            ml_prob = result['ml_prediction'].get('cheat_probability', 0)
            if ml_prob > 0.6:
                ml_high_risk += 1
            
            await asyncio.sleep(action['interval'])
        
        print(f"  기존 방식 위반: {basic_violations}회")
        print(f"  ML 고위험 판정: {ml_high_risk}회")
        
        # 효과성 평가
        if basic_violations > 0 and ml_high_risk > 0:
            print("  ✅ 두 방식 모두 탐지")
        elif basic_violations > 0:
            print("  🔵 기존 방식만 탐지")
        elif ml_high_risk > 0:
            print("  🟡 ML 방식만 탐지")
        else:
            print("  ⚪ 탐지 없음")

async def main():
    """메인 데모 함수"""
    print("🚀 BanHammer ML 고급 치팅 탐지 시스템 데모")
    print("=" * 60)
    
    client = AdvancedBanHammerClient()
    
    try:
        # API 상태 확인
        async with aiohttp.ClientSession() as session:
            async with session.get("http://localhost:8000/health") as response:
                health = await response.json()
                print(f"API 상태: {health['status']}")
        
        # ML 분석 기능 먼저 시연
        await demonstrate_ml_analytics(client)
        
        # 모델 훈련 (필요시)
        model_status = await client.get_model_status()
        if model_status.get('total_trained', 0) == 0:
            print("\n🏋️  모델 훈련이 필요합니다. 백그라운드에서 시작...")
            await client.train_models()
            await asyncio.sleep(5)  # 훈련 시작 대기
        
        # 정교한 치팅 패턴 시뮬레이션
        await simulate_sophisticated_cheat_patterns(client)
        
        # 탐지 방식 비교
        await compare_detection_methods(client)
        
        print("\n" + "=" * 60)
        print("🎉 ML 고급 치팅 탐지 데모 완료!")
        print("\n📋 주요 기능:")
        print("  🌳 Random Forest: 다차원 행동 패턴 분석 (95% 정확도 목표)")
        print("  📈 회귀 모델: 자원 축적 예측 및 잔차 분석")
        print("  🧠 CNN: 복잡한 시퀀스 패턴 탐지")
        print("  🤝 앙상블: 여러 모델 결과 종합")
        print("\n🔗 추가 엔드포인트:")
        print("  • /api/ml/train/all - 모든 모델 훈련")
        print("  • /api/ml/models/status - 모델 상태")
        print("  • /api/ml/analytics/model-performance - 성능 분석")
        print("  • /api/ml/analytics/feature-importance - 특징 중요도")
        print("  • /docs - 전체 API 문서")
        
    except aiohttp.ClientError as e:
        print(f"❌ API 서버에 연결할 수 없습니다: {e}")
        print("먼저 다음 명령으로 서버를 시작하세요:")
        print("  python main.py")
    except Exception as e:
        print(f"❌ 예상치 못한 오류: {e}")

if __name__ == "__main__":
    asyncio.run(main())