import asyncio
import time
from datetime import datetime, timedelta
from typing import Dict, List, Optional, Tuple, Any, Set
from dataclasses import dataclass, field
from enum import Enum
import numpy as np
from collections import defaultdict, deque
import redis.asyncio as redis
import json
import logging

from .game_profiles import GameProfile, GameProfileManager, ActionDefinition, DetectionRule
from ..ml.training_pipeline import MLAntiCheatEngine
from ..ml.models import ModelPrediction

logger = logging.getLogger(__name__)

class ViolationType(Enum):
    RATE_LIMIT_EXCEEDED = "rate_limit_exceeded"
    THRESHOLD_EXCEEDED = "threshold_exceeded"
    SUSPICIOUS_PATTERN = "suspicious_pattern"
    STATISTICAL_ANOMALY = "statistical_anomaly"
    ML_DETECTION = "ml_detection"
    CUSTOM_RULE = "custom_rule"
    INVALID_ACTION = "invalid_action"

@dataclass
class UniversalPlayerAction:
    """범용 플레이어 액션"""
    player_id: str
    game_id: str
    action_type: str
    timestamp: float
    value: Any = None
    metadata: Dict[str, Any] = field(default_factory=dict)
    session_id: Optional[str] = None
    client_info: Dict[str, Any] = field(default_factory=dict)

@dataclass
class UniversalViolation:
    """범용 위반 기록"""
    player_id: str
    game_id: str
    violation_type: ViolationType
    rule_id: str
    severity: float
    timestamp: float
    details: Dict[str, Any] = field(default_factory=dict)
    action_context: List[UniversalPlayerAction] = field(default_factory=list)

