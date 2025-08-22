from typing import Dict, List, Any, Optional, Set
from dataclasses import dataclass, field
from enum import Enum
import json
from pathlib import Path

class GameGenre(Enum):
    """게임 장르 분류"""
    MMORPG = "mmorpg"
    MOBILE_RPG = "mobile_rpg"
    FPS = "fps"
    MOBA = "moba"
    BATTLE_ROYALE = "battle_royale"
    STRATEGY = "strategy"
    PUZZLE = "puzzle"
    IDLE = "idle"
    CARD = "card"
    RACING = "racing"
    SPORTS = "sports"
    CASINO = "casino"
    PLATFORM = "platform"
    SANDBOX = "sandbox"

class ActionCategory(Enum):
    """행동 카테고리"""
    ECONOMY = "economy"          # 경제 관련 (구매, 판매, 거래)
    PROGRESSION = "progression"   # 진행 관련 (레벨업, 경험치)
    RESOURCE = "resource"        # 자원 관련 (수집, 채굴, 농장)
    COMBAT = "combat"           # 전투 관련 (공격, 스킬, 데미지)
    MOVEMENT = "movement"       # 이동 관련 (위치, 속도)
    SOCIAL = "social"           # 소셜 관련 (채팅, 친구, 길드)
    CUSTOMIZATION = "customization"  # 커스터마이징 (아이템, 캐릭터)
    ACHIEVEMENT = "achievement"  # 성취 관련 (퀘스트, 업적)
    COMPETITIVE = "competitive"  # 경쟁 관련 (랭킹, PvP)
    GACHA = "gacha"            # 가챠, 랜덤박스
    MINI_GAME = "mini_game"     # 미니게임
    ADMIN = "admin"            # 관리자 행동

@dataclass
class ActionDefinition:
    """액션 정의"""
    name: str
    category: ActionCategory
    description: str
    value_type: str = "float"  # float, int, bool, string
    value_range: Optional[tuple] = None  # (min, max)
    required_fields: Set[str] = field(default_factory=set)
    optional_fields: Set[str] = field(default_factory=set)
    metadata_schema: Dict[str, Any] = field(default_factory=dict)

@dataclass 
class DetectionRule:
    """탐지 규칙 정의"""
    rule_id: str
    name: str
    description: str
    action_types: List[str]  # 적용되는 액션 타입들
    rule_type: str  # rate_limit, threshold, pattern, statistical, ml
    parameters: Dict[str, Any]
    severity: float  # 1-5
    enabled: bool = True

@dataclass
class GameProfile:
    """게임별 설정 프로파일"""
    game_id: str
    game_name: str
    genre: GameGenre
    description: str
    
    # 액션 정의
    actions: Dict[str, ActionDefinition] = field(default_factory=dict)
    
    # 탐지 규칙
    detection_rules: List[DetectionRule] = field(default_factory=list)
    
    # 특화 설정
    rate_limits: Dict[str, Dict[str, Any]] = field(default_factory=dict)
    thresholds: Dict[str, float] = field(default_factory=dict)
    ml_features: List[str] = field(default_factory=list)
    
    # 업무 설정
    auto_ban_threshold: float = 8.0
    warning_threshold: float = 5.0
    monitoring_window_hours: int = 24
    
    def add_action(self, action_def: ActionDefinition):
        """액션 정의 추가"""
        self.actions[action_def.name] = action_def
    
    def add_detection_rule(self, rule: DetectionRule):
        """탐지 규칙 추가"""
        self.detection_rules.append(rule)
    
    def get_actions_by_category(self, category: ActionCategory) -> List[ActionDefinition]:
        """카테고리별 액션 조회"""
        return [action for action in self.actions.values() if action.category == category]
    
    def validate_action(self, action_type: str, value: Any, metadata: Dict[str, Any]) -> List[str]:
        """액션 유효성 검사"""
        errors = []
        
        if action_type not in self.actions:
            errors.append(f"Unknown action type: {action_type}")
            return errors
        
        action_def = self.actions[action_type]
        
        # 값 타입 검사
        if action_def.value_type == "float" and not isinstance(value, (int, float)):
            errors.append(f"Value must be numeric for action {action_type}")
        elif action_def.value_type == "int" and not isinstance(value, int):
            errors.append(f"Value must be integer for action {action_type}")
        elif action_def.value_type == "bool" and not isinstance(value, bool):
            errors.append(f"Value must be boolean for action {action_type}")
        
        # 값 범위 검사
        if action_def.value_range and isinstance(value, (int, float)):
            min_val, max_val = action_def.value_range
            if not (min_val <= value <= max_val):
                errors.append(f"Value {value} out of range [{min_val}, {max_val}] for {action_type}")
        
        # 필수 메타데이터 검사
        for required_field in action_def.required_fields:
            if required_field not in metadata:
                errors.append(f"Required metadata field '{required_field}' missing for {action_type}")
        
        return errors

