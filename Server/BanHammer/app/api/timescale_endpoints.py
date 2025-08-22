from fastapi import APIRouter, Depends, HTTPException, BackgroundTasks
from typing import List, Optional, Dict, Any
from datetime import datetime, timedelta
import time
import asyncio

from ..core.timescale_anti_cheat import TimescaleAntiCheatEngine, PlayerAction
from ..dependencies import get_timescale_engine
from ..schemas import (
    PlayerActionCreate, ViolationResponse, PlayerRiskResponse,
    BanPlayerRequest, PlayerStatsResponse
)

router = APIRouter()

@router.post("/action", response_model=Dict[str, Any])
async def submit_player_action_ts(
    action_data: PlayerActionCreate,
    background_tasks: BackgroundTasks,
    ts_engine: TimescaleAntiCheatEngine = Depends(get_timescale_engine)
):
    """
    TimescaleDB 기반 대용량 플레이어 액션 분석
    - 5천만명+ 지원
    - 메모리 사용량 최소화
    - 실시간 SQL 집계 분석
    """
    try:
        # 입력 검증
        if action_data.metadata and len(str(action_data.metadata)) > 10000:
            raise HTTPException(status_code=400, detail="Metadata too large")
        
        # PlayerAction 객체 생성
        action = PlayerAction(
            player_id=action_data.player_id,
            action_type=action_data.action_type,
            timestamp=time.time(),
            value=action_data.value,
            metadata=action_data.metadata or {}
        )
        
        # TimescaleDB 기반 실시간 분석
        violations = await ts_engine.analyze_action(action)
        
        # 현재 위험도 조회
        current_risk_score = await ts_engine.get_player_risk_score(action.player_id)
        
        # 자동 차단 검사
        should_ban, ban_reason = await ts_engine.should_ban_player(action.player_id)
        if should_ban:
            background_tasks.add_task(auto_ban_player_task, action.player_id, ban_reason, ts_engine)
        
        return {
            "action_processed": True,
            "violations_detected": len(violations),
            "current_risk_score": current_risk_score,
            "should_review": current_risk_score > 5.0,
            "violations": [
                {
                    "type": v.violation_type.value,
                    "severity": v.severity,
                    "details": v.details
                } for v in violations
            ]
        }
    
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Analysis failed: {str(e)}")

@router.get("/player/{player_id}/risk", response_model=PlayerRiskResponse) 
async def get_player_risk_ts(
    player_id: str,
    ts_engine: TimescaleAntiCheatEngine = Depends(get_timescale_engine)
):
    """플레이어 위험도 조회 (TimescaleDB)"""
    try:
        # 위험도 조회
        risk_score = await ts_engine.get_player_risk_score(player_id)
        
        # 최근 위반 사항 조회 (SQL 직접 쿼리)
        async with ts_engine.connection_pool.acquire() as conn:
            recent_violations = await conn.fetch("""
                SELECT violation_type, severity, time, details
                FROM violations_ts
                WHERE player_id = $1
                    AND time >= NOW() - INTERVAL '24 hours'
                ORDER BY time DESC
                LIMIT 10
            """, player_id)
            
            # 차단 상태 확인
            ban_status = await conn.fetchrow("""
                SELECT is_banned, ban_reason
                FROM player_summary
                WHERE player_id = $1
            """, player_id)
        
        is_banned = ban_status['is_banned'] if ban_status else False
        
        return PlayerRiskResponse(
            player_id=player_id,
            risk_score=risk_score,
            is_banned=is_banned,
            recent_violations=[
                ViolationResponse(
                    violation_type=v['violation_type'],
                    severity=v['severity'],
                    timestamp=v['time'],
                    details=eval(v['details']) if v['details'] else {}
                ) for v in recent_violations
            ]
        )
    
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Risk query failed: {str(e)}")

@router.get("/player/{player_id}/violations", response_model=List[ViolationResponse])
async def get_player_violations_ts(
    player_id: str,
    hours: int = 24,
    limit: int = 100,
    ts_engine: TimescaleAntiCheatEngine = Depends(get_timescale_engine)
):
    """플레이어 위반 기록 조회 (TimescaleDB)"""
    try:
        async with ts_engine.connection_pool.acquire() as conn:
            violations = await conn.fetch("""
                SELECT violation_type, severity, time, details
                FROM violations_ts
                WHERE player_id = $1
                    AND time >= NOW() - INTERVAL '{} hours'
                ORDER BY time DESC
                LIMIT $2
            """.format(hours), player_id, limit)
            
        return [
            ViolationResponse(
                violation_type=v['violation_type'],
                severity=v['severity'], 
                timestamp=v['time'],
                details=eval(v['details']) if v['details'] else {}
            ) for v in violations
        ]
    
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Violations query failed: {str(e)}")

