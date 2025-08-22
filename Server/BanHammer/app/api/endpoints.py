from fastapi import APIRouter, Depends, HTTPException, BackgroundTasks
from sqlalchemy.orm import Session
from typing import List, Optional, Dict, Any
from datetime import datetime, timedelta
import time
import json

from ..core.anti_cheat import AntiCheatEngine, PlayerAction, ViolationType
from ..models.database import Player, Violation, PlayerAction as DBPlayerAction, BanHistory
from ..dependencies import get_db, get_anti_cheat_engine
from ..schemas import (
    PlayerActionCreate, ViolationResponse, PlayerRiskResponse,
    BanPlayerRequest, PlayerStatsResponse
)

router = APIRouter()

@router.post("/action", response_model=Dict[str, Any])
async def submit_player_action(
    action_data: PlayerActionCreate,
    background_tasks: BackgroundTasks,
    db: Session = Depends(get_db),
    anti_cheat: AntiCheatEngine = Depends(get_anti_cheat_engine)
):
    """
    Submit a player action for anti-cheat analysis.
    
    This endpoint receives game actions and processes them through
    the anti-cheat engine to detect violations.
    """
    try:
        # Validate metadata size to prevent DoS attacks
        if action_data.metadata and len(str(action_data.metadata)) > 10000:
            raise HTTPException(status_code=400, detail="Metadata too large")
        
        # Create PlayerAction object
        action = PlayerAction(
            player_id=action_data.player_id,
            action_type=action_data.action_type,
            timestamp=time.time(),
            value=action_data.value,
            metadata=action_data.metadata or {}
        )
    
        # Ensure player exists
        player = db.query(Player).filter(Player.id == action.player_id).first()
        if not player:
            player = Player(
                id=action.player_id,
                username=action_data.username or action.player_id
            )
            db.add(player)
            try:
                db.commit()
            except Exception as e:
                db.rollback()
                raise HTTPException(status_code=500, detail="Failed to create player")
        
        # Store action in database
        db_action = DBPlayerAction(
            player_id=action.player_id,
            action_type=action.action_type,
            timestamp=datetime.fromtimestamp(action.timestamp),
            value=action.value
        )
        db_action.set_metadata(action.metadata)
        db.add(db_action)
        
        # Analyze action for violations
        violations = await anti_cheat.analyze_action(action)
        
        # Store violations in database
        for violation in violations:
            db_violation = Violation(
                player_id=violation.player_id,
                violation_type=violation.violation_type.value,
                severity=violation.severity,
                timestamp=datetime.fromtimestamp(violation.timestamp)
            )
            db_violation.set_details(violation.details)
            db.add(db_violation)
        
        # Update player risk score
        player.risk_score = await anti_cheat.get_player_risk_score(action.player_id)
        player.last_activity = datetime.now()
        
        try:
            db.commit()
        except Exception as e:
            db.rollback()
            raise HTTPException(status_code=500, detail="Failed to save action data")
        
        # Check if player should be banned
        should_ban, ban_reason = await anti_cheat.should_ban_player(action.player_id)
        if should_ban and not player.is_banned:
            background_tasks.add_task(ban_player_task, player.id, ban_reason, db)
        
        return {
            "action_processed": True,
            "violations_detected": len(violations),
            "current_risk_score": player.risk_score,
            "violations": [
                {
                    "type": v.violation_type.value,
                    "severity": v.severity,
                    "details": v.details
                } for v in violations
            ]
        }
    
    except HTTPException:
        raise
    except Exception as e:
        import logging
        logger = logging.getLogger(__name__)
        logger.error(f"Error processing action for player {action_data.player_id}: {str(e)}")
        raise HTTPException(status_code=500, detail="Internal server error")

@router.get("/player/{player_id}/risk", response_model=PlayerRiskResponse)
async def get_player_risk(
    player_id: str,
    db: Session = Depends(get_db),
    anti_cheat: AntiCheatEngine = Depends(get_anti_cheat_engine)
):
    """Get current risk score and analysis for a player."""
    player = db.query(Player).filter(Player.id == player_id).first()
    if not player:
        raise HTTPException(status_code=404, detail="Player not found")
    
    # Get current risk score from anti-cheat engine
    risk_score = await anti_cheat.get_player_risk_score(player_id)
    
    # Get recent violations
    recent_violations = db.query(Violation).filter(
        Violation.player_id == player_id,
        Violation.timestamp >= datetime.now() - timedelta(hours=24)
    ).order_by(Violation.timestamp.desc()).limit(10).all()
    
    return PlayerRiskResponse(
        player_id=player_id,
        risk_score=risk_score,
        is_banned=player.is_banned,
        recent_violations=[
            ViolationResponse(
                violation_type=v.violation_type,
                severity=v.severity,
                timestamp=v.timestamp,
                details=v.get_details()
            ) for v in recent_violations
        ]
    )

