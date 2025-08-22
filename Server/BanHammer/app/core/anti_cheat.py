import asyncio
import time
from datetime import datetime, timedelta
from typing import Dict, List, Optional, Tuple, Any
from dataclasses import dataclass, field
from enum import Enum
import numpy as np
from collections import defaultdict, deque
import redis.asyncio as redis
import json
import logging

from ..ml.training_pipeline import MLAntiCheatEngine
from ..ml.models import ModelPrediction

logger = logging.getLogger(__name__)

class ViolationType(Enum):
    RATE_LIMIT_EXCEEDED = "rate_limit_exceeded"
    SUSPICIOUS_BEHAVIOR = "suspicious_behavior"
    ANOMALY_DETECTED = "anomaly_detected"
    BOT_DETECTED = "bot_detected"
    IMPOSSIBLE_ACTION = "impossible_action"

@dataclass
class PlayerAction:
    player_id: str
    action_type: str
    timestamp: float
    value: float = 0.0
    metadata: Dict[str, Any] = field(default_factory=dict)

@dataclass
class ViolationRecord:
    player_id: str
    violation_type: ViolationType
    severity: float
    timestamp: float
    details: Dict[str, Any] = field(default_factory=dict)

class AntiCheatEngine:
    def __init__(self, redis_client: Optional[redis.Redis] = None, enable_ml: bool = True):
        self.redis = redis_client
        self.enable_ml = enable_ml
        
        # ML 엔진 초기화
        if enable_ml:
            try:
                self.ml_engine = MLAntiCheatEngine()
                logger.info("ML 치팅 탐지 엔진 초기화 완료")
            except Exception as e:
                logger.warning(f"ML 엔진 초기화 실패: {e}")
                self.ml_engine = None
        else:
            self.ml_engine = None
        
        # Rate limiting configurations
        self.rate_limits = {
            "reward_collection": {"max_per_minute": 10, "max_value_per_minute": 1000},
            "resource_gather": {"max_per_minute": 30, "max_value_per_minute": 500},
            "level_progress": {"max_per_minute": 3, "max_value_per_minute": 100},
            "purchase": {"max_per_minute": 5, "max_value_per_minute": 10000}
        }
        
        # Behavioral analysis parameters
        self.behavior_window = 300  # 5 minutes
        self.anomaly_threshold = 2.5  # Standard deviations
        self.bot_detection_patterns = {
            "perfect_timing": 0.05,  # Variance threshold for timing
            "identical_sequences": 5,  # Number of identical sequences before flagging
            "continuous_activity": 3600  # Hours of continuous activity
        }
        
        # In-memory storage for analysis with memory management
        self.player_actions: Dict[str, deque] = defaultdict(lambda: deque(maxlen=1000))
        self.violation_scores: Dict[str, float] = defaultdict(float)
        self.player_stats: Dict[str, Dict] = defaultdict(dict)
        
        # Memory management settings - 현실적인 수치로 변경
        self.max_players_in_memory = 100000  # Maximum players to keep in memory (10만명 = ~7.5GB)
        self.cleanup_interval = 600  # Cleanup every 10 minutes  
        self.last_cleanup = time.time()
        
        # DB 기반 대용량 처리를 위한 플래그
        self.use_db_for_analysis = True  # DB에서 히스토리 분석

    async def analyze_action(self, action: PlayerAction) -> List[ViolationRecord]:
        violations = []
        
        # Check memory usage and cleanup if needed
        await self._check_memory_usage()
        
        # Store action
        self.player_actions[action.player_id].append(action)
        
        # Update Redis if available
        if self.redis:
            await self._store_action_redis(action)
        
        # Check rate limits
        rate_violations = await self._check_rate_limits(action)
        violations.extend(rate_violations)
        
        # Behavioral analysis
        behavior_violations = await self._analyze_behavior(action)
        violations.extend(behavior_violations)
        
        # Statistical anomaly detection
        anomaly_violations = await self._detect_anomalies(action)
        violations.extend(anomaly_violations)
        
        # Bot detection
        bot_violations = await self._detect_bot_behavior(action)
        violations.extend(bot_violations)
        
        # ML 기반 탐지 (추가 검사)
        if self.ml_engine:
            try:
                action_dict = {
                    'player_id': action.player_id,
                    'action_type': action.action_type,
                    'timestamp': action.timestamp,
                    'value': action.value,
                    'metadata': action.metadata
                }
                
                ml_prediction = await self.ml_engine.analyze_player_ml(action.player_id, action_dict)
                
                if ml_prediction and ml_prediction.prediction > 0.7:  # 높은 치팅 확률
                    ml_violation = ViolationRecord(
                        player_id=action.player_id,
                        violation_type=ViolationType.ANOMALY_DETECTED,
                        severity=ml_prediction.prediction * 5.0,  # 0-5 스케일로 변환
                        timestamp=time.time(),
                        details={
                            'ml_model': ml_prediction.model_type,
                            'confidence': ml_prediction.confidence,
                            'prediction_score': ml_prediction.prediction,
                            'features_analyzed': list(ml_prediction.features_used.keys()) if ml_prediction.features_used else []
                        }
                    )
                    violations.append(ml_violation)
                    
            except Exception as e:
                logger.error(f"ML 분석 중 오류: {e}")
        
        # Update violation scores
        for violation in violations:
            self.violation_scores[action.player_id] += violation.severity
            
        return violations

    async def _check_rate_limits(self, action: PlayerAction) -> List[ViolationRecord]:
        violations = []
        action_type = action.action_type
        
        if action_type not in self.rate_limits:
            return violations
            
        limits = self.rate_limits[action_type]
        current_time = time.time()
        minute_ago = current_time - 60
        
        # Get recent actions of same type
        recent_actions = [
            a for a in self.player_actions[action.player_id] 
            if a.action_type == action_type and a.timestamp >= minute_ago
        ]
        
        # Check frequency limit
        if len(recent_actions) > limits["max_per_minute"]:
            violations.append(ViolationRecord(
                player_id=action.player_id,
                violation_type=ViolationType.RATE_LIMIT_EXCEEDED,
                severity=2.0,
                timestamp=current_time,
                details={
                    "action_type": action_type,
                    "frequency": len(recent_actions),
                    "limit": limits["max_per_minute"]
                }
            ))
        
        # Check value limit
        total_value = sum(a.value for a in recent_actions)
        if total_value > limits["max_value_per_minute"]:
            violations.append(ViolationRecord(
                player_id=action.player_id,
                violation_type=ViolationType.RATE_LIMIT_EXCEEDED,
                severity=3.0,
                timestamp=current_time,
                details={
                    "action_type": action_type,
                    "total_value": total_value,
                    "limit": limits["max_value_per_minute"]
                }
            ))
        
        return violations

    async def _analyze_behavior(self, action: PlayerAction) -> List[ViolationRecord]:
        violations = []
        current_time = time.time()
        window_start = current_time - self.behavior_window
        
        # Get actions in behavior window
        recent_actions = [
            a for a in self.player_actions[action.player_id]
            if a.timestamp >= window_start
        ]
        
        if len(recent_actions) < 10:  # Not enough data
            return violations
            
        # Check for perfect timing (bot-like behavior)
        if action.action_type in ["resource_gather", "reward_collection"]:
            same_type_actions = [a for a in recent_actions if a.action_type == action.action_type]
            if len(same_type_actions) >= 5:
                intervals = []
                for i in range(1, len(same_type_actions)):
                    interval = same_type_actions[i].timestamp - same_type_actions[i-1].timestamp
                    intervals.append(interval)
                
                if intervals:
                    variance = np.var(intervals)
                    if variance < self.bot_detection_patterns["perfect_timing"]:
                        violations.append(ViolationRecord(
                            player_id=action.player_id,
                            violation_type=ViolationType.BOT_DETECTED,
                            severity=4.0,
                            timestamp=current_time,
                            details={
                                "pattern": "perfect_timing",
                                "variance": variance,
                                "threshold": self.bot_detection_patterns["perfect_timing"]
                            }
                        ))
        
        # Check for 24/7 activity
        if len(recent_actions) >= 50:  # High activity
            time_span = max(a.timestamp for a in recent_actions) - min(a.timestamp for a in recent_actions)
            if time_span > self.bot_detection_patterns["continuous_activity"]:
                # Check if there are any breaks in activity
                timestamps = sorted([a.timestamp for a in recent_actions])
                max_gap = 0
                for i in range(1, len(timestamps)):
                    gap = timestamps[i] - timestamps[i-1]
                    max_gap = max(max_gap, gap)
                
                if max_gap < 300:  # No break longer than 5 minutes
                    violations.append(ViolationRecord(
                        player_id=action.player_id,
                        violation_type=ViolationType.SUSPICIOUS_BEHAVIOR,
                        severity=3.5,
                        timestamp=current_time,
                        details={
                            "pattern": "continuous_activity",
                            "duration_hours": time_span / 3600,
                            "max_break_seconds": max_gap
                        }
                    ))
        
        return violations

    async def _detect_anomalies(self, action: PlayerAction) -> List[ViolationRecord]:
        violations = []
        
        # Statistical anomaly detection for action values
        if action.value > 0:
            same_type_actions = [
                a.value for a in self.player_actions[action.player_id]
                if a.action_type == action.action_type and a.value > 0
            ]
            
            if len(same_type_actions) >= 10:
                mean_value = np.mean(same_type_actions)
                std_value = np.std(same_type_actions)
                
                if std_value > 0:
                    z_score = abs((action.value - mean_value) / std_value)
                    
                    if z_score > self.anomaly_threshold:
                        violations.append(ViolationRecord(
                            player_id=action.player_id,
                            violation_type=ViolationType.ANOMALY_DETECTED,
                            severity=min(z_score, 5.0),
                            timestamp=time.time(),
                            details={
                                "z_score": z_score,
                                "action_value": action.value,
                                "mean_value": mean_value,
                                "std_value": std_value
                            }
                        ))
        
        return violations

    async def _detect_bot_behavior(self, action: PlayerAction) -> List[ViolationRecord]:
        violations = []
        current_time = time.time()
        
        # Check for identical action sequences
        recent_actions = list(self.player_actions[action.player_id])[-20:]  # Last 20 actions
        
        if len(recent_actions) >= 10:
            # Look for repeating patterns
            sequences = []
            for length in [3, 4, 5]:  # Check sequences of length 3, 4, 5
                for i in range(len(recent_actions) - length + 1):
                    sequence = tuple(a.action_type for a in recent_actions[i:i+length])
                    sequences.append(sequence)
            
            # Count sequence occurrences
            sequence_counts = defaultdict(int)
            for seq in sequences:
                sequence_counts[seq] += 1
            
            # Flag if any sequence repeats too often
            for seq, count in sequence_counts.items():
                if count >= self.bot_detection_patterns["identical_sequences"]:
                    violations.append(ViolationRecord(
                        player_id=action.player_id,
                        violation_type=ViolationType.BOT_DETECTED,
                        severity=3.0,
                        timestamp=current_time,
                        details={
                            "pattern": "identical_sequences",
                            "sequence": seq,
                            "repetitions": count
                        }
                    ))
        
        return violations

    async def _store_action_redis(self, action: PlayerAction):
        if not self.redis:
            return
            
        key = f"player_actions:{action.player_id}"
        action_data = {
            "action_type": action.action_type,
            "timestamp": action.timestamp,
            "value": action.value,
            "metadata": action.metadata
        }
        
        # Store in Redis with expiration
        await self.redis.lpush(key, json.dumps(action_data))
        await self.redis.ltrim(key, 0, 999)  # Keep last 1000 actions
        await self.redis.expire(key, 86400)  # Expire after 24 hours

    async def get_player_risk_score(self, player_id: str) -> float:
        base_score = self.violation_scores.get(player_id, 0.0)
        
        # Decay score over time
        current_time = time.time()
        recent_actions = [
            a for a in self.player_actions[player_id]
            if current_time - a.timestamp < 3600  # Last hour
        ]
        
        if not recent_actions:
            # Decay score if no recent activity
            decay_factor = 0.9
            self.violation_scores[player_id] *= decay_factor
            base_score = self.violation_scores[player_id]
        
        return min(base_score, 10.0)  # Cap at 10.0

    async def should_ban_player(self, player_id: str) -> Tuple[bool, str]:
        risk_score = await self.get_player_risk_score(player_id)
        
        if risk_score >= 8.0:
            return True, "High risk score indicating severe violations"
        
        # Check for specific critical violations
        recent_actions = list(self.player_actions[player_id])[-50:]  # Last 50 actions
        current_time = time.time()
        
        critical_violations = 0
        for _ in recent_actions:
            # This would be populated by actual violation checking logic
            pass
        
        if critical_violations >= 3:
            return True, "Multiple critical violations detected"
        
        return False, ""

    async def _check_memory_usage(self):
        """Check and manage memory usage to prevent memory leaks."""
        current_time = time.time()
        
        # Check if cleanup is needed
        if current_time - self.last_cleanup > self.cleanup_interval:
            await self._cleanup_memory()
            self.last_cleanup = current_time
        
        # Emergency cleanup if too many players in memory
        if len(self.player_actions) > self.max_players_in_memory:
            await self._emergency_cleanup()
    
    async def _emergency_cleanup(self):
        """Emergency cleanup when memory usage is too high."""
        current_time = time.time()
        players_to_remove = []
        
        # Sort players by last activity
        player_last_activity = {}
        for player_id, actions in self.player_actions.items():
            if actions:
                player_last_activity[player_id] = actions[-1].timestamp
            else:
                players_to_remove.append(player_id)
        
        # Remove players with no actions first
        for player_id in players_to_remove:
            self._remove_player_data(player_id)
        
        # If still too many players, remove oldest inactive ones
        if len(self.player_actions) > self.max_players_in_memory:
            sorted_players = sorted(player_last_activity.items(), key=lambda x: x[1])
            excess_count = len(self.player_actions) - self.max_players_in_memory + 1000000  # Remove extra 1 million
            
            for player_id, _ in sorted_players[:excess_count]:
                self._remove_player_data(player_id)
        
        logger.warning(f"Emergency cleanup completed. Players in memory: {len(self.player_actions)}")
    
    def _remove_player_data(self, player_id: str):
        """Remove all data for a specific player."""
        if player_id in self.player_actions:
            del self.player_actions[player_id]
        if player_id in self.violation_scores:
            del self.violation_scores[player_id]
        if player_id in self.player_stats:
            del self.player_stats[player_id]

    async def _cleanup_memory(self):
        """Regular cleanup of old data to manage memory usage."""
        current_time = time.time()
        cutoff_time = current_time - 86400  # 24 hours ago
        
        for player_id in list(self.player_actions.keys()):
            # Remove old actions
            actions = self.player_actions[player_id]
            while actions and actions[0].timestamp < cutoff_time:
                actions.popleft()
            
            # Remove player if no recent actions
            if not actions:
                self._remove_player_data(player_id)
        
        logger.info(f"Memory cleanup completed. Players in memory: {len(self.player_actions)}")

    async def cleanup_old_data(self):
        """Legacy method for compatibility."""
        await self._cleanup_memory()