class GameProfileManager:
    """게임 프로파일 관리자"""
    
    def __init__(self, profiles_dir: str = "game_profiles"):
        self.profiles_dir = Path(profiles_dir)
        self.profiles_dir.mkdir(exist_ok=True)
        self.profiles: Dict[str, GameProfile] = {}
        self._load_all_profiles()
    
    def create_profile(self, game_id: str, game_name: str, genre: GameGenre, 
                      description: str = "") -> GameProfile:
        """새 게임 프로파일 생성"""
        profile = GameProfile(
            game_id=game_id,
            game_name=game_name,
            genre=genre,
            description=description
        )
        
        # 장르별 기본 설정 적용
        self._apply_genre_defaults(profile)
        
        self.profiles[game_id] = profile
        self.save_profile(game_id)
        
        return profile
    
    def get_profile(self, game_id: str) -> Optional[GameProfile]:
        """프로파일 조회"""
        return self.profiles.get(game_id)
    
    def save_profile(self, game_id: str):
        """프로파일 저장"""
        if game_id not in self.profiles:
            return
        
        profile = self.profiles[game_id]
        profile_data = self._serialize_profile(profile)
        
        file_path = self.profiles_dir / f"{game_id}.json"
        with open(file_path, 'w', encoding='utf-8') as f:
            json.dump(profile_data, f, indent=2, ensure_ascii=False)
    
    def load_profile(self, game_id: str) -> Optional[GameProfile]:
        """프로파일 로드"""
        file_path = self.profiles_dir / f"{game_id}.json"
        if not file_path.exists():
            return None
        
        with open(file_path, 'r', encoding='utf-8') as f:
            profile_data = json.load(f)
        
        profile = self._deserialize_profile(profile_data)
        self.profiles[game_id] = profile
        return profile
    
    def _load_all_profiles(self):
        """모든 프로파일 로드"""
        for profile_file in self.profiles_dir.glob("*.json"):
            game_id = profile_file.stem
            self.load_profile(game_id)
    
    def list_profiles(self) -> List[Dict[str, Any]]:
        """프로파일 목록 조회"""
        return [
            {
                "game_id": profile.game_id,
                "game_name": profile.game_name,
                "genre": profile.genre.value,
                "description": profile.description,
                "action_count": len(profile.actions),
                "rule_count": len(profile.detection_rules)
            }
            for profile in self.profiles.values()
        ]
    
    def _apply_genre_defaults(self, profile: GameProfile):
        """장르별 기본 설정 적용"""
        if profile.genre == GameGenre.MMORPG:
            self._setup_mmorpg_defaults(profile)
        elif profile.genre == GameGenre.MOBILE_RPG:
            self._setup_mobile_rpg_defaults(profile)
        elif profile.genre == GameGenre.FPS:
            self._setup_fps_defaults(profile)
        elif profile.genre == GameGenre.MOBA:
            self._setup_moba_defaults(profile)
        elif profile.genre == GameGenre.IDLE:
            self._setup_idle_defaults(profile)
        elif profile.genre == GameGenre.CARD:
            self._setup_card_defaults(profile)
        elif profile.genre == GameGenre.PUZZLE:
            self._setup_puzzle_defaults(profile)
        # 추가 장르들...
    
    def _setup_mmorpg_defaults(self, profile: GameProfile):
        """MMORPG 기본 설정"""
        # 액션 정의
        actions = [
            ActionDefinition("kill_monster", ActionCategory.COMBAT, "몬스터 처치", "int", (1, 1000)),
            ActionDefinition("gain_exp", ActionCategory.PROGRESSION, "경험치 획득", "float", (1, 10000)),
            ActionDefinition("level_up", ActionCategory.PROGRESSION, "레벨업", "int", (1, 500)),
            ActionDefinition("acquire_item", ActionCategory.RESOURCE, "아이템 획득", "int", (1, 100)),
            ActionDefinition("trade_item", ActionCategory.ECONOMY, "아이템 거래", "float", (1, 1000000)),
            ActionDefinition("complete_quest", ActionCategory.ACHIEVEMENT, "퀘스트 완료", "int", (1, 50)),
            ActionDefinition("move_location", ActionCategory.MOVEMENT, "위치 이동", "float", (0, 10000)),
            ActionDefinition("use_skill", ActionCategory.COMBAT, "스킬 사용", "int", (1, 100)),
            ActionDefinition("join_guild", ActionCategory.SOCIAL, "길드 가입", "bool"),
            ActionDefinition("pvp_battle", ActionCategory.COMPETITIVE, "PvP 전투", "int", (1, 100))
        ]
        
        for action in actions:
            profile.add_action(action)
        
        # 탐지 규칙
        rules = [
            DetectionRule("mmorpg_exp_farm", "경험치 파밍 탐지", "비정상적인 경험치 획득", 
                         ["gain_exp"], "rate_limit", {"max_per_minute": 20, "max_value_per_minute": 50000}, 3.0),
            DetectionRule("mmorpg_item_dupe", "아이템 복사 탐지", "짧은 시간 내 동일 아이템 대량 획득",
                         ["acquire_item"], "pattern", {"duplicate_threshold": 10, "time_window": 60}, 4.5),
            DetectionRule("mmorpg_teleport", "순간이동 탐지", "물리적으로 불가능한 이동",
                         ["move_location"], "threshold", {"max_speed": 1000}, 5.0),
            DetectionRule("mmorpg_skill_spam", "스킬 스팸 탐지", "쿨다운 무시 스킬 연발",
                         ["use_skill"], "rate_limit", {"max_per_minute": 60}, 2.5)
        ]
        
        for rule in rules:
            profile.add_detection_rule(rule)
    
    def _setup_mobile_rpg_defaults(self, profile: GameProfile):
        """모바일 RPG 기본 설정"""
        actions = [
            ActionDefinition("auto_battle", ActionCategory.COMBAT, "자동 전투", "int", (1, 1000)),
            ActionDefinition("collect_reward", ActionCategory.RESOURCE, "보상 수집", "float", (1, 100000)),
            ActionDefinition("upgrade_equipment", ActionCategory.CUSTOMIZATION, "장비 강화", "int", (1, 20)),
            ActionDefinition("summon_hero", ActionCategory.GACHA, "영웅 소환", "int", (1, 100)),
            ActionDefinition("complete_stage", ActionCategory.PROGRESSION, "스테이지 클리어", "int", (1, 10000)),
            ActionDefinition("claim_daily", ActionCategory.ACHIEVEMENT, "일일 보상 수령", "int", (1, 50)),
            ActionDefinition("spend_currency", ActionCategory.ECONOMY, "재화 소모", "float", (1, 1000000)),
            ActionDefinition("idle_farming", ActionCategory.RESOURCE, "방치 파밍", "float", (1, 100000))
        ]
        
        for action in actions:
            profile.add_action(action)
        
        rules = [
            DetectionRule("mobile_reward_abuse", "보상 수집 남용", "과도한 보상 수집", 
                         ["collect_reward"], "rate_limit", {"max_per_minute": 15, "max_value_per_minute": 500000}, 3.5),
            DetectionRule("mobile_gacha_exploit", "가챠 익스플로잇", "비정상적인 소환 패턴",
                         ["summon_hero"], "statistical", {"z_threshold": 3.0}, 4.0),
            DetectionRule("mobile_stage_rush", "스테이지 러쉬 탐지", "불가능한 속도의 스테이지 클리어",
                         ["complete_stage"], "rate_limit", {"max_per_minute": 10}, 4.5)
        ]
        
        for rule in rules:
            profile.add_detection_rule(rule)
    
    def _setup_fps_defaults(self, profile: GameProfile):
        """FPS 기본 설정"""
        actions = [
            ActionDefinition("player_kill", ActionCategory.COMBAT, "플레이어 킬", "int", (1, 100)),
            ActionDefinition("headshot", ActionCategory.COMBAT, "헤드샷", "int", (1, 50)),
            ActionDefinition("move_position", ActionCategory.MOVEMENT, "위치 이동", "float"),
            ActionDefinition("fire_weapon", ActionCategory.COMBAT, "무기 발사", "int", (1, 1000)),
            ActionDefinition("reload_weapon", ActionCategory.COMBAT, "재장전", "int", (1, 100)),
            ActionDefinition("match_result", ActionCategory.COMPETITIVE, "매치 결과", "string"),
            ActionDefinition("accuracy_shot", ActionCategory.COMBAT, "명중률 기록", "float", (0, 1))
        ]
        
        for action in actions:
            profile.add_action(action)
        
        rules = [
            DetectionRule("fps_aimbot", "에임봇 탐지", "비정상적인 명중률", 
                         ["headshot", "accuracy_shot"], "statistical", {"accuracy_threshold": 0.95, "headshot_ratio": 0.8}, 5.0),
            DetectionRule("fps_wallhack", "월핵 탐지", "벽 너머 킬 패턴",
                         ["player_kill"], "pattern", {"suspicious_angle_threshold": 160}, 4.5),
            DetectionRule("fps_speed_hack", "스피드핵 탐지", "비정상적인 이동 속도",
                         ["move_position"], "threshold", {"max_speed": 1000}, 4.0)
        ]
        
        for rule in rules:
            profile.add_detection_rule(rule)
    
    def _setup_idle_defaults(self, profile: GameProfile):
        """방치형 게임 기본 설정"""
        actions = [
            ActionDefinition("idle_income", ActionCategory.RESOURCE, "방치 수익", "float", (0, 1000000)),
            ActionDefinition("prestige", ActionCategory.PROGRESSION, "환생/전직", "int", (1, 1000)),
            ActionDefinition("upgrade_building", ActionCategory.CUSTOMIZATION, "건물 업그레이드", "int", (1, 10000)),
            ActionDefinition("collect_offline", ActionCategory.RESOURCE, "오프라인 수집", "float", (0, 10000000)),
            ActionDefinition("watch_ad", ActionCategory.RESOURCE, "광고 시청", "int", (1, 100)),
            ActionDefinition("purchase_boost", ActionCategory.ECONOMY, "부스터 구매", "float", (1, 1000))
        ]
        
        for action in actions:
            profile.add_action(action)
        
        rules = [
            DetectionRule("idle_income_hack", "수익 조작 탐지", "비정상적인 방치 수익", 
                         ["idle_income"], "regression", {"residual_threshold": 3.0}, 4.0),
            DetectionRule("idle_offline_exploit", "오프라인 익스플로잇", "과도한 오프라인 수익",
                         ["collect_offline"], "threshold", {"time_based_limit": True}, 3.5),
            DetectionRule("idle_ad_spam", "광고 스팸 탐지", "광고 시청 남용",
                         ["watch_ad"], "rate_limit", {"max_per_hour": 50}, 2.0)
        ]
        
        for rule in rules:
            profile.add_detection_rule(rule)
    
    def _setup_card_defaults(self, profile: GameProfile):
        """카드 게임 기본 설정"""
        actions = [
            ActionDefinition("play_card", ActionCategory.COMPETITIVE, "카드 플레이", "string"),
            ActionDefinition("win_match", ActionCategory.COMPETITIVE, "매치 승리", "bool"),
            ActionDefinition("earn_coins", ActionCategory.ECONOMY, "코인 획득", "float", (1, 10000)),
            ActionDefinition("open_pack", ActionCategory.GACHA, "팩 개봉", "int", (1, 100)),
            ActionDefinition("craft_card", ActionCategory.CUSTOMIZATION, "카드 제작", "int", (1, 10)),
            ActionDefinition("rank_change", ActionCategory.COMPETITIVE, "랭크 변화", "int", (-1000, 1000))
        ]
        
        for action in actions:
            profile.add_action(action)
        
        rules = [
            DetectionRule("card_win_rate", "승률 조작 탐지", "비정상적인 승률", 
                         ["win_match"], "statistical", {"win_rate_threshold": 0.95}, 4.0),
            DetectionRule("card_pack_exploit", "팩 개봉 익스플로잇", "과도한 팩 개봉",
                         ["open_pack"], "rate_limit", {"max_per_minute": 10}, 3.0),
            DetectionRule("card_rank_boost", "랭크 부스팅 탐지", "급격한 랭크 상승",
                         ["rank_change"], "threshold", {"max_increase_per_hour": 500}, 3.5)
        ]
        
        for rule in rules:
            profile.add_detection_rule(rule)
    
    def _setup_puzzle_defaults(self, profile: GameProfile):
        """퍼즐 게임 기본 설정"""
        actions = [
            ActionDefinition("solve_puzzle", ActionCategory.ACHIEVEMENT, "퍼즐 해결", "int", (1, 10000)),
            ActionDefinition("use_hint", ActionCategory.RESOURCE, "힌트 사용", "int", (1, 100)),
            ActionDefinition("complete_level", ActionCategory.PROGRESSION, "레벨 완료", "int", (1, 10000)),
            ActionDefinition("earn_stars", ActionCategory.ACHIEVEMENT, "별 획득", "int", (1, 3)),
            ActionDefinition("buy_moves", ActionCategory.ECONOMY, "이동 횟수 구매", "int", (1, 100)),
            ActionDefinition("solve_time", ActionCategory.COMPETITIVE, "해결 시간", "float", (1, 3600))
        ]
        
        for action in actions:
            profile.add_action(action)
        
        rules = [
            DetectionRule("puzzle_solve_speed", "퍼즐 해결 속도 탐지", "비인간적인 해결 속도", 
                         ["solve_time"], "threshold", {"min_solve_time": 5.0}, 4.0),
            DetectionRule("puzzle_perfect_score", "완벽 점수 탐지", "항상 완벽한 점수",
                         ["earn_stars"], "pattern", {"perfect_ratio": 0.95}, 3.5),
            DetectionRule("puzzle_level_skip", "레벨 스킵 탐지", "과도한 레벨 진행 속도",
                         ["complete_level"], "rate_limit", {"max_per_minute": 20}, 3.0)
        ]
        
        for rule in rules:
            profile.add_detection_rule(rule)
    
    def _serialize_profile(self, profile: GameProfile) -> Dict[str, Any]:
        """프로파일 직렬화"""
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
                    "value_range": action.value_range,
                    "required_fields": list(action.required_fields),
                    "optional_fields": list(action.optional_fields),
                    "metadata_schema": action.metadata_schema
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
                    "parameters": rule.parameters,
                    "severity": rule.severity,
                    "enabled": rule.enabled
                }
                for rule in profile.detection_rules
            ],
            "rate_limits": profile.rate_limits,
            "thresholds": profile.thresholds,
            "ml_features": profile.ml_features,
            "auto_ban_threshold": profile.auto_ban_threshold,
            "warning_threshold": profile.warning_threshold,
            "monitoring_window_hours": profile.monitoring_window_hours
        }
    
    def _deserialize_profile(self, data: Dict[str, Any]) -> GameProfile:
        """프로파일 역직렬화"""
        profile = GameProfile(
            game_id=data["game_id"],
            game_name=data["game_name"],
            genre=GameGenre(data["genre"]),
            description=data["description"],
            rate_limits=data.get("rate_limits", {}),
            thresholds=data.get("thresholds", {}),
            ml_features=data.get("ml_features", []),
            auto_ban_threshold=data.get("auto_ban_threshold", 8.0),
            warning_threshold=data.get("warning_threshold", 5.0),
            monitoring_window_hours=data.get("monitoring_window_hours", 24)
        )
        
        # 액션 복원
        for name, action_data in data.get("actions", {}).items():
            action = ActionDefinition(
                name=action_data["name"],
                category=ActionCategory(action_data["category"]),
                description=action_data["description"],
                value_type=action_data.get("value_type", "float"),
                value_range=tuple(action_data["value_range"]) if action_data.get("value_range") else None,
                required_fields=set(action_data.get("required_fields", [])),
                optional_fields=set(action_data.get("optional_fields", [])),
                metadata_schema=action_data.get("metadata_schema", {})
            )
            profile.actions[name] = action
        
        # 탐지 규칙 복원
        for rule_data in data.get("detection_rules", []):
            rule = DetectionRule(
                rule_id=rule_data["rule_id"],
                name=rule_data["name"],
                description=rule_data["description"],
                action_types=rule_data["action_types"],
                rule_type=rule_data["rule_type"],
                parameters=rule_data["parameters"],
                severity=rule_data["severity"],
                enabled=rule_data.get("enabled", True)
            )
            profile.detection_rules.append(rule)
        
        return profile