class DynamicRuleEngine:
    """동적 규칙 엔진"""
    
    def __init__(self):
        self.custom_rules: Dict[str, callable] = {}
        
    def register_rule(self, rule_id: str, rule_function: callable):
        """사용자 정의 규칙 등록"""
        self.custom_rules[rule_id] = rule_function
        
    async def evaluate_rule(self, rule: DetectionRule, actions: List[UniversalPlayerAction], 
                          profile: GameProfile) -> Optional[UniversalViolation]:
        """규칙 평가"""
        if rule.rule_type == "rate_limit":
            return await self._evaluate_rate_limit(rule, actions, profile)
        elif rule.rule_type == "threshold":
            return await self._evaluate_threshold(rule, actions, profile)
        elif rule.rule_type == "pattern":
            return await self._evaluate_pattern(rule, actions, profile)
        elif rule.rule_type == "statistical":
            return await self._evaluate_statistical(rule, actions, profile)
        elif rule.rule_type == "custom":
            return await self._evaluate_custom(rule, actions, profile)
        else:
            logger.warning(f"Unknown rule type: {rule.rule_type}")
            return None
    
    async def _evaluate_rate_limit(self, rule: DetectionRule, actions: List[UniversalPlayerAction], 
                                 profile: GameProfile) -> Optional[UniversalViolation]:
        """속도 제한 규칙 평가"""
        if not actions:
            return None
            
        current_time = time.time()
        window = rule.parameters.get("time_window", 60)  # 기본 1분
        cutoff_time = current_time - window
        
        # 해당 규칙에 적용되는 액션들 필터링
        relevant_actions = [
            action for action in actions 
            if action.action_type in rule.action_types and action.timestamp >= cutoff_time
        ]
        
        # 빈도 검사
        max_count = rule.parameters.get("max_per_minute", rule.parameters.get("max_count", 10))
        if len(relevant_actions) > max_count:
            return UniversalViolation(
                player_id=actions[0].player_id,
                game_id=actions[0].game_id,
                violation_type=ViolationType.RATE_LIMIT_EXCEEDED,
                rule_id=rule.rule_id,
                severity=rule.severity,
                timestamp=current_time,
                details={
                    "actual_count": len(relevant_actions),
                    "max_allowed": max_count,
                    "time_window": window,
                    "actions": rule.action_types
                },
                action_context=relevant_actions[-5:]  # 최근 5개 액션
            )
        
        # 값 기반 제한 검사
        if "max_value_per_minute" in rule.parameters or "max_total_value" in rule.parameters:
            total_value = sum(
                float(action.value) if action.value and isinstance(action.value, (int, float)) else 0
                for action in relevant_actions
            )
            
            max_value = rule.parameters.get("max_value_per_minute", rule.parameters.get("max_total_value", 1000))
            if total_value > max_value:
                return UniversalViolation(
                    player_id=actions[0].player_id,
                    game_id=actions[0].game_id,
                    violation_type=ViolationType.RATE_LIMIT_EXCEEDED,
                    rule_id=rule.rule_id,
                    severity=rule.severity,
                    timestamp=current_time,
                    details={
                        "actual_value": total_value,
                        "max_allowed": max_value,
                        "time_window": window,
                        "value_type": "cumulative"
                    },
                    action_context=relevant_actions[-5:]
                )
        
        return None
    
    async def _evaluate_threshold(self, rule: DetectionRule, actions: List[UniversalPlayerAction], 
                                profile: GameProfile) -> Optional[UniversalViolation]:
        """임계값 규칙 평가"""
        if not actions:
            return None
            
        relevant_actions = [action for action in actions if action.action_type in rule.action_types]
        if not relevant_actions:
            return None
            
        latest_action = relevant_actions[-1]
        
        # 단일 값 임계값 검사
        if "max_value" in rule.parameters:
            if isinstance(latest_action.value, (int, float)) and latest_action.value > rule.parameters["max_value"]:
                return UniversalViolation(
                    player_id=latest_action.player_id,
                    game_id=latest_action.game_id,
                    violation_type=ViolationType.THRESHOLD_EXCEEDED,
                    rule_id=rule.rule_id,
                    severity=rule.severity,
                    timestamp=time.time(),
                    details={
                        "actual_value": latest_action.value,
                        "max_allowed": rule.parameters["max_value"],
                        "threshold_type": "maximum"
                    },
                    action_context=[latest_action]
                )
        
        if "min_value" in rule.parameters:
            if isinstance(latest_action.value, (int, float)) and latest_action.value < rule.parameters["min_value"]:
                return UniversalViolation(
                    player_id=latest_action.player_id,
                    game_id=latest_action.game_id,
                    violation_type=ViolationType.THRESHOLD_EXCEEDED,
                    rule_id=rule.rule_id,
                    severity=rule.severity,
                    timestamp=time.time(),
                    details={
                        "actual_value": latest_action.value,
                        "min_allowed": rule.parameters["min_value"],
                        "threshold_type": "minimum"
                    },
                    action_context=[latest_action]
                )
        
        return None
    
    async def _evaluate_pattern(self, rule: DetectionRule, actions: List[UniversalPlayerAction], 
                              profile: GameProfile) -> Optional[UniversalViolation]:
        """패턴 규칙 평가"""
        if len(actions) < 3:
            return None
            
        relevant_actions = [action for action in actions if action.action_type in rule.action_types]
        if len(relevant_actions) < 3:
            return None
            
        # 반복 패턴 검사
        if "repetition_threshold" in rule.parameters:
            threshold = rule.parameters["repetition_threshold"]
            pattern_length = rule.parameters.get("pattern_length", 3)
            
            if len(relevant_actions) >= pattern_length * 2:
                # 최근 액션들에서 패턴 찾기
                recent_actions = relevant_actions[-20:]  # 최근 20개
                sequences = []
                
                for i in range(len(recent_actions) - pattern_length + 1):
                    sequence = tuple(action.action_type for action in recent_actions[i:i+pattern_length])
                    sequences.append(sequence)
                
                # 가장 많이 반복되는 패턴 찾기
                sequence_counts = defaultdict(int)
                for seq in sequences:
                    sequence_counts[seq] += 1
                
                max_repetitions = max(sequence_counts.values()) if sequence_counts else 0
                
                if max_repetitions >= threshold:
                    most_common_pattern = max(sequence_counts.items(), key=lambda x: x[1])
                    
                    return UniversalViolation(
                        player_id=actions[0].player_id,
                        game_id=actions[0].game_id,
                        violation_type=ViolationType.SUSPICIOUS_PATTERN,
                        rule_id=rule.rule_id,
                        severity=rule.severity,
                        timestamp=time.time(),
                        details={
                            "pattern": most_common_pattern[0],
                            "repetitions": most_common_pattern[1],
                            "threshold": threshold,
                            "pattern_type": "sequence_repetition"
                        },
                        action_context=relevant_actions[-10:]
                    )
        
        # 완벽한 타이밍 검사
        if "perfect_timing_threshold" in rule.parameters:
            threshold = rule.parameters["perfect_timing_threshold"]
            
            if len(relevant_actions) >= 5:
                intervals = []
                for i in range(1, len(relevant_actions)):
                    interval = relevant_actions[i].timestamp - relevant_actions[i-1].timestamp
                    intervals.append(interval)
                
                if intervals:
                    variance = np.var(intervals)
                    if variance < threshold:
                        return UniversalViolation(
                            player_id=actions[0].player_id,
                            game_id=actions[0].game_id,
                            violation_type=ViolationType.SUSPICIOUS_PATTERN,
                            rule_id=rule.rule_id,
                            severity=rule.severity,
                            timestamp=time.time(),
                            details={
                                "timing_variance": variance,
                                "threshold": threshold,
                                "pattern_type": "perfect_timing",
                                "interval_count": len(intervals)
                            },
                            action_context=relevant_actions[-5:]
                        )
        
        return None
    
    async def _evaluate_statistical(self, rule: DetectionRule, actions: List[UniversalPlayerAction], 
                                  profile: GameProfile) -> Optional[UniversalViolation]:
        """통계적 규칙 평가"""
        relevant_actions = [
            action for action in actions 
            if action.action_type in rule.action_types and 
               isinstance(action.value, (int, float))
        ]
        
        if len(relevant_actions) < 10:  # 통계 분석에 최소 10개 샘플 필요
            return None
            
        values = [float(action.value) for action in relevant_actions]
        
        # Z-score 기반 이상치 탐지
        if "z_threshold" in rule.parameters:
            mean_val = np.mean(values)
            std_val = np.std(values)
            
            if std_val > 0:
                latest_value = values[-1]
                z_score = abs((latest_value - mean_val) / std_val)
                
                if z_score > rule.parameters["z_threshold"]:
                    return UniversalViolation(
                        player_id=actions[0].player_id,
                        game_id=actions[0].game_id,
                        violation_type=ViolationType.STATISTICAL_ANOMALY,
                        rule_id=rule.rule_id,
                        severity=min(rule.severity * (z_score / rule.parameters["z_threshold"]), 5.0),
                        timestamp=time.time(),
                        details={
                            "z_score": z_score,
                            "threshold": rule.parameters["z_threshold"],
                            "mean_value": mean_val,
                            "std_value": std_val,
                            "anomaly_value": latest_value
                        },
                        action_context=[relevant_actions[-1]]
                    )
        
        return None
    
    async def _evaluate_custom(self, rule: DetectionRule, actions: List[UniversalPlayerAction], 
                             profile: GameProfile) -> Optional[UniversalViolation]:
        """사용자 정의 규칙 평가"""
        if rule.rule_id not in self.custom_rules:
            logger.warning(f"Custom rule {rule.rule_id} not registered")
            return None
            
        try:
            rule_function = self.custom_rules[rule.rule_id]
            result = await rule_function(rule, actions, profile)
            return result
        except Exception as e:
            logger.error(f"Error evaluating custom rule {rule.rule_id}: {e}")
            return None

