from sqlalchemy import Column, Integer, String, Float, DateTime, Text, Boolean, Index, text
from sqlalchemy.ext.declarative import declarative_base
from sqlalchemy.dialects.postgresql import JSONB, TIMESTAMPTZ
from sqlalchemy.sql import func
from datetime import datetime
import json

TimescaleBase = declarative_base()

class PlayerActionTimeseries(TimescaleBase):
    """
    TimescaleDB 하이퍼테이블 - 플레이어 액션 시계열 데이터
    5천만명의 대용량 액션 데이터를 효율적으로 저장
    """
    __tablename__ = "player_actions_ts"
    
    # 시계열 데이터 구조
    time = Column(TIMESTAMPTZ, primary_key=True, nullable=False)
    player_id = Column(String(100), primary_key=True, nullable=False)
    action_type = Column(String(50), nullable=False)
    value = Column(Float, default=0.0)
    metadata = Column(JSONB)
    
    # 성능 최적화 인덱스
    __table_args__ = (
        # 플레이어별 시간 역순 인덱스 (최신 액션 빠른 조회)
        Index('idx_player_time_desc', 'player_id', 'time', postgresql_ops={'time': 'DESC'}),
        # 액션 타입별 시간 인덱스 (타입별 통계)  
        Index('idx_action_type_time', 'action_type', 'time'),
        # 복합 인덱스 (플레이어 + 액션타입 + 시간)
        Index('idx_player_action_time', 'player_id', 'action_type', 'time'),
        # 값 범위 쿼리용 인덱스
        Index('idx_value_time', 'value', 'time'),
    )

class ViolationTimeseries(TimescaleBase):
    """위반 사항 시계열 테이블"""
    __tablename__ = "violations_ts"
    
    time = Column(TIMESTAMPTZ, primary_key=True, nullable=False)
    player_id = Column(String(100), primary_key=True, nullable=False) 
    violation_type = Column(String(50), nullable=False)
    severity = Column(Float, nullable=False)
    details = Column(JSONB)
    resolved = Column(Boolean, default=False)
    
    __table_args__ = (
        Index('idx_violation_player_time', 'player_id', 'time', postgresql_ops={'time': 'DESC'}),
        Index('idx_violation_type_time', 'violation_type', 'time'),
        Index('idx_violation_severity', 'severity', 'time'),
    )

class PlayerSummary(TimescaleBase):
    """
    플레이어 요약 정보 (일반 테이블)
    - 자주 조회되는 기본 정보만 저장
    - 메모리에서 캐시 가능한 크기 유지
    """
    __tablename__ = "player_summary"
    
    player_id = Column(String(100), primary_key=True)
    username = Column(String(50))
    created_at = Column(TIMESTAMPTZ, default=func.now())
    last_activity = Column(TIMESTAMPTZ, default=func.now())
    
    # 실시간 업데이트되는 요약 정보
    current_risk_score = Column(Float, default=0.0)
    total_actions_today = Column(Integer, default=0)
    total_violations_today = Column(Integer, default=0)
    
    # 상태 정보
    is_banned = Column(Boolean, default=False)
    ban_reason = Column(Text)
    ban_timestamp = Column(TIMESTAMPTZ)
    
    # 업데이트 시간
    updated_at = Column(TIMESTAMPTZ, default=func.now(), onupdate=func.now())

class ActionTypeStats(TimescaleBase):
    """액션 타입별 통계 (연속 집계뷰)"""
    __tablename__ = "action_type_stats"
    
    bucket = Column(TIMESTAMPTZ, primary_key=True)  # 1분 단위 버킷
    action_type = Column(String(50), primary_key=True)
    player_count = Column(Integer, default=0)
    total_actions = Column(Integer, default=0)
    avg_value = Column(Float, default=0.0)
    max_value = Column(Float, default=0.0)
    
    __table_args__ = (
        Index('idx_bucket_action', 'bucket', 'action_type'),
    )

# TimescaleDB 연속 집계뷰를 위한 SQL
CONTINUOUS_AGGREGATES_SQL = """
-- 1분 단위 액션 통계 연속 집계뷰
CREATE MATERIALIZED VIEW IF NOT EXISTS action_stats_1min
WITH (timescaledb.continuous) AS
SELECT
    time_bucket('1 minute', time) AS bucket,
    action_type,
    player_id,
    COUNT(*) as action_count,
    SUM(value) as total_value,
    AVG(value) as avg_value,
    MAX(value) as max_value
FROM player_actions_ts
GROUP BY bucket, action_type, player_id;

-- 1시간 단위 플레이어 활동 요약
CREATE MATERIALIZED VIEW IF NOT EXISTS player_activity_1hour  
WITH (timescaledb.continuous) AS
SELECT
    time_bucket('1 hour', time) AS bucket,
    player_id,
    COUNT(*) as total_actions,
    COUNT(DISTINCT action_type) as unique_action_types,
    SUM(value) as total_value
FROM player_actions_ts
GROUP BY bucket, player_id;

-- 실시간 업데이트 정책
SELECT add_continuous_aggregate_policy('action_stats_1min',
    start_offset => INTERVAL '1 hour',
    end_offset => INTERVAL '1 minute',
    schedule_interval => INTERVAL '1 minute');

SELECT add_continuous_aggregate_policy('player_activity_1hour',
    start_offset => INTERVAL '1 day', 
    end_offset => INTERVAL '1 hour',
    schedule_interval => INTERVAL '1 hour');
"""

# 데이터 보존 정책
RETENTION_POLICY_SQL = """
-- 원시 액션 데이터는 30일 보관
SELECT add_retention_policy('player_actions_ts', INTERVAL '30 days');

-- 위반 데이터는 1년 보관  
SELECT add_retention_policy('violations_ts', INTERVAL '1 year');

-- 압축 정책 (7일 이후 데이터 압축)
ALTER TABLE player_actions_ts SET (
    timescaledb.compress,
    timescaledb.compress_segmentby = 'player_id',
    timescaledb.compress_orderby = 'time'
);

SELECT add_compression_policy('player_actions_ts', INTERVAL '7 days');
"""

# 하이퍼테이블 생성 함수
def create_hypertables(engine):
    """TimescaleDB 하이퍼테이블 및 정책 생성"""
    with engine.connect() as conn:
        # 테이블 생성
        TimescaleBase.metadata.create_all(engine)
        
        # 하이퍼테이블 변환
        conn.execute(text("""
            SELECT create_hypertable('player_actions_ts', 'time', 
                chunk_time_interval => INTERVAL '1 day',
                if_not_exists => TRUE);
        """))
        
        conn.execute(text("""
            SELECT create_hypertable('violations_ts', 'time',
                chunk_time_interval => INTERVAL '1 week', 
                if_not_exists => TRUE);
        """))
        
        # 연속 집계뷰 생성
        conn.execute(text(CONTINUOUS_AGGREGATES_SQL))
        
        # 데이터 보존 정책 적용
        conn.execute(text(RETENTION_POLICY_SQL))
        
        conn.commit()
        print("TimescaleDB 하이퍼테이블 및 정책 생성 완료")

def get_table_stats(engine):
    """테이블 통계 조회"""
    with engine.connect() as conn:
        result = conn.execute(text("""
            SELECT 
                schemaname,
                tablename,
                pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) as size,
                pg_size_pretty(pg_relation_size(schemaname||'.'||tablename)) as table_size
            FROM pg_tables 
            WHERE tablename IN ('player_actions_ts', 'violations_ts', 'player_summary')
            ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC;
        """))
        
        return [dict(row) for row in result]