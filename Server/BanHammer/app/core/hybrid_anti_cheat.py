import asyncio
import time
import json
from datetime import datetime, timedelta
from typing import Dict, List, Optional, Tuple, Any
from dataclasses import dataclass
import redis.asyncio as redis
import logging
from sqlalchemy.orm import Session

from .anti_cheat import PlayerAction, ViolationRecord, ViolationType
from ..dependencies import get_db
from ..models.database import Player, PlayerAction as DBPlayerAction, Violation

logger = logging.getLogger(__name__)

class HybridAntiCheatEngine:
    """
    하이브리드 안티치트 엔진:
    - 메모리: 최근 활성 플레이어 (1만명)
    - Redis: 중간 캐시 (100만명, 1시간 TTL)  
    - DB: 영구 저장소 (모든 데이터)
    """
    
    def __init__(self, redis_client: Optional[redis.Redis] = None):
        self.redis = redis_client
        
        # 메모리 레이어 (최근 활성 플레이어만)
        self.memory_players = {}  # 최대 10,000명
        self.max_memory_players = 10000
        
        # Redis 레이어 설정
        self.redis_ttl = 3600  # 1시간
        
        # 기본 설정
        self.rate_limits = {
            "reward_collection": {"max_per_minute": 10, "max_value_per_minute": 1000},
            "resource_gather": {"max_per_minute": 30, "max_value_per_minute": 500},
            "level_progress": {"max_per_minute": 3, "max_value_per_minute": 100},
        }
    
    async def analyze_action(self, action: PlayerAction, db: Session) -> List[ViolationRecord]:
        """액션 분석 - 3단계 레이어 활용"""
        violations = []
        player_id = action.player_id
        
        # 1. 메모리에서 플레이어 데이터 확인
        player_data = await self._get_player_data(player_id, db)
        
        # 2. 액션 저장 (DB는 비동기로)
        await self._store_action(action, db)
        
        # 3. 실시간 분석 (메모리 데이터 기반)
        violations.extend(await self._check_rate_limits(action, player_data))
        violations.extend(await self._analyze_behavior(action, player_data))
        
        # 4. 플레이어 데이터 업데이트
        await self._update_player_data(player_id, action, violations)
        
        return violations
    
    async def _get_player_data(self, player_id: str, db: Session) -> Dict:
        """3단계 레이어에서 플레이어 데이터 조회"""
        
        # 1단계: 메모리 확인
        if player_id in self.memory_players:
            self.memory_players[player_id]['last_access'] = time.time()
            return self.memory_players[player_id]
        
        # 2단계: Redis 확인
        if self.redis:
            try:
                redis_data = await self.redis.get(f"player:{player_id}")
                if redis_data:
                    data = json.loads(redis_data)
                    # 메모리로 승격 (자주 사용되는 플레이어)
                    await self._promote_to_memory(player_id, data)
                    return data
            except Exception as e:
                logger.warning(f"Redis 조회 실패: {e}")
        
        # 3단계: DB에서 로드
        return await self._load_from_db(player_id, db)
    
    async def _promote_to_memory(self, player_id: str, data: Dict):
        """Redis → 메모리 승격"""
        # 메모리 공간 확보
        if len(self.memory_players) >= self.max_memory_players:
            await self._evict_from_memory()
        
        data['last_access'] = time.time()
        self.memory_players[player_id] = data
        
    async def _evict_from_memory(self):
        """메모리에서 오래된 플레이어 제거"""
        if not self.memory_players:
            return
            
        # 가장 오래 사용되지 않은 25% 제거
        sorted_players = sorted(
            self.memory_players.items(),
            key=lambda x: x[1].get('last_access', 0)
        )
        
        evict_count = max(1, len(sorted_players) // 4)
        
        for player_id, data in sorted_players[:evict_count]:
            # Redis로 강등
            if self.redis:
                try:
                    await self.redis.setex(
                        f"player:{player_id}", 
                        self.redis_ttl, 
                        json.dumps(data)
                    )
                except Exception as e:
                    logger.warning(f"Redis 저장 실패: {e}")
            
            del self.memory_players[player_id]
    
    async def _load_from_db(self, player_id: str, db: Session) -> Dict:
        """DB에서 플레이어 데이터 로드"""
        # 최근 1시간 액션만 로드 (성능 최적화)
        one_hour_ago = datetime.now() - timedelta(hours=1)
        
        actions = db.query(DBPlayerAction).filter(
            DBPlayerAction.player_id == player_id,
            DBPlayerAction.timestamp >= one_hour_ago
        ).order_by(DBPlayerAction.timestamp.desc()).limit(100).all()
        
        violations = db.query(Violation).filter(
            Violation.player_id == player_id,
            Violation.timestamp >= one_hour_ago
        ).all()
        
        data = {
            'player_id': player_id,
            'actions': [self._action_to_dict(a) for a in actions],
            'violations': [self._violation_to_dict(v) for v in violations],
            'risk_score': 0.0,
            'last_access': time.time(),
            'loaded_at': time.time()
        }
        
        # 메모리에 캐시
        await self._promote_to_memory(player_id, data)
        
        return data
    
    async def _store_action(self, action: PlayerAction, db: Session):
        """액션을 DB에 비동기 저장"""
        try:
            db_action = DBPlayerAction(
                player_id=action.player_id,
                action_type=action.action_type,
                timestamp=datetime.fromtimestamp(action.timestamp),
                value=action.value
            )
            db_action.set_metadata(action.metadata)
            db.add(db_action)
            db.commit()
            
        except Exception as e:
            db.rollback()
            logger.error(f"액션 저장 실패: {e}")
    
    async def _check_rate_limits(self, action: PlayerAction, player_data: Dict) -> List[ViolationRecord]:
        """속도 제한 검사 (메모리 데이터 기반)"""
        violations = []
        
        if action.action_type not in self.rate_limits:
            return violations
        
        limits = self.rate_limits[action.action_type]
        current_time = time.time()
        minute_ago = current_time - 60
        
        # 최근 1분간 같은 타입 액션 확인
        recent_actions = [
            a for a in player_data.get('actions', [])
            if a['action_type'] == action.action_type 
            and a['timestamp'] >= minute_ago
        ]
        
        if len(recent_actions) > limits["max_per_minute"]:
            violations.append(ViolationRecord(
                player_id=action.player_id,
                violation_type=ViolationType.RATE_LIMIT_EXCEEDED,
                severity=2.0,
                timestamp=current_time,
                details={
                    "action_type": action.action_type,
                    "frequency": len(recent_actions),
                    "limit": limits["max_per_minute"]
                }
            ))
        
        return violations
    
    async def _analyze_behavior(self, action: PlayerAction, player_data: Dict) -> List[ViolationRecord]:
        """행동 패턴 분석"""
        violations = []
        actions = player_data.get('actions', [])
        
        if len(actions) < 10:
            return violations
        
        # 봇 탐지 로직 (기존과 동일)
        # ... 구현 생략 (기존 anti_cheat.py의 로직 재사용)
        
        return violations
    
    async def _update_player_data(self, player_id: str, action: PlayerAction, violations: List[ViolationRecord]):
        """플레이어 데이터 업데이트"""
        if player_id not in self.memory_players:
            return
        
        # 액션 추가
        action_dict = self._action_to_dict_from_object(action)
        self.memory_players[player_id]['actions'].insert(0, action_dict)
        
        # 최대 100개 액션만 메모리에 유지
        self.memory_players[player_id]['actions'] = \
            self.memory_players[player_id]['actions'][:100]
        
        # 위험도 점수 업데이트
        for violation in violations:
            self.memory_players[player_id]['risk_score'] += violation.severity
        
        self.memory_players[player_id]['last_access'] = time.time()
    
    def _action_to_dict(self, db_action: DBPlayerAction) -> Dict:
        """DB 액션을 딕셔너리로 변환"""
        return {
            'action_type': db_action.action_type,
            'timestamp': db_action.timestamp.timestamp(),
            'value': db_action.value,
            'metadata': db_action.get_metadata()
        }
    
    def _action_to_dict_from_object(self, action: PlayerAction) -> Dict:
        """액션 객체를 딕셔너리로 변환"""
        return {
            'action_type': action.action_type,
            'timestamp': action.timestamp,
            'value': action.value,
            'metadata': action.metadata
        }
    
    def _violation_to_dict(self, violation: Violation) -> Dict:
        """DB 위반을 딕셔너리로 변환"""
        return {
            'violation_type': violation.violation_type,
            'severity': violation.severity,
            'timestamp': violation.timestamp.timestamp(),
            'details': violation.get_details()
        }
    
    async def get_player_risk_score(self, player_id: str, db: Session) -> float:
        """플레이어 위험도 조회"""
        player_data = await self._get_player_data(player_id, db)
        return player_data.get('risk_score', 0.0)
    
    async def cleanup_old_data(self):
        """정기적인 정리"""
        current_time = time.time()
        
        # 메모리에서 1시간 이상 사용되지 않은 플레이어 제거
        old_players = [
            pid for pid, data in self.memory_players.items()
            if current_time - data.get('last_access', 0) > 3600
        ]
        
        for player_id in old_players:
            data = self.memory_players[player_id]
            
            # Redis로 이동
            if self.redis:
                try:
                    await self.redis.setex(
                        f"player:{player_id}",
                        self.redis_ttl,
                        json.dumps(data)
                    )
                except Exception as e:
                    logger.warning(f"Redis 저장 실패: {e}")
            
            del self.memory_players[player_id]
        
        logger.info(f"메모리 정리 완료. 활성 플레이어: {len(self.memory_players)}")