class UniversalAntiCheatEngine:
    """범용 치팅 탐지 엔진"""
    
    def __init__(self, redis_client: Optional[redis.Redis] = None, enable_ml: bool = True):
        self.redis = redis_client
        self.enable_ml = enable_ml
        
        # 게임 프로파일 관리자
        self.profile_manager = GameProfileManager()
        
        # 동적 규칙 엔진
        self.rule_engine = DynamicRuleEngine()
        
        # ML 엔진 (옵션)
        if enable_ml:
            try:
                self.ml_engine = MLAntiCheatEngine()
                logger.info("Universal ML 치팅 탐지 엔진 초기화 완료")
            except Exception as e:
                logger.warning(f"ML 엔진 초기화 실패: {e}")
                self.ml_engine = None
        else:
            self.ml_engine = None
        
        # 플레이어 액션 버퍼 (게임별)
        self.player_actions: Dict[str, Dict[str, deque]] = defaultdict(lambda: defaultdict(lambda: deque(maxlen=1000)))
        
        # 위험도 점수 (게임별)
        self.violation_scores: Dict[str, Dict[str, float]] = defaultdict(lambda: defaultdict(float))
        
    async def analyze_action(self, action: UniversalPlayerAction) -> List[UniversalViolation]:
        """범용 액션 분석"""
        violations = []
        
        # 게임 프로파일 조회
        profile = self.profile_manager.get_profile(action.game_id)
        if not profile:
            logger.warning(f"Game profile not found: {action.game_id}")
            return violations
        
        # 액션 유효성 검사
        validation_errors = profile.validate_action(action.action_type, action.value, action.metadata)
        if validation_errors:
            violation = UniversalViolation(
                player_id=action.player_id,
                game_id=action.game_id,
                violation_type=ViolationType.INVALID_ACTION,
                rule_id="validation_error",
                severity=2.0,
                timestamp=time.time(),
                details={"errors": validation_errors},
                action_context=[action]
            )
            violations.append(violation)
        
        # 액션 저장
        self.player_actions[action.game_id][action.player_id].append(action)
        
        # Redis에 저장 (옵션)
        if self.redis:
            await self._store_action_redis(action)
        
        # 규칙 기반 탐지
        recent_actions = list(self.player_actions[action.game_id][action.player_id])
        
        for rule in profile.detection_rules:
            if not rule.enabled:
                continue
                
            try:
                violation = await self.rule_engine.evaluate_rule(rule, recent_actions, profile)
                if violation:
                    violations.append(violation)
            except Exception as e:
                logger.error(f"Error evaluating rule {rule.rule_id}: {e}")
        
        # ML 기반 탐지 (추가)
        if self.ml_engine and len(recent_actions) >= 10:
            try:
                # 액션을 ML 엔진 형식으로 변환
                action_dict = {
                    'player_id': action.player_id,
                    'action_type': action.action_type,
                    'timestamp': action.timestamp,
                    'value': action.value if isinstance(action.value, (int, float)) else 0,
                    'metadata': action.metadata
                }
                
                ml_prediction = await self.ml_engine.analyze_player_ml(action.player_id, action_dict)
                
                if ml_prediction and ml_prediction.prediction > 0.7:
                    ml_violation = UniversalViolation(
                        player_id=action.player_id,
                        game_id=action.game_id,
                        violation_type=ViolationType.ML_DETECTION,
                        rule_id="ml_ensemble",
                        severity=ml_prediction.prediction * 5.0,
                        timestamp=time.time(),
                        details={
                            'ml_model': ml_prediction.model_type,
                            'confidence': ml_prediction.confidence,
                            'prediction_score': ml_prediction.prediction,
                            'features_analyzed': list(ml_prediction.features_used.keys()) if ml_prediction.features_used else []
                        },
                        action_context=[action]
                    )
                    violations.append(ml_violation)
                    
            except Exception as e:
                logger.error(f"ML 분석 중 오류: {e}")
        
        # 위반 점수 업데이트
        for violation in violations:
            self.violation_scores[action.game_id][action.player_id] += violation.severity
        
        return violations
    
    async def get_player_risk_score(self, game_id: str, player_id: str) -> float:
        """플레이어 위험도 점수 조회"""
        base_score = self.violation_scores.get(game_id, {}).get(player_id, 0.0)
        
        # 시간에 따른 점수 감쇠
        current_time = time.time()
        recent_actions = self.player_actions.get(game_id, {}).get(player_id, deque())
        
        if recent_actions:
            latest_action_time = recent_actions[-1].timestamp
            time_diff = current_time - latest_action_time
            
            # 1시간마다 10% 감쇠
            decay_factor = 0.9 ** (time_diff / 3600)
            decayed_score = base_score * decay_factor
            
            self.violation_scores[game_id][player_id] = decayed_score
            return min(decayed_score, 10.0)
        
        return min(base_score, 10.0)
    
    async def should_ban_player(self, game_id: str, player_id: str) -> Tuple[bool, str]:
        """플레이어 차단 여부 판단"""
        profile = self.profile_manager.get_profile(game_id)
        if not profile:
            return False, "Game profile not found"
            
        risk_score = await self.get_player_risk_score(game_id, player_id)
        
        if risk_score >= profile.auto_ban_threshold:
            return True, f"Risk score {risk_score:.2f} exceeds threshold {profile.auto_ban_threshold}"
        
        return False, ""
    
    def register_game(self, game_id: str, game_name: str, genre: str, description: str = "") -> GameProfile:
        """새 게임 등록"""
        from .game_profiles import GameGenre
        
        try:
            game_genre = GameGenre(genre.lower())
        except ValueError:
            logger.warning(f"Unknown genre {genre}, using SANDBOX")
            game_genre = GameGenre.SANDBOX
            
        return self.profile_manager.create_profile(game_id, game_name, game_genre, description)
    
    def get_game_profile(self, game_id: str) -> Optional[GameProfile]:
        """게임 프로파일 조회"""
        return self.profile_manager.get_profile(game_id)
    
    def list_games(self) -> List[Dict[str, Any]]:
        """등록된 게임 목록"""
        return self.profile_manager.list_profiles()
    
    async def _store_action_redis(self, action: UniversalPlayerAction):
        """Redis에 액션 저장"""
        if not self.redis:
            return
            
        key = f"universal_actions:{action.game_id}:{action.player_id}"
        action_data = {
            "action_type": action.action_type,
            "timestamp": action.timestamp,
            "value": action.value,
            "metadata": action.metadata,
            "session_id": action.session_id,
            "client_info": action.client_info
        }
        
        try:
            await self.redis.lpush(key, json.dumps(action_data, default=str))
            await self.redis.ltrim(key, 0, 999)  # 최근 1000개만 유지
            await self.redis.expire(key, 86400)  # 24시간 만료
        except Exception as e:
            logger.error(f"Redis 저장 오류: {e}")
    
    async def cleanup_old_data(self):
        """오래된 데이터 정리"""
        current_time = time.time()
        cutoff_time = current_time - 86400  # 24시간 전
        
        for game_id in list(self.player_actions.keys()):
            for player_id in list(self.player_actions[game_id].keys()):
                actions = self.player_actions[game_id][player_id]
                
                # 오래된 액션 제거
                while actions and actions[0].timestamp < cutoff_time:
                    actions.popleft()
                
                # 액션이 없으면 플레이어 제거
                if not actions:
                    del self.player_actions[game_id][player_id]
                    if player_id in self.violation_scores[game_id]:
                        del self.violation_scores[game_id][player_id]
            
            # 게임에 플레이어가 없으면 게임 제거
            if not self.player_actions[game_id]:
                del self.player_actions[game_id]
                if game_id in self.violation_scores:
                    del self.violation_scores[game_id]