from sqlalchemy import create_engine
from sqlalchemy.orm import sessionmaker, Session
from sqlalchemy.pool import StaticPool
import redis.asyncio as redis
import os
from typing import Generator

from .core.anti_cheat import AntiCheatEngine
from .models.database import Base

# Database configuration
DATABASE_URL = os.getenv("DATABASE_URL", "sqlite:///./banhammer.db")

# Create engine with connection pooling for SQLite
if DATABASE_URL.startswith("sqlite"):
    engine = create_engine(
        DATABASE_URL,
        connect_args={"check_same_thread": False},
        poolclass=StaticPool,
    )
else:
    engine = create_engine(DATABASE_URL)

# Create tables
Base.metadata.create_all(bind=engine)

# Session factory
SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)

# Redis configuration
REDIS_URL = os.getenv("REDIS_URL", "redis://localhost:6379/0")

# Global instances
_redis_client = None
_anti_cheat_engine = None

async def get_redis_client():
    """Get Redis client instance."""
    global _redis_client
    if _redis_client is None:
        try:
            _redis_client = await redis.from_url(REDIS_URL)
        except Exception:
            # Redis is optional, continue without it
            _redis_client = None
    return _redis_client

def get_db() -> Generator[Session, None, None]:
    """Get database session."""
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()

async def get_anti_cheat_engine() -> AntiCheatEngine:
    """Get anti-cheat engine instance."""
    global _anti_cheat_engine
    if _anti_cheat_engine is None:
        redis_client = await get_redis_client()
        _anti_cheat_engine = AntiCheatEngine(redis_client=redis_client)
    return _anti_cheat_engine