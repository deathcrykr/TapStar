from fastapi import FastAPI, BackgroundTasks
from fastapi.middleware.cors import CORSMiddleware
import logging
import asyncio
from contextlib import asynccontextmanager

from app.api.endpoints import router
from app.api.ml_endpoints import router as ml_router
from app.middleware import AntiCheatMiddleware, SecurityHeadersMiddleware, RequestLoggingMiddleware
from app.config import settings
from app.dependencies import get_anti_cheat_engine

# Configure logging
logging.basicConfig(
    level=getattr(logging, settings.log_level.upper()),
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)

logger = logging.getLogger(__name__)

# Background task for cleaning up old data
async def cleanup_task():
    """Background task to clean up old anti-cheat data."""
    while True:
        try:
            anti_cheat = await get_anti_cheat_engine()
            await anti_cheat.cleanup_old_data()
            logger.info("Completed anti-cheat data cleanup")
        except Exception as e:
            logger.error(f"Error in cleanup task: {e}")
        
        # Wait 1 hour before next cleanup
        await asyncio.sleep(3600)

@asynccontextmanager
async def lifespan(app: FastAPI):
    # Startup
    logger.info("Starting BanHammer Anti-Cheat API")
    
    # Start background cleanup task
    cleanup_task_handle = asyncio.create_task(cleanup_task())
    
    yield
    
    # Shutdown
    logger.info("Shutting down BanHammer Anti-Cheat API")
    cleanup_task_handle.cancel()
    try:
        await cleanup_task_handle
    except asyncio.CancelledError:
        pass

# Create FastAPI app
app = FastAPI(
    title=settings.api_title,
    version=settings.api_version,
    description="""
    BanHammer는 게임 서버를 위한 강력한 치팅 탐지 및 방지 시스템입니다.
    
    ## 주요 기능
    
    * **실시간 행동 분석**: 플레이어 행동을 실시간으로 모니터링하고 분석
    * **다층 탐지 시스템**: 
        - 속도 제한 및 임계값 검사
        - 행동 패턴 분석 
        - 통계적 이상 탐지
        - 봇 행동 탐지
    * **자동화된 대응**: 위험도에 따른 자동 경고 및 차단
    * **실시간 모니터링**: 대시보드를 통한 실시간 상황 파악
    
    ## 탐지 가능한 치팅 유형
    
    * 보상 수집 과다 (예: 1분 내 과도한 게임 머니 획득)
    * 자동화 봇 (완벽한 타이밍, 24시간 연속 활동)
    * 불가능한 행동 (물리 법칙 위반, 순간이동 등)
    * 경제 시스템 악용 (복제 버그, 거래 조작 등)
    """,
    debug=settings.debug_mode,
    lifespan=lifespan
)

# Add CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # Configure appropriately for production
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Add custom middleware
if settings.log_requests:
    app.add_middleware(RequestLoggingMiddleware)

app.add_middleware(SecurityHeadersMiddleware)

if settings.rate_limiting_enabled:
    app.add_middleware(AntiCheatMiddleware, rate_limit_enabled=True)

# Include API routes
app.include_router(router, prefix="/api", tags=["anti-cheat"])
app.include_router(ml_router, prefix="/api/ml", tags=["machine-learning"])

@app.get("/", tags=["health"])
async def root():
    """Health check endpoint."""
    return {
        "service": "BanHammer Anti-Cheat API",
        "version": settings.api_version,
        "status": "healthy"
    }

@app.get("/health", tags=["health"])
async def health_check():
    """Detailed health check with system status."""
    try:
        # Check database connectivity
        from app.dependencies import SessionLocal
        db = SessionLocal()
        db.execute("SELECT 1")
        db.close()
        db_status = "healthy"
    except Exception as e:
        db_status = f"error: {str(e)}"
    
    try:
        # Check Redis connectivity
        from app.dependencies import get_redis_client
        redis_client = await get_redis_client()
        if redis_client:
            await redis_client.ping()
            redis_status = "healthy"
        else:
            redis_status = "disabled"
    except Exception as e:
        redis_status = f"error: {str(e)}"
    
    return {
        "service": "BanHammer Anti-Cheat API",
        "version": settings.api_version,
        "status": "healthy",
        "components": {
            "database": db_status,
            "redis": redis_status,
        }
    }

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(
        "main:app",
        host="0.0.0.0",
        port=8000,
        reload=settings.debug_mode,
        log_level=settings.log_level.lower()
    )