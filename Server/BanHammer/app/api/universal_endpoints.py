from fastapi import APIRouter, Depends, HTTPException, BackgroundTasks, Query
from sqlalchemy.orm import Session
from typing import Dict, Any, List, Optional
import time
import logging
from pydantic import BaseModel

from ..core.universal_anti_cheat import UniversalAntiCheatEngine, UniversalPlayerAction
from ..core.game_profiles import GameProfile, GameGenre, ActionDefinition, DetectionRule, ActionCategory
from ..plugins.plugin_system import PluginManager
from ..dependencies import get_db
from ..models.database import Player, Violation, PlayerAction as DBPlayerAction

logger = logging.getLogger(__name__)

router = APIRouter()

# 글로벌 인스턴스
_universal_engine = None
_plugin_manager = None

def get_universal_engine() -> UniversalAntiCheatEngine:
    """범용 치팅 탐지 엔진 인스턴스 반환"""
    global _universal_engine
    if _universal_engine is None:
        _universal_engine = UniversalAntiCheatEngine()
    return _universal_engine

def get_plugin_manager() -> PluginManager:
    """플러그인 매니저 인스턴스 반환"""
    global _plugin_manager
    if _plugin_manager is None:
        _plugin_manager = PluginManager()
        _plugin_manager.load_all_plugins()
    return _plugin_manager

# Pydantic 모델들
class UniversalActionCreate(BaseModel):
    player_id: str
    game_id: str
    action_type: str
    value: Any = None
    metadata: Dict[str, Any] = {}
    session_id: Optional[str] = None
    client_info: Dict[str, Any] = {}

class GameRegistration(BaseModel):
    game_id: str
    game_name: str
    genre: str
    description: str = ""

class ActionDefinitionCreate(BaseModel):
    name: str
    category: str
    description: str
    value_type: str = "float"
    value_range: Optional[List[float]] = None
    required_fields: List[str] = []
    optional_fields: List[str] = []
    metadata_schema: Dict[str, Any] = {}

class DetectionRuleCreate(BaseModel):
    rule_id: str
    name: str
    description: str
    action_types: List[str]
    rule_type: str
    parameters: Dict[str, Any]
    severity: float
    enabled: bool = True

@router.post("/games/register")
async def register_game(
    registration: GameRegistration,
    engine: UniversalAntiCheatEngine = Depends(get_universal_engine)
):
    """새 게임 등록"""
    try:
        profile = engine.register_game(
            game_id=registration.game_id,
            game_name=registration.game_name,
            genre=registration.genre,
            description=registration.description
        )
        
        return {
            "message": f"게임 '{registration.game_name}' 등록 완료",
            "game_id": registration.game_id,
            "genre": profile.genre.value,
            "default_actions": len(profile.actions),
            "default_rules": len(profile.detection_rules)
        }
        
    except Exception as e:
        logger.error(f"게임 등록 실패: {e}")
        raise HTTPException(status_code=500, detail=f"게임 등록 실패: {str(e)}")

@router.get("/games")
async def list_games(
    engine: UniversalAntiCheatEngine = Depends(get_universal_engine)
):
    """등록된 게임 목록 조회"""
    return {"games": engine.list_games()}

@router.get("/games/{game_id}")
async def get_game_profile(
    game_id: str,
    engine: UniversalAntiCheatEngine = Depends(get_universal_engine)
):
    """게임 프로파일 상세 조회"""
    profile = engine.get_game_profile(game_id)
    if not profile:
        raise HTTPException(status_code=404, detail="게임을 찾을 수 없습니다")
    
    return {
        "game_id": profile.game_id,
        "game_name": profile.game_name,
        "genre": profile.genre.value,
        "description": profile.description,
        "actions": {
            name: {
                "name": action.name,
                "category": action.category.value,
                "description": action.description,
                "value_type": action.value_type,
                "value_range": action.value_range
            }
            for name, action in profile.actions.items()
        },
        "detection_rules": [
            {
                "rule_id": rule.rule_id,
                "name": rule.name,
                "description": rule.description,
                "action_types": rule.action_types,
                "rule_type": rule.rule_type,
                "severity": rule.severity,
                "enabled": rule.enabled
            }
            for rule in profile.detection_rules
        ],
        "thresholds": {
            "auto_ban_threshold": profile.auto_ban_threshold,
            "warning_threshold": profile.warning_threshold
        }
    }

