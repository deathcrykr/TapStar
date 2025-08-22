from pydantic import BaseModel, Field
from typing import Optional, Dict, Any, List
from datetime import datetime
from enum import Enum

class ActionType(str, Enum):
    REWARD_COLLECTION = "reward_collection"
    RESOURCE_GATHER = "resource_gather" 
    LEVEL_PROGRESS = "level_progress"
    PURCHASE = "purchase"
    MOVEMENT = "movement"
    COMBAT = "combat"
    TRADE = "trade"

class PlayerActionCreate(BaseModel):
    player_id: str = Field(..., description="Unique player identifier")
    username: Optional[str] = Field(None, description="Player username")
    action_type: str = Field(..., description="Type of action performed")
    value: float = Field(default=0.0, description="Numeric value associated with action")
    metadata: Optional[Dict[str, Any]] = Field(default=None, description="Additional action data")

class ViolationResponse(BaseModel):
    player_id: Optional[str] = None
    violation_type: str
    severity: float
    timestamp: datetime
    details: Dict[str, Any] = Field(default_factory=dict)

class PlayerRiskResponse(BaseModel):
    player_id: str
    risk_score: float
    is_banned: bool
    recent_violations: List[ViolationResponse]

class BanPlayerRequest(BaseModel):
    reason: str = Field(..., description="Reason for the ban")
    ban_type: str = Field(default="permanent", description="Type of ban (temporary/permanent)")
    banned_by: str = Field(..., description="Admin/system that issued the ban")
    unban_timestamp: Optional[datetime] = Field(None, description="When to unban (for temporary bans)")

class PlayerStatsResponse(BaseModel):
    player_id: str
    total_actions: int
    avg_session_duration: float
    total_violations: int
    highest_risk_score: float
    last_violation_timestamp: Optional[datetime]
    suspicious_patterns: List[str]
    
class AntiCheatConfigUpdate(BaseModel):
    config_key: str
    config_value: Any
    description: Optional[str] = None
    updated_by: str

class PlayerActionAnalysis(BaseModel):
    action_processed: bool
    violations_detected: int
    current_risk_score: float
    should_review: bool = Field(default=False)
    recommendations: List[str] = Field(default_factory=list)