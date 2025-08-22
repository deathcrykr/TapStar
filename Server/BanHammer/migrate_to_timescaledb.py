#!/usr/bin/env python3
"""
BanHammer 데이터 마이그레이션: SQLite → TimescaleDB
5천만명+ 대용량 데이터 처리를 위한 마이그레이션 스크립트
"""

import asyncio
import asyncpg
import sqlite3
import psycopg2
import os
import sys
from datetime import datetime, timedelta
import json
import logging
from typing import Dict, List, Tuple
from tqdm import tqdm

# 로깅 설정
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler('migration.log'),
        logging.StreamHandler()
    ]
)
logger = logging.getLogger(__name__)

class TimescaleMigration:
    """SQLite/PostgreSQL → TimescaleDB 마이그레이션"""
    
    def __init__(self, source_db_path: str, timescale_url: str):
        self.source_db_path = source_db_path
        self.timescale_url = timescale_url
        self.batch_size = 10000  # 배치 크기
        
    async def run_migration(self):
        """전체 마이그레이션 프로세스"""
        logger.info("🚀 TimescaleDB 마이그레이션 시작")
        
        try:
            # 1. TimescaleDB 연결 및 초기화
            await self.init_timescaledb()
            
            # 2. 기존 데이터 마이그레이션
            await self.migrate_players()
            await self.migrate_actions()
            await self.migrate_violations()
            
            # 3. 인덱스 및 연속 집계뷰 생성
            await self.create_continuous_aggregates()
            
            # 4. 데이터 검증
            await self.verify_migration()
            
            logger.info("✅ 마이그레이션 완료!")
            
        except Exception as e:
            logger.error(f"❌ 마이그레이션 실패: {e}")
            sys.exit(1)
    
    async def init_timescaledb(self):
        """TimescaleDB 초기화"""
        logger.info("TimescaleDB 초기화 중...")
        
        conn = await asyncpg.connect(self.timescale_url)
        
        try:
            # TimescaleDB 확장 설치
            await conn.execute("CREATE EXTENSION IF NOT EXISTS timescaledb;")
            
            # 기존 테이블 삭제 (재마이그레이션용)
            await conn.execute("DROP TABLE IF EXISTS player_actions_ts CASCADE;")
            await conn.execute("DROP TABLE IF EXISTS violations_ts CASCADE;")
            await conn.execute("DROP TABLE IF EXISTS player_summary CASCADE;")
            
            # 테이블 생성
            await self.create_tables(conn)
            
            # 하이퍼테이블 생성
            await conn.execute("""
                SELECT create_hypertable('player_actions_ts', 'time', 
                    chunk_time_interval => INTERVAL '1 day',
                    if_not_exists => TRUE);
            """)
            
            await conn.execute("""
                SELECT create_hypertable('violations_ts', 'time',
                    chunk_time_interval => INTERVAL '1 week', 
                    if_not_exists => TRUE);
            """)
            
            logger.info("✅ TimescaleDB 초기화 완료")
            
        finally:
            await conn.close()
    
    async def create_tables(self, conn: asyncpg.Connection):
        """테이블 생성"""
        # 플레이어 액션 하이퍼테이블
        await conn.execute("""
            CREATE TABLE player_actions_ts (
                time TIMESTAMPTZ NOT NULL,
                player_id TEXT NOT NULL,
                action_type TEXT NOT NULL,
                value DOUBLE PRECISION DEFAULT 0,
                metadata JSONB
            );
        """)
        
        # 위반 사항 하이퍼테이블
        await conn.execute("""
            CREATE TABLE violations_ts (
                time TIMESTAMPTZ NOT NULL,
                player_id TEXT NOT NULL,
                violation_type TEXT NOT NULL,
                severity DOUBLE PRECISION NOT NULL,
                details JSONB,
                resolved BOOLEAN DEFAULT FALSE
            );
        """)
        
        # 플레이어 요약 테이블
        await conn.execute("""
            CREATE TABLE player_summary (
                player_id TEXT PRIMARY KEY,
                username TEXT,
                created_at TIMESTAMPTZ DEFAULT NOW(),
                last_activity TIMESTAMPTZ DEFAULT NOW(),
                current_risk_score DOUBLE PRECISION DEFAULT 0.0,
                total_actions_today INTEGER DEFAULT 0,
                total_violations_today INTEGER DEFAULT 0,
                is_banned BOOLEAN DEFAULT FALSE,
                ban_reason TEXT,
                ban_timestamp TIMESTAMPTZ,
                updated_at TIMESTAMPTZ DEFAULT NOW()
            );
        """)
    
    async def migrate_players(self):
        """플레이어 데이터 마이그레이션"""
        logger.info("플레이어 데이터 마이그레이션 시작...")
        
        # SQLite 연결
        sqlite_conn = sqlite3.connect(self.source_db_path)
        sqlite_conn.row_factory = sqlite3.Row
        
        # TimescaleDB 연결
        ts_conn = await asyncpg.connect(self.timescale_url)
        
        try:
            # 플레이어 수 확인
            total_players = sqlite_conn.execute("SELECT COUNT(*) FROM players").fetchone()[0]
            logger.info(f"마이그레이션할 플레이어: {total_players:,}명")
            
            if total_players == 0:
                logger.info("마이그레이션할 플레이어가 없습니다.")
                return
            
            # 배치로 마이그레이션
            offset = 0
            progress_bar = tqdm(total=total_players, desc="플레이어")
            
            while offset < total_players:
                players = sqlite_conn.execute("""
                    SELECT id, username, created_at, last_activity, risk_score, 
                           is_banned, ban_reason, ban_timestamp
                    FROM players
                    LIMIT ? OFFSET ?
                """, (self.batch_size, offset)).fetchall()
                
                if not players:
                    break
                
                # TimescaleDB에 배치 삽입
                values = []
                for player in players:
                    values.append((
                        player['id'],
                        player['username'],
                        self.parse_datetime(player['created_at']),
                        self.parse_datetime(player['last_activity']),
                        player['risk_score'] or 0.0,
                        0,  # total_actions_today
                        0,  # total_violations_today
                        bool(player['is_banned']),
                        player['ban_reason'],
                        self.parse_datetime(player['ban_timestamp']) if player['ban_timestamp'] else None
                    ))
                
                await ts_conn.executemany("""
                    INSERT INTO player_summary (
                        player_id, username, created_at, last_activity, 
                        current_risk_score, total_actions_today, total_violations_today,
                        is_banned, ban_reason, ban_timestamp
                    ) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
                """, values)
                
                offset += self.batch_size
                progress_bar.update(len(players))
            
            progress_bar.close()
            logger.info("✅ 플레이어 마이그레이션 완료")
            
        finally:
            sqlite_conn.close()
            await ts_conn.close()
    
    async def migrate_actions(self):
        """액션 데이터 마이그레이션"""
        logger.info("액션 데이터 마이그레이션 시작...")
        
        sqlite_conn = sqlite3.connect(self.source_db_path)
        sqlite_conn.row_factory = sqlite3.Row
        ts_conn = await asyncpg.connect(self.timescale_url)
        
        try:
            # 액션 수 확인
            total_actions = sqlite_conn.execute("SELECT COUNT(*) FROM player_actions").fetchone()[0]
            logger.info(f"마이그레이션할 액션: {total_actions:,}개")
            
            if total_actions == 0:
                logger.info("마이그레이션할 액션이 없습니다.")
                return
            
            # 메모리 효율적인 배치 처리
            offset = 0
            progress_bar = tqdm(total=total_actions, desc="액션")
            
            while offset < total_actions:
                actions = sqlite_conn.execute("""
                    SELECT player_id, action_type, timestamp, value, metadata
                    FROM player_actions
                    ORDER BY timestamp
                    LIMIT ? OFFSET ?
                """, (self.batch_size, offset)).fetchall()
                
                if not actions:
                    break
                
                # TimescaleDB에 배치 삽입
                values = []
                for action in actions:
                    metadata = None
                    if action['metadata']:
                        try:
                            metadata = json.loads(action['metadata'])
                        except:
                            metadata = {"raw": action['metadata']}
                    
                    values.append((
                        self.parse_datetime(action['timestamp']),
                        action['player_id'],
                        action['action_type'],
                        action['value'] or 0.0,
                        json.dumps(metadata) if metadata else None
                    ))
                
                await ts_conn.executemany("""
                    INSERT INTO player_actions_ts (time, player_id, action_type, value, metadata)
                    VALUES ($1, $2, $3, $4, $5)
                """, values)
                
                offset += self.batch_size
                progress_bar.update(len(actions))
                
                # 메모리 정리
                if offset % (self.batch_size * 10) == 0:
                    await asyncio.sleep(0.1)  # 잠시 대기
            
            progress_bar.close()
            logger.info("✅ 액션 마이그레이션 완료")
            
        finally:
            sqlite_conn.close()
            await ts_conn.close()
    
    async def migrate_violations(self):
        """위반 데이터 마이그레이션"""
        logger.info("위반 데이터 마이그레이션 시작...")
        
        sqlite_conn = sqlite3.connect(self.source_db_path)
        sqlite_conn.row_factory = sqlite3.Row
        ts_conn = await asyncpg.connect(self.timescale_url)
        
        try:
            total_violations = sqlite_conn.execute("SELECT COUNT(*) FROM violations").fetchone()[0]
            logger.info(f"마이그레이션할 위반: {total_violations:,}개")
            
            if total_violations == 0:
                logger.info("마이그레이션할 위반이 없습니다.")
                return
            
            offset = 0
            progress_bar = tqdm(total=total_violations, desc="위반")
            
            while offset < total_violations:
                violations = sqlite_conn.execute("""
                    SELECT player_id, violation_type, severity, timestamp, details, resolved
                    FROM violations
                    ORDER BY timestamp
                    LIMIT ? OFFSET ?
                """, (self.batch_size, offset)).fetchall()
                
                if not violations:
                    break
                
                values = []
                for violation in violations:
                    details = None
                    if violation['details']:
                        try:
                            details = json.loads(violation['details'])
                        except:
                            details = {"raw": violation['details']}
                    
                    values.append((
                        self.parse_datetime(violation['timestamp']),
                        violation['player_id'],
                        violation['violation_type'],
                        violation['severity'],
                        json.dumps(details) if details else None,
                        bool(violation['resolved'])
                    ))
                
                await ts_conn.executemany("""
                    INSERT INTO violations_ts (time, player_id, violation_type, severity, details, resolved)
                    VALUES ($1, $2, $3, $4, $5, $6)
                """, values)
                
                offset += self.batch_size
                progress_bar.update(len(violations))
            
            progress_bar.close()
            logger.info("✅ 위반 마이그레이션 완료")
            
        finally:
            sqlite_conn.close()
            await ts_conn.close()
    
    async def create_continuous_aggregates(self):
        """연속 집계뷰 및 인덱스 생성"""
        logger.info("연속 집계뷰 생성 중...")
        
        conn = await asyncpg.connect(self.timescale_url)
        
        try:
            # 인덱스 생성
            await conn.execute("""
                CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_player_time_desc 
                ON player_actions_ts (player_id, time DESC);
            """)
            
            await conn.execute("""
                CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_action_type_time
                ON player_actions_ts (action_type, time);
            """)
            
            # 연속 집계뷰 생성
            await conn.execute("""
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
            """)
            
            # 자동 업데이트 정책
            await conn.execute("""
                SELECT add_continuous_aggregate_policy('action_stats_1min',
                    start_offset => INTERVAL '1 hour',
                    end_offset => INTERVAL '1 minute',
                    schedule_interval => INTERVAL '1 minute');
            """)
            
            # 데이터 보존 정책
            await conn.execute("""
                SELECT add_retention_policy('player_actions_ts', INTERVAL '90 days');
            """)
            
            await conn.execute("""
                SELECT add_retention_policy('violations_ts', INTERVAL '1 year');
            """)
            
            logger.info("✅ 연속 집계뷰 생성 완료")
            
        finally:
            await conn.close()
    
    async def verify_migration(self):
        """마이그레이션 검증"""
        logger.info("마이그레이션 데이터 검증 중...")
        
        sqlite_conn = sqlite3.connect(self.source_db_path)
        ts_conn = await asyncpg.connect(self.timescale_url)
        
        try:
            # 플레이어 수 비교
            sqlite_players = sqlite_conn.execute("SELECT COUNT(*) FROM players").fetchone()[0]
            ts_players = await ts_conn.fetchval("SELECT COUNT(*) FROM player_summary")
            
            # 액션 수 비교
            sqlite_actions = sqlite_conn.execute("SELECT COUNT(*) FROM player_actions").fetchone()[0]
            ts_actions = await ts_conn.fetchval("SELECT COUNT(*) FROM player_actions_ts")
            
            # 위반 수 비교
            sqlite_violations = sqlite_conn.execute("SELECT COUNT(*) FROM violations").fetchone()[0]
            ts_violations = await ts_conn.fetchval("SELECT COUNT(*) FROM violations_ts")
            
            # 결과 출력
            logger.info("📊 마이그레이션 검증 결과:")
            logger.info(f"  플레이어: {sqlite_players:,} → {ts_players:,} ({'✅' if sqlite_players == ts_players else '❌'})")
            logger.info(f"  액션: {sqlite_actions:,} → {ts_actions:,} ({'✅' if sqlite_actions == ts_actions else '❌'})")
            logger.info(f"  위반: {sqlite_violations:,} → {ts_violations:,} ({'✅' if sqlite_violations == ts_violations else '❌'})")
            
            # 샘플 데이터 검증
            sample_player = await ts_conn.fetchrow("SELECT * FROM player_summary LIMIT 1")
            sample_action = await ts_conn.fetchrow("SELECT * FROM player_actions_ts LIMIT 1")
            
            if sample_player and sample_action:
                logger.info("✅ 샘플 데이터 검증 성공")
            else:
                logger.warning("⚠️ 샘플 데이터가 없습니다")
                
        finally:
            sqlite_conn.close()
            await ts_conn.close()
    
    def parse_datetime(self, dt_str) -> datetime:
        """날짜 문자열 파싱"""
        if not dt_str:
            return datetime.now()
        
        try:
            # SQLite datetime 형식들 지원
            formats = [
                '%Y-%m-%d %H:%M:%S.%f',
                '%Y-%m-%d %H:%M:%S',
                '%Y-%m-%d',
            ]
            
            for fmt in formats:
                try:
                    return datetime.strptime(dt_str, fmt)
                except ValueError:
                    continue
            
            # Unix timestamp 시도
            return datetime.fromtimestamp(float(dt_str))
            
        except:
            return datetime.now()

async def main():
    """메인 함수"""
    # 환경변수에서 DB URL 읽기
    source_db = os.getenv("SOURCE_DB_PATH", "./banhammer.db")
    timescale_url = os.getenv("TIMESCALEDB_URL", "postgresql://user:password@localhost:5432/banhammer_ts")
    
    if not os.path.exists(source_db):
        logger.error(f"소스 데이터베이스가 존재하지 않습니다: {source_db}")
        sys.exit(1)
    
    print(f"""
╔══════════════════════════════════════════════════════════════╗
║                   BanHammer → TimescaleDB                    ║
║                    대용량 데이터 마이그레이션                    ║
╠══════════════════════════════════════════════════════════════╣
║  소스: {source_db:<50} ║
║  대상: TimescaleDB (5천만명+ 지원)                           ║
╚══════════════════════════════════════════════════════════════╝
    """)
    
    # 마이그레이션 실행
    migration = TimescaleMigration(source_db, timescale_url)
    await migration.run_migration()

if __name__ == "__main__":
    asyncio.run(main())