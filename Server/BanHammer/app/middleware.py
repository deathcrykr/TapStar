from fastapi import Request, Response
from starlette.middleware.base import BaseHTTPMiddleware
from starlette.responses import JSONResponse
import time
import logging
from typing import Callable
from .dependencies import get_anti_cheat_engine

logger = logging.getLogger(__name__)

class AntiCheatMiddleware(BaseHTTPMiddleware):
    """
    Middleware that can intercept requests and apply anti-cheat checks
    before they reach the endpoints.
    """
    
    def __init__(self, app, rate_limit_enabled: bool = True):
        super().__init__(app)
        self.rate_limit_enabled = rate_limit_enabled
        self.request_counts = {}  # Simple in-memory rate limiting
        
    async def dispatch(self, request: Request, call_next: Callable) -> Response:
        start_time = time.time()
        
        # Extract player ID from request if available
        player_id = await self._extract_player_id(request)
        
        # Apply rate limiting if enabled and player ID is available
        if self.rate_limit_enabled and player_id:
            if await self._is_rate_limited(player_id, request):
                return JSONResponse(
                    status_code=429,
                    content={"error": "Rate limit exceeded", "player_id": player_id}
                )
        
        # Process the request
        response = await call_next(request)
        
        # Log request processing time
        process_time = time.time() - start_time
        response.headers["X-Process-Time"] = str(process_time)
        
        # Log suspicious activity if processing took too long
        if process_time > 5.0:  # More than 5 seconds
            logger.warning(
                f"Slow request detected: {request.url.path} took {process_time:.2f}s"
                f" for player {player_id}"
            )
        
        return response
    
    async def _extract_player_id(self, request: Request) -> str:
        """Extract player ID from request headers, query params, or body."""
        # Check headers first
        player_id = request.headers.get("X-Player-ID")
        if player_id:
            return player_id
        
        # Check query parameters
        player_id = request.query_params.get("player_id")
        if player_id:
            return player_id
        
        # For POST requests, try to extract from body
        if request.method == "POST" and request.url.path.startswith("/api/"):
            try:
                # This is a simplified extraction - in reality you'd want to
                # parse the body more carefully based on content type
                body = await request.body()
                if b"player_id" in body:
                    # Extract player_id from JSON body (simplified)
                    import json
                    try:
                        data = json.loads(body)
                        return data.get("player_id")
                    except:
                        pass
            except:
                pass
        
        return None
    
    async def _is_rate_limited(self, player_id: str, request: Request) -> bool:
        """Simple rate limiting check."""
        current_time = time.time()
        window_start = current_time - 60  # 1 minute window
        
        if player_id not in self.request_counts:
            self.request_counts[player_id] = []
        
        # Clean old requests
        self.request_counts[player_id] = [
            req_time for req_time in self.request_counts[player_id]
            if req_time > window_start
        ]
        
        # Add current request
        self.request_counts[player_id].append(current_time)
        
        # Check rate limits based on endpoint
        endpoint = request.url.path
        limit = self._get_rate_limit_for_endpoint(endpoint)
        
        return len(self.request_counts[player_id]) > limit
    
    def _get_rate_limit_for_endpoint(self, endpoint: str) -> int:
        """Get rate limit for specific endpoint."""
        if endpoint.startswith("/api/action"):
            return 60  # 60 actions per minute max
        elif endpoint.startswith("/api/player"):
            return 30  # 30 player queries per minute
        else:
            return 100  # Default limit


class SecurityHeadersMiddleware(BaseHTTPMiddleware):
    """Add security headers to responses."""
    
    async def dispatch(self, request: Request, call_next: Callable) -> Response:
        response = await call_next(request)
        
        # Add security headers
        response.headers["X-Content-Type-Options"] = "nosniff"
        response.headers["X-Frame-Options"] = "DENY" 
        response.headers["X-XSS-Protection"] = "1; mode=block"
        response.headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains"
        
        return response


class RequestLoggingMiddleware(BaseHTTPMiddleware):
    """Log all requests for audit purposes."""
    
    async def dispatch(self, request: Request, call_next: Callable) -> Response:
        # Extract basic request info
        client_ip = request.client.host if request.client else "unknown"
        user_agent = request.headers.get("user-agent", "unknown")
        player_id = request.headers.get("X-Player-ID", "anonymous")
        
        # Log request
        logger.info(
            f"Request: {request.method} {request.url.path} "
            f"from {client_ip} (Player: {player_id}, UA: {user_agent[:50]})"
        )
        
        response = await call_next(request)
        
        # Log response
        logger.info(
            f"Response: {response.status_code} for {request.method} {request.url.path} "
            f"(Player: {player_id})"
        )
        
        return response