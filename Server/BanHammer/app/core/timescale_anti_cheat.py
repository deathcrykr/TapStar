import asyncio
import asyncpg
import time
import json
import logging
from datetime import datetime, timedelta
from typing import Dict, List, Optional, Tuple, Any
from dataclasses import dataclass, asdict
from enum import Enum

from .anti_cheat import ViolationType, ViolationRecord
from ..models.timescale_models import PlayerActionTimeseries, ViolationTimeseries, PlayerSummary

logger = logging.getLogger(__name__)

@dataclass
class PlayerAction:
    player_id: str
    action_type: str
    timestamp: float
    value: float = 0.0
    metadata: Dict[str, Any] = None

class TimescaleAntiCheatEngine:
    """
    TimescaleDB 기반 대용량 안티치트 엔진
    - 5천만명+ 플레이어 지원
    - 메모리 사용량 최소화 (~100MB)
    - 실시간 SQL 집계 쿼리 기반 탐지
    """
    
    def __init__(self, db_url: str, redis_client=None):
        self.db_url = db_url
        self.redis = redis_client
        self.connection_pool = None
        
        # 기본 설정
        self.rate_limits = {
            "reward_collection": {"max_per_minute": 10, "max_value_per_minute": 1000},
            "resource_gather": {"max_per_minute": 30, "max_value_per_minute": 500}, 
            "level_progress": {"max_per_minute": 3, "max_value_per_minute": 100},
            "purchase": {"max_per_minute": 5, "max_value_per_minute": 10000},
            "combat": {"max_per_minute": 60, "max_value_per_minute": 2000},
            "trade": {"max_per_minute": 20, "max_value_per_minute": 50000}
        }
        
        # 탐지 임계값
        self.anomaly_threshold = 2.5
        self.bot_timing_threshold = 0.05  # 5% 분산 이하면 봇 의심
        self.auto_ban_threshold = 8.0
        
        # 소량 캐시 (최근 활성 플레이어 요약 정보만)
        self.player_cache = {}  # 최대 1만명
        self.max_cache_size = 10000
    
    async def init_connection_pool(self):
        """비동기 DB 연결 풀 초기화"""
        self.connection_pool = await asyncpg.create_pool(
            self.db_url,
            min_size=10,
            max_size=50,
            command_timeout=60
        )
        logger.info("TimescaleDB 연결 풀 초기화 완료")
    
    async def analyze_action(self, action: PlayerAction) -> List[ViolationRecord]:
        """대용량 실시간 액션 분석"""
        violations = []
        
        try:
            async with self.connection_pool.acquire() as conn:
                # 1. 액션 저장 (비동기)
                await self._store_action_async(conn, action)
                
                # 2. 실시간 치팅 탐지 (SQL 집계)
                violations.extend(await self._check_rate_limits_sql(conn, action))
                violations.extend(await self._detect_anomalies_sql(conn, action))
                violations.extend(await self._detect_bot_behavior_sql(conn, action))
                
                # 3. 위반 사항 저장
                if violations:
                    await self._store_violations_async(conn, violations)
                
                # 4. 플레이어 요약 정보 업데이트
                await self._update_player_summary(conn, action, violations)
                
        except Exception as e:
            logger.error(f"액션 분석 중 오류: {e}")
            
        return violations
    
    async def _store_action_async(self, conn: asyncpg.Connection, action: PlayerAction):
        """액션을 TimescaleDB에 고속 저장"""
        await conn.execute("""
            INSERT INTO player_actions_ts (time, player_id, action_type, value, metadata)
            VALUES ($1, $2, $3, $4, $5)
        """, 
            datetime.fromtimestamp(action.timestamp),
            action.player_id,
            action.action_type, 
            action.value,
            json.dumps(action.metadata) if action.metadata else None
        )
    
    async def _check_rate_limits_sql(self, conn: asyncpg.Connection, action: PlayerAction) -> List[ViolationRecord]:
        """SQL 집계로 속도 제한 검사 - 초고속"""
        violations = []
        
        if action.action_type not in self.rate_limits:
            return violations
        
        limits = self.rate_limits[action.action_type]
        
        # 1분간 액션 횟수와 총합을 한 번의 쿼리로 조회
        result = await conn.fetchrow("""
            SELECT 
                COUNT(*) as action_count,
                COALESCE(SUM(value), 0) as total_value
            FROM player_actions_ts 
            WHERE player_id = $1 
                AND action_type = $2 
                AND time >= NOW() - INTERVAL '1 minute'
        """, action.player_id, action.action_type)
        
        current_time = time.time()
        
        # 빈도 제한 검사
        if result['action_count'] > limits["max_per_minute"]:
            violations.append(ViolationRecord(
                player_id=action.player_id,
                violation_type=ViolationType.RATE_LIMIT_EXCEEDED,
                severity=2.0 + (result['action_count'] - limits["max_per_minute"]) * 0.1,
                timestamp=current_time,
                details={
                    "action_type": action.action_type,
                    "frequency": result['action_count'],
                    "limit": limits["max_per_minute"]
                }
            ))
        
        # 값 제한 검사  
        if result['total_value'] > limits["max_value_per_minute"]:
            violations.append(ViolationRecord(
                player_id=action.player_id,
                violation_type=ViolationType.RATE_LIMIT_EXCEEDED,
                severity=3.0 + (result['total_value'] - limits["max_value_per_minute"]) / limits["max_value_per_minute"],
                timestamp=current_time,
                details={
                    "action_type": action.action_type,
                    "total_value": result['total_value'],
                    "limit": limits["max_value_per_minute"]
                }
            ))
        
        return violations
    
    async def _detect_anomalies_sql(self, conn: asyncpg.Connection, action: PlayerAction) -> List[ViolationRecord]:
        """SQL 기반 통계적 이상치 탐지"""
        violations = []
        
        if action.value <= 0:
            return violations
        
        # 최근 1시간 동안의 같은 액션 타입 통계 조회
        stats = await conn.fetchrow("""
            SELECT 
                COUNT(*) as sample_count,
                AVG(value) as mean_value,
                STDDEV(value) as std_value
            FROM player_actions_ts
            WHERE player_id = $1 
                AND action_type = $2 
                AND value > 0
                AND time >= NOW() - INTERVAL '1 hour'
        """, action.player_id, action.action_type)
        
        if stats['sample_count'] >= 10 and stats['std_value'] > 0:
            z_score = abs((action.value - stats['mean_value']) / stats['std_value'])
            
            if z_score > self.anomaly_threshold:
                violations.append(ViolationRecord(
                    player_id=action.player_id,
                    violation_type=ViolationType.ANOMALY_DETECTED,
                    severity=min(z_score, 5.0),
                    timestamp=time.time(),
                    details={
                        "z_score": float(z_score),
                        "action_value": action.value,
                        "mean_value": float(stats['mean_value']),
                        "std_value": float(stats['std_value'])
                    }
                ))
        
        return violations
    
    async def _detect_bot_behavior_sql(self, conn: asyncpg.Connection, action: PlayerAction) -> List[ViolationRecord]:
        """SQL 기반 봇 행동 패턴 탐지"""
        violations = []
        current_time = time.time()
        
        # 1. 완벽한 타이밍 간격 탐지 (Window Function 사용)
        timing_stats = await conn.fetchrow("""
            WITH action_intervals AS (
                SELECT 
                    EXTRACT(EPOCH FROM (time - LAG(time) OVER (ORDER BY time))) as interval_seconds
                FROM player_actions_ts
                WHERE player_id = $1 
                    AND action_type = $2
                    AND time >= NOW() - INTERVAL '30 minutes'
                ORDER BY time
            )
            SELECT 
                COUNT(*) as interval_count,
                COALESCE(STDDEV(interval_seconds), 0) as timing_variance,
                AVG(interval_seconds) as avg_interval
            FROM action_intervals
            WHERE interval_seconds IS NOT NULL 
                AND interval_seconds BETWEEN 0.1 AND 300
        """, action.player_id, action.action_type)
        
        if (timing_stats['interval_count'] > 10 and 
            timing_stats['timing_variance'] < self.bot_timing_threshold):
            violations.append(ViolationRecord(
                player_id=action.player_id,
                violation_type=ViolationType.BOT_DETECTED,
                severity=4.0,
                timestamp=current_time,
                details={
                    "pattern": "perfect_timing",
                    "variance": float(timing_stats['timing_variance']),
                    "intervals": timing_stats['interval_count'],
                    "avg_interval": float(timing_stats['avg_interval'])
                }
            ))
        
        # 2. 반복 시퀀스 패턴 탐지  
        sequence_stats = await conn.fetchrow("""
            WITH recent_actions AS (
                SELECT action_type,
                       LAG(action_type, 1) OVER (ORDER BY time) as prev1,
                       LAG(action_type, 2) OVER (ORDER BY time) as prev2
                FROM player_actions_ts
                WHERE player_id = $1 
                    AND time >= NOW() - INTERVAL '20 minutes'
                ORDER BY time
            ),
            patterns AS (
                SELECT CONCAT(prev2, '-', prev1, '-', action_type) as pattern
                FROM recent_actions
                WHERE prev2 IS NOT NULL
            )
            SELECT pattern, COUNT(*) as pattern_count
            FROM patterns
            GROUP BY pattern
            ORDER BY pattern_count DESC
            LIMIT 1
        """, action.player_id)
        
        if sequence_stats and sequence_stats['pattern_count'] > 5:
            violations.append(ViolationRecord(
                player_id=action.player_id,
                violation_type=ViolationType.BOT_DETECTED,
                severity=3.5,
                timestamp=current_time,
                details={
                    "pattern": "repeated_sequence",
                    "sequence": sequence_stats['pattern'],
                    "repetitions": sequence_stats['pattern_count']
                }
            ))
        
        # 3. 24시간 연속 활동 탐지
        activity_stats = await conn.fetchrow("""
            WITH hourly_activity AS (
                SELECT date_trunc('hour', time) as hour_bucket
                FROM player_actions_ts
                WHERE player_id = $1 
                    AND time >= NOW() - INTERVAL '24 hours'
                GROUP BY hour_bucket
            )
            SELECT COUNT(*) as active_hours
            FROM hourly_activity
        """, action.player_id)
        
        if activity_stats['active_hours'] >= 20:  # 24시간 중 20시간 이상 활동
            violations.append(ViolationRecord(
                player_id=action.player_id,
                violation_type=ViolationType.SUSPICIOUS_BEHAVIOR,
                severity=3.0,
                timestamp=current_time,
                details={
                    "pattern": "excessive_activity",
                    "active_hours": activity_stats['active_hours']
                }
            ))
        
        return violations
    
    async def _store_violations_async(self, conn: asyncpg.Connection, violations: List[ViolationRecord]):
        """위반 사항 배치 저장"""
        if not violations:
            return
        
        values = []
        for v in violations:
            values.append((
                datetime.fromtimestamp(v.timestamp),
                v.player_id,
                v.violation_type.value,
                v.severity,
                json.dumps(v.details)
            ))
        
        await conn.executemany("""
            INSERT INTO violations_ts (time, player_id, violation_type, severity, details)
            VALUES ($1, $2, $3, $4, $5)
        """, values)
    
    async def _update_player_summary(self, conn: asyncpg.Connection, action: PlayerAction, violations: List[ViolationRecord]):
        """플레이어 요약 정보 업데이트 (UPSERT)"""
        violation_score = sum(v.severity for v in violations)
        
        await conn.execute("""
            INSERT INTO player_summary (
                player_id, last_activity, current_risk_score,
                total_actions_today, total_violations_today, updated_at
            ) VALUES ($1, $2, $3, 1, $4, NOW())
            ON CONFLICT (player_id) DO UPDATE SET
                last_activity = $2,
                current_risk_score = player_summary.current_risk_score + $3,
                total_actions_today = player_summary.total_actions_today + 1,
                total_violations_today = player_summary.total_violations_today + $4,
                updated_at = NOW()
        """, 
            action.player_id,
            datetime.fromtimestamp(action.timestamp),
            violation_score,
            len(violations)
        )
    
    async def get_player_risk_score(self, player_id: str) -> float:
        """플레이어 위험도 조회 (캐시 우선)"""
        # 1. 캐시 확인
        if player_id in self.player_cache:
            cache_entry = self.player_cache[player_id]
            if time.time() - cache_entry['cached_at'] < 300:  # 5분 캐시
                return cache_entry['risk_score']
        
        # 2. DB 조회
        async with self.connection_pool.acquire() as conn:
            result = await conn.fetchrow("""
                SELECT current_risk_score
                FROM player_summary
                WHERE player_id = $1
            """, player_id)
            
            risk_score = result['current_risk_score'] if result else 0.0
            
            # 3. 캐시 업데이트
            self._update_cache(player_id, {'risk_score': risk_score})
            
            return risk_score
    
    async def should_ban_player(self, player_id: str) -> Tuple[bool, str]:
        """자동 차단 여부 결정"""
        risk_score = await self.get_player_risk_score(player_id)
        
        if risk_score >= self.auto_ban_threshold:
            return True, f"자동 차단 - 위험도 {risk_score:.1f}"
        
        # 최근 심각한 위반 검사
        async with self.connection_pool.acquire() as conn:
            critical_violations = await conn.fetchval("""
                SELECT COUNT(*)
                FROM violations_ts
                WHERE player_id = $1
                    AND severity >= 4.0
                    AND time >= NOW() - INTERVAL '1 hour'
            """, player_id)
            
            if critical_violations >= 3:
                return True, f"심각한 위반 {critical_violations}회 탐지"
        
        return False, ""
    
    def _update_cache(self, player_id: str, data: Dict):
        """캐시 업데이트 (LRU 방식)"""
        if len(self.player_cache) >= self.max_cache_size:
            # 가장 오래된 항목 제거
            oldest_key = min(self.player_cache.keys(), 
                           key=lambda k: self.player_cache[k]['cached_at'])
            del self.player_cache[oldest_key]
        
        data['cached_at'] = time.time()
        self.player_cache[player_id] = data
    
    async def get_system_stats(self) -> Dict[str, Any]:
        """시스템 통계 조회"""
        async with self.connection_pool.acquire() as conn:
            # 오늘의 전체 통계
            today_stats = await conn.fetchrow("""
                SELECT 
                    COUNT(DISTINCT player_id) as active_players,
                    COUNT(*) as total_actions,
                    AVG(value) as avg_action_value
                FROM player_actions_ts
                WHERE time >= CURRENT_DATE
            """)
            
            # 위반 통계
            violation_stats = await conn.fetch("""
                SELECT 
                    violation_type,
                    COUNT(*) as count,
                    AVG(severity) as avg_severity
                FROM violations_ts
                WHERE time >= CURRENT_DATE
                GROUP BY violation_type
                ORDER BY count DESC
            """)
            
            return {
                "active_players_today": today_stats['active_players'],
                "total_actions_today": today_stats['total_actions'], 
                "avg_action_value": float(today_stats['avg_action_value'] or 0),
                "violation_breakdown": [dict(row) for row in violation_stats],
                "cache_size": len(self.player_cache)
            }
    
    async def cleanup_old_data(self):
        """정기 정리 (TimescaleDB 자동 정책 사용)"""
        # TimescaleDB의 retention policy가 자동으로 처리
        # 캐시만 정리
        current_time = time.time()
        expired_keys = [
            k for k, v in self.player_cache.items()
            if current_time - v['cached_at'] > 3600  # 1시간 만료
        ]
        
        for key in expired_keys:
            del self.player_cache[key]
        
        logger.info(f"캐시 정리 완료. 활성 캐시: {len(self.player_cache)}")
    
    async def close(self):
        """연결 풀 정리"""
        if self.connection_pool:
            await self.connection_pool.close()