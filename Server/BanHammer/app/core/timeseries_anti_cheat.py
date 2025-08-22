"""
시계열 DB 기반 안티치트 엔진
- InfluxDB 또는 TimescaleDB 사용
- 대용량 시계열 데이터 처리에 최적화
"""

import asyncio
import time
from typing import Dict, List, Optional
from datetime import datetime, timedelta
import asyncpg  # PostgreSQL/TimescaleDB
# from influxdb_client.client.influxdb_client_async import InfluxDBClientAsync  # InfluxDB

class TimeSeriesAntiCheatEngine:
    """
    TimescaleDB 기반 안티치트 엔진
    - 플레이어 액션: 시계열 데이터로 저장
    - 실시간 집계 쿼리로 치팅 탐지
    - 메모리 사용량 최소화
    """
    
    def __init__(self, db_url: str):
        self.db_url = db_url
        self.connection_pool = None
    
    async def init_db(self):
        """DB 연결 풀 초기화"""
        self.connection_pool = await asyncpg.create_pool(self.db_url)
        
        # TimescaleDB 하이퍼테이블 생성
        async with self.connection_pool.acquire() as conn:
            await conn.execute("""
                CREATE TABLE IF NOT EXISTS player_actions_ts (
                    time TIMESTAMPTZ NOT NULL,
                    player_id TEXT NOT NULL,
                    action_type TEXT NOT NULL,
                    value DOUBLE PRECISION DEFAULT 0,
                    metadata JSONB
                );
                
                -- 하이퍼테이블 생성 (TimescaleDB 전용)
                SELECT create_hypertable('player_actions_ts', 'time', if_not_exists => TRUE);
                
                -- 인덱스 생성
                CREATE INDEX IF NOT EXISTS idx_player_time ON player_actions_ts (player_id, time DESC);
                CREATE INDEX IF NOT EXISTS idx_action_type ON player_actions_ts (action_type, time DESC);
            """)
    
    async def analyze_action_fast(self, player_id: str, action_type: str, value: float) -> Dict:
        """실시간 치팅 탐지 (SQL 집계 쿼리 사용)"""
        async with self.connection_pool.acquire() as conn:
            # 1분간 액션 횟수 및 총합 확인
            result = await conn.fetchrow("""
                SELECT 
                    COUNT(*) as action_count,
                    SUM(value) as total_value
                FROM player_actions_ts 
                WHERE player_id = $1 
                    AND action_type = $2 
                    AND time >= NOW() - INTERVAL '1 minute'
            """, player_id, action_type)
            
            # 액션 저장
            await conn.execute("""
                INSERT INTO player_actions_ts (time, player_id, action_type, value)
                VALUES (NOW(), $1, $2, $3)
            """, player_id, action_type, value)
            
            # 치팅 검사
            violations = []
            
            if result['action_count'] > 10:  # 1분에 10회 초과
                violations.append({
                    'type': 'rate_limit_exceeded',
                    'severity': 2.0,
                    'details': {'count': result['action_count']}
                })
            
            if result['total_value'] > 1000:  # 1분에 1000 초과
                violations.append({
                    'type': 'value_limit_exceeded', 
                    'severity': 3.0,
                    'details': {'total': result['total_value']}
                })
            
            return {
                'violations': violations,
                'player_stats': {
                    'recent_actions': result['action_count'],
                    'recent_total': result['total_value']
                }
            }
    
    async def get_player_behavior_pattern(self, player_id: str, hours: int = 1) -> Dict:
        """플레이어 행동 패턴 분석"""
        async with self.connection_pool.acquire() as conn:
            # 시간대별 활동 패턴
            pattern = await conn.fetch("""
                SELECT 
                    date_trunc('minute', time) as minute,
                    action_type,
                    COUNT(*) as count,
                    AVG(value) as avg_value
                FROM player_actions_ts
                WHERE player_id = $1 
                    AND time >= NOW() - INTERVAL '{} hours'
                GROUP BY minute, action_type
                ORDER BY minute DESC
            """.format(hours), player_id)
            
            return {'pattern': [dict(row) for row in pattern]}
    
    async def detect_bot_behavior(self, player_id: str) -> List[Dict]:
        """봇 행동 탐지 (시계열 분석)"""
        async with self.connection_pool.acquire() as conn:
            # 완벽한 타이밍 간격 탐지
            timing_analysis = await conn.fetchrow("""
                WITH action_intervals AS (
                    SELECT 
                        time,
                        LAG(time) OVER (ORDER BY time) as prev_time,
                        EXTRACT(EPOCH FROM (time - LAG(time) OVER (ORDER BY time))) as interval_seconds
                    FROM player_actions_ts
                    WHERE player_id = $1 
                        AND action_type = 'reward_collection'
                        AND time >= NOW() - INTERVAL '1 hour'
                )
                SELECT 
                    COUNT(*) as total_intervals,
                    STDDEV(interval_seconds) as timing_variance,
                    AVG(interval_seconds) as avg_interval
                FROM action_intervals
                WHERE interval_seconds IS NOT NULL
            """, player_id)
            
            violations = []
            
            if (timing_analysis['timing_variance'] and 
                timing_analysis['timing_variance'] < 0.1 and 
                timing_analysis['total_intervals'] > 10):
                violations.append({
                    'type': 'bot_perfect_timing',
                    'severity': 4.0,
                    'details': {
                        'variance': float(timing_analysis['timing_variance']),
                        'intervals': timing_analysis['total_intervals']
                    }
                })
            
            return violations

# 사용 예시
async def main():
    engine = TimeSeriesAntiCheatEngine("postgresql://user:pass@localhost/gamedb")
    await engine.init_db()
    
    # 실시간 분석
    result = await engine.analyze_action_fast("player_123", "reward_collection", 100.0)
    print(f"치팅 탐지 결과: {result}")