@router.get("/player/{player_id}/behavior-analysis")
async def get_player_behavior_analysis(
    player_id: str,
    hours: int = 6,
    ts_engine: TimescaleAntiCheatEngine = Depends(get_timescale_engine)
):
    """플레이어 행동 패턴 상세 분석"""
    try:
        async with ts_engine.connection_pool.acquire() as conn:
            # 시간대별 활동 패턴
            hourly_pattern = await conn.fetch("""
                SELECT 
                    date_trunc('hour', time) as hour_bucket,
                    action_type,
                    COUNT(*) as action_count,
                    AVG(value) as avg_value,
                    STDDEV(value) as value_stddev
                FROM player_actions_ts
                WHERE player_id = $1 
                    AND time >= NOW() - INTERVAL '{} hours'
                GROUP BY hour_bucket, action_type
                ORDER BY hour_bucket DESC
            """.format(hours), player_id)
            
            # 액션 타입별 통계
            action_stats = await conn.fetch("""
                SELECT 
                    action_type,
                    COUNT(*) as total_count,
                    AVG(value) as avg_value,
                    MAX(value) as max_value,
                    PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY value) as p95_value
                FROM player_actions_ts
                WHERE player_id = $1
                    AND time >= NOW() - INTERVAL '{} hours'
                GROUP BY action_type
                ORDER BY total_count DESC
            """.format(hours), player_id)
            
            # 타이밍 분석 (봇 탐지용)
            timing_analysis = await conn.fetchrow("""
                WITH intervals AS (
                    SELECT 
                        action_type,
                        EXTRACT(EPOCH FROM (time - LAG(time) OVER (PARTITION BY action_type ORDER BY time))) as interval_sec
                    FROM player_actions_ts
                    WHERE player_id = $1
                        AND time >= NOW() - INTERVAL '{} hours'
                )
                SELECT 
                    COUNT(*) as total_intervals,
                    AVG(interval_sec) as avg_interval,
                    STDDEV(interval_sec) as interval_stddev,
                    MIN(interval_sec) as min_interval,
                    MAX(interval_sec) as max_interval
                FROM intervals
                WHERE interval_sec IS NOT NULL
                    AND interval_sec BETWEEN 0.1 AND 3600
            """.format(hours), player_id)
        
        return {
            "player_id": player_id,
            "analysis_period_hours": hours,
            "hourly_pattern": [dict(row) for row in hourly_pattern],
            "action_statistics": [dict(row) for row in action_stats],
            "timing_analysis": dict(timing_analysis) if timing_analysis else {},
            "risk_indicators": {
                "perfect_timing": timing_analysis['interval_stddev'] < 0.1 if timing_analysis else False,
                "high_frequency": len(hourly_pattern) > hours * 0.8,  # 80% 이상 시간대 활동
                "value_anomalies": any(row['value_stddev'] and row['value_stddev'] > row['avg_value'] * 2 
                                     for row in action_stats if row['value_stddev'])
            }
        }
    
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Behavior analysis failed: {str(e)}")

@router.post("/player/{player_id}/ban")
async def ban_player_ts(
    player_id: str,
    ban_request: BanPlayerRequest,
    ts_engine: TimescaleAntiCheatEngine = Depends(get_timescale_engine)
):
    """플레이어 차단 (TimescaleDB)"""
    try:
        async with ts_engine.connection_pool.acquire() as conn:
            # 플레이어 존재 확인
            player_exists = await conn.fetchval("""
                SELECT COUNT(*) FROM player_summary WHERE player_id = $1
            """, player_id)
            
            if not player_exists:
                raise HTTPException(status_code=404, detail="Player not found")
            
            # 차단 정보 업데이트
            await conn.execute("""
                UPDATE player_summary 
                SET is_banned = true,
                    ban_reason = $2,
                    ban_timestamp = NOW(),
                    updated_at = NOW()
                WHERE player_id = $1
            """, player_id, ban_request.reason)
            
            return {
                "message": f"Player {player_id} has been banned",
                "reason": ban_request.reason,
                "banned_by": ban_request.banned_by
            }
    
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Ban failed: {str(e)}")

@router.post("/player/{player_id}/unban") 
async def unban_player_ts(
    player_id: str,
    ts_engine: TimescaleAntiCheatEngine = Depends(get_timescale_engine)
):
    """플레이어 차단 해제"""
    try:
        async with ts_engine.connection_pool.acquire() as conn:
            result = await conn.execute("""
                UPDATE player_summary
                SET is_banned = false,
                    ban_reason = NULL,
                    ban_timestamp = NULL, 
                    updated_at = NOW()
                WHERE player_id = $1 AND is_banned = true
            """, player_id)
            
            if result == "UPDATE 0":
                raise HTTPException(status_code=404, detail="Player not found or not banned")
            
            return {"message": f"Player {player_id} has been unbanned"}
    
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Unban failed: {str(e)}")