@router.post("/action")
async def submit_universal_action(
    action_data: UniversalActionCreate,
    background_tasks: BackgroundTasks,
    db: Session = Depends(get_db),
    engine: UniversalAntiCheatEngine = Depends(get_universal_engine),
    plugin_manager: PluginManager = Depends(get_plugin_manager)
):
    """범용 플레이어 액션 제출 및 분석"""
    # UniversalPlayerAction 생성
    action = UniversalPlayerAction(
        player_id=action_data.player_id,
        game_id=action_data.game_id,
        action_type=action_data.action_type,
        timestamp=time.time(),
        value=action_data.value,
        metadata=action_data.metadata,
        session_id=action_data.session_id,
        client_info=action_data.client_info
    )
    
    # 플러그인을 통한 전처리
    processed_action = await plugin_manager.process_action_through_plugins(action)
    
    # 기본 치팅 탐지
    violations = await engine.analyze_action(processed_action)
    
    # 플러그인을 통한 추가 탐지
    profile = engine.get_game_profile(action_data.game_id)
    if profile:
        recent_actions = list(engine.player_actions.get(action_data.game_id, {}).get(action_data.player_id, []))
        plugin_violations = await plugin_manager.detect_violations_with_plugins(recent_actions, profile)
        violations.extend(plugin_violations)
    
    # 데이터베이스에 저장
    try:
        # 플레이어 확인/생성
        player = db.query(Player).filter(Player.id == action_data.player_id).first()
        if not player:
            player = Player(
                id=action_data.player_id,
                username=action_data.player_id  # 기본값
            )
            db.add(player)
        
        # 액션 저장
        db_action = DBPlayerAction(
            player_id=action_data.player_id,
            action_type=action_data.action_type,
            timestamp=datetime.fromtimestamp(processed_action.timestamp),
            value=float(action_data.value) if isinstance(action_data.value, (int, float)) else 0
        )
        db_action.set_metadata({
            **action_data.metadata,
            "game_id": action_data.game_id,
            "session_id": action_data.session_id
        })
        db.add(db_action)
        
        # 위반 사항 저장
        for violation in violations:
            db_violation = Violation(
                player_id=violation.player_id,
                violation_type=violation.violation_type.value,
                severity=violation.severity,
                timestamp=datetime.fromtimestamp(violation.timestamp)
            )
            db_violation.set_details({
                **violation.details,
                "game_id": violation.game_id,
                "rule_id": violation.rule_id
            })
            db.add(db_violation)
        
        # 위험도 업데이트
        player.risk_score = await engine.get_player_risk_score(action_data.game_id, action_data.player_id)
        player.last_activity = datetime.now()
        
        db.commit()
        
    except Exception as e:
        logger.error(f"데이터베이스 저장 실패: {e}")
        db.rollback()
    
    # 알림 발송 (백그라운드)
    for violation in violations:
        if violation.severity >= 4.0:  # 심각한 위반만 알림
            background_tasks.add_task(
                plugin_manager.send_notifications,
                violation,
                {"profile": profile}
            )
    
    # 자동 차단 검사
    should_ban, ban_reason = await engine.should_ban_player(action_data.game_id, action_data.player_id)
    
    return {
        "action_processed": True,
        "game_id": action_data.game_id,
        "violations_detected": len(violations),
        "current_risk_score": await engine.get_player_risk_score(action_data.game_id, action_data.player_id),
        "should_ban": should_ban,
        "ban_reason": ban_reason if should_ban else None,
        "violations": [
            {
                "type": v.violation_type.value,
                "rule_id": v.rule_id,
                "severity": v.severity,
                "details": v.details
            }
            for v in violations
        ]
    }