@router.get("/player/{player_id}/violations", response_model=List[ViolationResponse])
async def get_player_violations(
    player_id: str,
    limit: int = 50,
    db: Session = Depends(get_db)
):
    """Get violation history for a player."""
    violations = db.query(Violation).filter(
        Violation.player_id == player_id
    ).order_by(Violation.timestamp.desc()).limit(limit).all()
    
    return [
        ViolationResponse(
            violation_type=v.violation_type,
            severity=v.severity,
            timestamp=v.timestamp,
            details=v.get_details()
        ) for v in violations
    ]

@router.post("/player/{player_id}/ban")
async def ban_player(
    player_id: str,
    ban_request: BanPlayerRequest,
    db: Session = Depends(get_db)
):
    """Manually ban a player."""
    player = db.query(Player).filter(Player.id == player_id).first()
    if not player:
        raise HTTPException(status_code=404, detail="Player not found")
    
    if player.is_banned:
        raise HTTPException(status_code=400, detail="Player is already banned")
    
    # Ban the player
    player.is_banned = True
    player.ban_reason = ban_request.reason
    player.ban_timestamp = datetime.now()
    
    # Create ban history record
    ban_record = BanHistory(
        player_id=player_id,
        ban_type=ban_request.ban_type,
        ban_reason=ban_request.reason,
        banned_by=ban_request.banned_by,
        unban_timestamp=ban_request.unban_timestamp
    )
    
    db.add(ban_record)
    db.commit()
    
    return {"message": f"Player {player_id} has been banned", "reason": ban_request.reason}

@router.post("/player/{player_id}/unban")
async def unban_player(
    player_id: str,
    db: Session = Depends(get_db)
):
    """Unban a player."""
    player = db.query(Player).filter(Player.id == player_id).first()
    if not player:
        raise HTTPException(status_code=404, detail="Player not found")
    
    if not player.is_banned:
        raise HTTPException(status_code=400, detail="Player is not banned")
    
    # Unban the player
    player.is_banned = False
    player.ban_reason = None
    player.ban_timestamp = None
    
    # Update ban history
    active_ban = db.query(BanHistory).filter(
        BanHistory.player_id == player_id,
        BanHistory.is_active == True
    ).first()
    
    if active_ban:
        active_ban.is_active = False
        active_ban.unban_timestamp = datetime.now()
    
    db.commit()
    
    return {"message": f"Player {player_id} has been unbanned"}

@router.get("/violations/recent", response_model=List[ViolationResponse])
async def get_recent_violations(
    hours: int = 24,
    severity_threshold: float = 2.0,
    limit: int = 100,
    db: Session = Depends(get_db)
):
    """Get recent violations above a certain severity threshold."""
    since = datetime.now() - timedelta(hours=hours)
    
    violations = db.query(Violation).filter(
        Violation.timestamp >= since,
        Violation.severity >= severity_threshold
    ).order_by(Violation.timestamp.desc()).limit(limit).all()
    
    return [
        ViolationResponse(
            player_id=v.player_id,
            violation_type=v.violation_type,
            severity=v.severity,
            timestamp=v.timestamp,
            details=v.get_details()
        ) for v in violations
    ]

@router.get("/stats/overview")
async def get_overview_stats(
    db: Session = Depends(get_db)
):
    """Get overview statistics for the anti-cheat system."""
    # Total players
    total_players = db.query(Player).count()
    
    # Banned players
    banned_players = db.query(Player).filter(Player.is_banned == True).count()
    
    # Recent violations (last 24 hours)
    recent_violations = db.query(Violation).filter(
        Violation.timestamp >= datetime.now() - timedelta(hours=24)
    ).count()
    
    # High-risk players (risk score > 5.0)
    high_risk_players = db.query(Player).filter(Player.risk_score > 5.0).count()
    
    # Violation types breakdown (last 7 days)
    violation_types = db.query(
        Violation.violation_type,
        db.func.count(Violation.id).label('count')
    ).filter(
        Violation.timestamp >= datetime.now() - timedelta(days=7)
    ).group_by(Violation.violation_type).all()
    
    return {
        "total_players": total_players,
        "banned_players": banned_players,
        "recent_violations_24h": recent_violations,
        "high_risk_players": high_risk_players,
        "violation_breakdown": {vt: count for vt, count in violation_types}
    }

async def ban_player_task(player_id: str, reason: str, db: Session):
    """Background task to ban a player."""
    player = db.query(Player).filter(Player.id == player_id).first()
    if player and not player.is_banned:
        player.is_banned = True
        player.ban_reason = reason
        player.ban_timestamp = datetime.now()
        
        # Create ban history record
        ban_record = BanHistory(
            player_id=player_id,
            ban_type="automatic",
            ban_reason=reason,
            banned_by="system"
        )
        
        db.add(ban_record)
        db.commit()