@router.get("/violations/recent", response_model=List[ViolationResponse])
async def get_recent_violations_ts(
    hours: int = 24,
    severity_threshold: float = 2.0,
    limit: int = 100,
    ts_engine: TimescaleAntiCheatEngine = Depends(get_timescale_engine)
):
    """최근 위반 사항 조회"""
    try:
        async with ts_engine.connection_pool.acquire() as conn:
            violations = await conn.fetch("""
                SELECT player_id, violation_type, severity, time, details
                FROM violations_ts
                WHERE time >= NOW() - INTERVAL '{} hours'
                    AND severity >= $1
                ORDER BY time DESC
                LIMIT $2
            """.format(hours), severity_threshold, limit)
            
        return [
            ViolationResponse(
                player_id=v['player_id'],
                violation_type=v['violation_type'],
                severity=v['severity'],
                timestamp=v['time'],
                details=eval(v['details']) if v['details'] else {}
            ) for v in violations
        ]
    
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Recent violations query failed: {str(e)}")

@router.get("/stats/overview")
async def get_overview_stats_ts(
    ts_engine: TimescaleAntiCheatEngine = Depends(get_timescale_engine)
):
    """시스템 전체 통계 (TimescaleDB 연속 집계뷰 활용)"""
    try:
        stats = await ts_engine.get_system_stats()
        
        # 추가 상세 통계
        async with ts_engine.connection_pool.acquire() as conn:
            # 차단된 플레이어 수
            banned_count = await conn.fetchval("""
                SELECT COUNT(*) FROM player_summary WHERE is_banned = true
            """)
            
            # 고위험 플레이어 수  
            high_risk_count = await conn.fetchval("""
                SELECT COUNT(*) FROM player_summary WHERE current_risk_score > 5.0
            """)
            
            # 시간대별 액션 추이 (연속 집계뷰 사용)
            hourly_trend = await conn.fetch("""
                SELECT 
                    bucket,
                    SUM(total_actions) as actions,
                    COUNT(DISTINCT player_id) as active_players
                FROM action_stats_1min
                WHERE bucket >= NOW() - INTERVAL '24 hours'
                GROUP BY bucket
                ORDER BY bucket DESC
                LIMIT 24
            """)
        
        return {
            **stats,
            "banned_players": banned_count,
            "high_risk_players": high_risk_count,
            "hourly_trend": [dict(row) for row in hourly_trend]
        }
    
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Stats query failed: {str(e)}")

@router.get("/stats/performance")
async def get_performance_stats(
    ts_engine: TimescaleAntiCheatEngine = Depends(get_timescale_engine)
):
    """TimescaleDB 성능 통계"""
    try:
        async with ts_engine.connection_pool.acquire() as conn:
            # 테이블 크기 정보
            table_sizes = await conn.fetch("""
                SELECT 
                    schemaname,
                    tablename,
                    pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) as size,
                    pg_size_pretty(pg_relation_size(schemaname||'.'||tablename)) as table_size
                FROM pg_tables 
                WHERE tablename IN ('player_actions_ts', 'violations_ts', 'player_summary')
                ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC
            """)
            
            # 청크 정보 (TimescaleDB 전용)
            chunk_info = await conn.fetch("""
                SELECT 
                    chunk_schema,
                    chunk_name,
                    range_start,
                    range_end,
                    pg_size_pretty(pg_relation_size(chunk_schema||'.'||chunk_name)) as chunk_size
                FROM timescaledb_information.chunks
                WHERE hypertable_name IN ('player_actions_ts', 'violations_ts')
                ORDER BY range_start DESC
                LIMIT 10
            """)
            
            return {
                "table_sizes": [dict(row) for row in table_sizes],
                "recent_chunks": [dict(row) for row in chunk_info],
                "cache_stats": {
                    "cached_players": len(ts_engine.player_cache),
                    "max_cache_size": ts_engine.max_cache_size
                }
            }
    
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Performance stats failed: {str(e)}")

# 백그라운드 태스크
async def auto_ban_player_task(player_id: str, reason: str, ts_engine: TimescaleAntiCheatEngine):
    """자동 차단 백그라운드 태스크"""
    try:
        async with ts_engine.connection_pool.acquire() as conn:
            await conn.execute("""
                UPDATE player_summary
                SET is_banned = true,
                    ban_reason = $2,
                    ban_timestamp = NOW(),
                    updated_at = NOW()
                WHERE player_id = $1
            """, player_id, f"자동 차단: {reason}")
            
        logger.info(f"자동 차단 완료: {player_id} - {reason}")
    
    except Exception as e:
        logger.error(f"자동 차단 실패: {player_id} - {e}")