@router.get("/games/{game_id}/player/{player_id}/risk")
async def get_player_risk(
    game_id: str,
    player_id: str,
    engine: UniversalAntiCheatEngine = Depends(get_universal_engine),
    plugin_manager: PluginManager = Depends(get_plugin_manager)
):
    """플레이어 위험도 및 분석 조회"""
    # 기본 위험도
    risk_score = await engine.get_player_risk_score(game_id, player_id)
    
    # 플레이어 액션 조회
    recent_actions = list(engine.player_actions.get(game_id, {}).get(player_id, []))
    
    # 플러그인을 통한 행동 분석
    behavior_analysis = await plugin_manager.analyze_player_behavior(player_id, game_id, recent_actions)
    
    # 자동 차단 권장 여부
    should_ban, ban_reason = await engine.should_ban_player(game_id, player_id)
    
    return {
        "player_id": player_id,
        "game_id": game_id,
        "risk_score": risk_score,
        "should_ban": should_ban,
        "ban_reason": ban_reason if should_ban else None,
        "recent_actions_count": len(recent_actions),
        "behavior_analysis": behavior_analysis,
        "last_action_time": recent_actions[-1].timestamp if recent_actions else None
    }

@router.post("/games/{game_id}/actions")
async def add_action_definition(
    game_id: str,
    action_def: ActionDefinitionCreate,
    engine: UniversalAntiCheatEngine = Depends(get_universal_engine)
):
    """게임에 새 액션 정의 추가"""
    profile = engine.get_game_profile(game_id)
    if not profile:
        raise HTTPException(status_code=404, detail="게임을 찾을 수 없습니다")
    
    try:
        # ActionDefinition 생성
        action_definition = ActionDefinition(
            name=action_def.name,
            category=ActionCategory(action_def.category.lower()),
            description=action_def.description,
            value_type=action_def.value_type,
            value_range=tuple(action_def.value_range) if action_def.value_range else None,
            required_fields=set(action_def.required_fields),
            optional_fields=set(action_def.optional_fields),
            metadata_schema=action_def.metadata_schema
        )
        
        profile.add_action(action_definition)
        engine.profile_manager.save_profile(game_id)
        
        return {
            "message": f"액션 '{action_def.name}' 추가 완료",
            "action_name": action_def.name,
            "category": action_def.category
        }
        
    except ValueError as e:
        raise HTTPException(status_code=400, detail=f"잘못된 카테고리: {action_def.category}")
    except Exception as e:
        logger.error(f"액션 정의 추가 실패: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@router.post("/games/{game_id}/rules")
async def add_detection_rule(
    game_id: str,
    rule_def: DetectionRuleCreate,
    engine: UniversalAntiCheatEngine = Depends(get_universal_engine)
):
    """게임에 새 탐지 규칙 추가"""
    profile = engine.get_game_profile(game_id)
    if not profile:
        raise HTTPException(status_code=404, detail="게임을 찾을 수 없습니다")
    
    try:
        # DetectionRule 생성
        detection_rule = DetectionRule(
            rule_id=rule_def.rule_id,
            name=rule_def.name,
            description=rule_def.description,
            action_types=rule_def.action_types,
            rule_type=rule_def.rule_type,
            parameters=rule_def.parameters,
            severity=rule_def.severity,
            enabled=rule_def.enabled
        )
        
        profile.add_detection_rule(detection_rule)
        engine.profile_manager.save_profile(game_id)
        
        return {
            "message": f"탐지 규칙 '{rule_def.name}' 추가 완료",
            "rule_id": rule_def.rule_id,
            "rule_type": rule_def.rule_type
        }
        
    except Exception as e:
        logger.error(f"탐지 규칙 추가 실패: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@router.get("/plugins")
async def get_plugin_status(
    plugin_manager: PluginManager = Depends(get_plugin_manager)
):
    """플러그인 상태 조회"""
    return plugin_manager.get_plugin_status()

@router.post("/plugins/{plugin_name}/enable")
async def enable_plugin(
    plugin_name: str,
    plugin_manager: PluginManager = Depends(get_plugin_manager)
):
    """플러그인 활성화"""
    success = plugin_manager.enable_plugin(plugin_name)
    if success:
        return {"message": f"플러그인 '{plugin_name}' 활성화됨"}
    else:
        raise HTTPException(status_code=404, detail="플러그인을 찾을 수 없습니다")

@router.post("/plugins/{plugin_name}/disable")
async def disable_plugin(
    plugin_name: str,
    plugin_manager: PluginManager = Depends(get_plugin_manager)
):
    """플러그인 비활성화"""
    success = plugin_manager.disable_plugin(plugin_name)
    if success:
        return {"message": f"플러그인 '{plugin_name}' 비활성화됨"}
    else:
        raise HTTPException(status_code=404, detail="플러그인을 찾을 수 없습니다")

@router.get("/games/{game_id}/analytics")
async def get_game_analytics(
    game_id: str,
    days: int = Query(default=7, ge=1, le=30),
    db: Session = Depends(get_db)
):
    """게임별 분석 데이터"""
    from datetime import datetime, timedelta
    
    cutoff_date = datetime.now() - timedelta(days=days)
    
    # 기본 통계
    total_players = db.query(Player).join(DBPlayerAction).filter(
        DBPlayerAction.timestamp >= cutoff_date,
        DBPlayerAction.metadata.like(f'%"game_id": "{game_id}"%')
    ).distinct().count()
    
    total_actions = db.query(DBPlayerAction).filter(
        DBPlayerAction.timestamp >= cutoff_date,
        DBPlayerAction.metadata.like(f'%"game_id": "{game_id}"%')
    ).count()
    
    total_violations = db.query(Violation).filter(
        Violation.timestamp >= cutoff_date,
        Violation.details.like(f'%"game_id": "{game_id}"%')
    ).count()
    
    # 위반 유형별 통계
    violation_types = db.query(
        Violation.violation_type,
        db.func.count(Violation.id).label('count')
    ).filter(
        Violation.timestamp >= cutoff_date,
        Violation.details.like(f'%"game_id": "{game_id}"%')
    ).group_by(Violation.violation_type).all()
    
    return {
        "game_id": game_id,
        "period_days": days,
        "statistics": {
            "total_players": total_players,
            "total_actions": total_actions,
            "total_violations": total_violations,
            "violations_per_player": round(total_violations / total_players, 2) if total_players > 0 else 0
        },
        "violation_breakdown": {vt: count for vt, count in violation_types},
        "generated_at": datetime.now().isoformat()
    }

@router.get("/supported-genres")
async def get_supported_genres():
    """지원되는 게임 장르 목록"""
    return {
        "genres": [
            {
                "id": genre.value,
                "name": genre.name,
                "description": _get_genre_description(genre)
            }
            for genre in GameGenre
        ]
    }

def _get_genre_description(genre: GameGenre) -> str:
    """장르 설명 반환"""
    descriptions = {
        GameGenre.MMORPG: "대규모 다중 접속 온라인 역할 수행 게임",
        GameGenre.MOBILE_RPG: "모바일 역할 수행 게임",
        GameGenre.FPS: "1인칭 슈팅 게임",
        GameGenre.MOBA: "멀티플레이어 온라인 배틀 아레나",
        GameGenre.BATTLE_ROYALE: "배틀로얄 게임",
        GameGenre.STRATEGY: "전략 게임",
        GameGenre.PUZZLE: "퍼즐 게임",
        GameGenre.IDLE: "방치형 게임",
        GameGenre.CARD: "카드 게임",
        GameGenre.RACING: "레이싱 게임",
        GameGenre.SPORTS: "스포츠 게임",
        GameGenre.CASINO: "카지노 게임",
        GameGenre.PLATFORM: "플랫폼 게임",
        GameGenre.SANDBOX: "샌드박스 게임"
    }
    return descriptions.get(genre, "기타 게임")

# 필요한 import 추가
from datetime import datetime