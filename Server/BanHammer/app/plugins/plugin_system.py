import importlib
import inspect
from abc import ABC, abstractmethod
from typing import Dict, List, Any, Optional, Type, Callable
from pathlib import Path
import logging
from dataclasses import dataclass

from ..core.universal_anti_cheat import UniversalPlayerAction, UniversalViolation
from ..core.game_profiles import GameProfile, DetectionRule

logger = logging.getLogger(__name__)

@dataclass
class PluginMetadata:
    """플러그인 메타데이터"""
    name: str
    version: str
    description: str
    author: str
    game_types: List[str]  # 지원하는 게임 타입들
    dependencies: List[str] = None
    
    def __post_init__(self):
        if self.dependencies is None:
            self.dependencies = []

class BasePlugin(ABC):
    """플러그인 베이스 클래스"""
    
    def __init__(self):
        self.metadata: Optional[PluginMetadata] = None
        self.enabled = True
    
    @abstractmethod
    def get_metadata(self) -> PluginMetadata:
        """플러그인 메타데이터 반환"""
        pass
    
    def initialize(self, config: Dict[str, Any] = None):
        """플러그인 초기화"""
        self.metadata = self.get_metadata()
        logger.info(f"Plugin {self.metadata.name} v{self.metadata.version} initialized")
    
    def cleanup(self):
        """플러그인 정리"""
        logger.info(f"Plugin {self.metadata.name} cleaned up")

class DetectionPlugin(BasePlugin):
    """탐지 플러그인"""
    
    @abstractmethod
    async def detect_violations(self, actions: List[UniversalPlayerAction], 
                              profile: GameProfile) -> List[UniversalViolation]:
        """위반 사항 탐지"""
        pass
    
    def get_custom_rules(self) -> List[DetectionRule]:
        """사용자 정의 탐지 규칙 반환"""
        return []

class DataProcessorPlugin(BasePlugin):
    """데이터 처리 플러그인"""
    
    @abstractmethod
    async def process_action(self, action: UniversalPlayerAction) -> UniversalPlayerAction:
        """액션 전처리"""
        pass
    
    async def post_process_violations(self, violations: List[UniversalViolation]) -> List[UniversalViolation]:
        """위반 사항 후처리 (옵션)"""
        return violations

class NotificationPlugin(BasePlugin):
    """알림 플러그인"""
    
    @abstractmethod
    async def send_notification(self, violation: UniversalViolation, context: Dict[str, Any]):
        """알림 발송"""
        pass

class AnalyticsPlugin(BasePlugin):
    """분석 플러그인"""
    
    @abstractmethod
    async def analyze_player_behavior(self, player_id: str, game_id: str, 
                                    actions: List[UniversalPlayerAction]) -> Dict[str, Any]:
        """플레이어 행동 분석"""
        pass
    
    def get_dashboard_metrics(self) -> Dict[str, Any]:
        """대시보드 메트릭 제공 (옵션)"""
        return {}

class PluginManager:
    """플러그인 관리자"""
    
    def __init__(self, plugins_dir: str = "plugins"):
        self.plugins_dir = Path(plugins_dir)
        self.plugins_dir.mkdir(exist_ok=True)
        
        self.detection_plugins: Dict[str, DetectionPlugin] = {}
        self.data_processor_plugins: Dict[str, DataProcessorPlugin] = {}
        self.notification_plugins: Dict[str, NotificationPlugin] = {}
        self.analytics_plugins: Dict[str, AnalyticsPlugin] = {}
        
        self.plugin_configs: Dict[str, Dict[str, Any]] = {}
        
    def load_plugin(self, plugin_path: str, config: Dict[str, Any] = None) -> bool:
        """플러그인 로드"""
        try:
            # 모듈 로드
            spec = importlib.util.spec_from_file_location("plugin", plugin_path)
            module = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(module)
            
            # 플러그인 클래스 찾기
            for name, obj in inspect.getmembers(module):
                if (inspect.isclass(obj) and 
                    issubclass(obj, BasePlugin) and 
                    obj != BasePlugin and
                    not inspect.isabstract(obj)):
                    
                    # 플러그인 인스턴스 생성
                    plugin_instance = obj()
                    plugin_instance.initialize(config)
                    
                    # 플러그인 타입에 따라 등록
                    self._register_plugin(plugin_instance)
                    
                    logger.info(f"Successfully loaded plugin: {plugin_instance.metadata.name}")
                    return True
                    
            logger.warning(f"No valid plugin class found in {plugin_path}")
            return False
            
        except Exception as e:
            logger.error(f"Failed to load plugin {plugin_path}: {e}")
            return False
    
    def _register_plugin(self, plugin: BasePlugin):
        """플러그인 등록"""
        plugin_name = plugin.metadata.name
        
        if isinstance(plugin, DetectionPlugin):
            self.detection_plugins[plugin_name] = plugin
        elif isinstance(plugin, DataProcessorPlugin):
            self.data_processor_plugins[plugin_name] = plugin
        elif isinstance(plugin, NotificationPlugin):
            self.notification_plugins[plugin_name] = plugin
        elif isinstance(plugin, AnalyticsPlugin):
            self.analytics_plugins[plugin_name] = plugin
    
    def load_all_plugins(self):
        """모든 플러그인 로드"""
        plugin_files = list(self.plugins_dir.glob("*.py"))
        
        for plugin_file in plugin_files:
            if plugin_file.name.startswith("_"):  # __init__.py 등 제외
                continue
                
            config = self.plugin_configs.get(plugin_file.stem, {})
            self.load_plugin(str(plugin_file), config)
    
    def unload_plugin(self, plugin_name: str):
        """플러그인 언로드"""
        # 모든 플러그인 저장소에서 제거
        for plugin_dict in [self.detection_plugins, self.data_processor_plugins,
                          self.notification_plugins, self.analytics_plugins]:
            if plugin_name in plugin_dict:
                plugin = plugin_dict[plugin_name]
                plugin.cleanup()
                del plugin_dict[plugin_name]
                logger.info(f"Unloaded plugin: {plugin_name}")
                return True
        
        logger.warning(f"Plugin not found: {plugin_name}")
        return False
    
    async def process_action_through_plugins(self, action: UniversalPlayerAction) -> UniversalPlayerAction:
        """액션을 데이터 처리 플러그인들을 통해 전처리"""
        processed_action = action
        
        for plugin in self.data_processor_plugins.values():
            if plugin.enabled:
                try:
                    processed_action = await plugin.process_action(processed_action)
                except Exception as e:
                    logger.error(f"Error in data processor plugin {plugin.metadata.name}: {e}")
        
        return processed_action
    
    async def detect_violations_with_plugins(self, actions: List[UniversalPlayerAction], 
                                           profile: GameProfile) -> List[UniversalViolation]:
        """탐지 플러그인들을 통해 위반 사항 탐지"""
        all_violations = []
        
        for plugin in self.detection_plugins.values():
            if plugin.enabled and self._is_plugin_applicable(plugin, profile):
                try:
                    violations = await plugin.detect_violations(actions, profile)
                    all_violations.extend(violations)
                except Exception as e:
                    logger.error(f"Error in detection plugin {plugin.metadata.name}: {e}")
        
        # 후처리 플러그인 적용
        for plugin in self.data_processor_plugins.values():
            if plugin.enabled:
                try:
                    all_violations = await plugin.post_process_violations(all_violations)
                except Exception as e:
                    logger.error(f"Error in post-processing plugin {plugin.metadata.name}: {e}")
        
        return all_violations
    
    async def send_notifications(self, violation: UniversalViolation, context: Dict[str, Any]):
        """알림 플러그인들을 통해 알림 발송"""
        for plugin in self.notification_plugins.values():
            if plugin.enabled:
                try:
                    await plugin.send_notification(violation, context)
                except Exception as e:
                    logger.error(f"Error in notification plugin {plugin.metadata.name}: {e}")
    
    async def analyze_player_behavior(self, player_id: str, game_id: str, 
                                    actions: List[UniversalPlayerAction]) -> Dict[str, Any]:
        """분석 플러그인들을 통해 플레이어 행동 분석"""
        analysis_results = {}
        
        for plugin_name, plugin in self.analytics_plugins.items():
            if plugin.enabled:
                try:
                    result = await plugin.analyze_player_behavior(player_id, game_id, actions)
                    analysis_results[plugin_name] = result
                except Exception as e:
                    logger.error(f"Error in analytics plugin {plugin.metadata.name}: {e}")
                    analysis_results[plugin_name] = {"error": str(e)}
        
        return analysis_results
    
    def _is_plugin_applicable(self, plugin: BasePlugin, profile: GameProfile) -> bool:
        """플러그인이 해당 게임에 적용 가능한지 확인"""
        if not plugin.metadata.game_types:
            return True  # 모든 게임에 적용 가능
        
        return (profile.genre.value in plugin.metadata.game_types or 
                "all" in plugin.metadata.game_types)
    
    def get_plugin_status(self) -> Dict[str, Dict[str, Any]]:
        """모든 플러그인 상태 조회"""
        status = {
            "detection_plugins": {},
            "data_processor_plugins": {},
            "notification_plugins": {},
            "analytics_plugins": {}
        }
        
        for category, plugins in [
            ("detection_plugins", self.detection_plugins),
            ("data_processor_plugins", self.data_processor_plugins),
            ("notification_plugins", self.notification_plugins),
            ("analytics_plugins", self.analytics_plugins)
        ]:
            for name, plugin in plugins.items():
                status[category][name] = {
                    "enabled": plugin.enabled,
                    "version": plugin.metadata.version,
                    "description": plugin.metadata.description,
                    "author": plugin.metadata.author,
                    "game_types": plugin.metadata.game_types
                }
        
        return status
    
    def enable_plugin(self, plugin_name: str) -> bool:
        """플러그인 활성화"""
        return self._set_plugin_status(plugin_name, True)
    
    def disable_plugin(self, plugin_name: str) -> bool:
        """플러그인 비활성화"""
        return self._set_plugin_status(plugin_name, False)
    
    def _set_plugin_status(self, plugin_name: str, enabled: bool) -> bool:
        """플러그인 활성화/비활성화"""
        for plugin_dict in [self.detection_plugins, self.data_processor_plugins,
                          self.notification_plugins, self.analytics_plugins]:
            if plugin_name in plugin_dict:
                plugin_dict[plugin_name].enabled = enabled
                logger.info(f"Plugin {plugin_name} {'enabled' if enabled else 'disabled'}")
                return True
        
        logger.warning(f"Plugin not found: {plugin_name}")
        return False
    
    def set_plugin_config(self, plugin_name: str, config: Dict[str, Any]):
        """플러그인 설정 업데이트"""
        self.plugin_configs[plugin_name] = config

# 기본 제공 플러그인들

class FPSDetectionPlugin(DetectionPlugin):
    """FPS 게임 전용 탐지 플러그인"""
    
    def get_metadata(self) -> PluginMetadata:
        return PluginMetadata(
            name="FPS Detection Plugin",
            version="1.0.0",
            description="FPS 게임의 에임봇, 월핵 등을 탐지합니다",
            author="BanHammer Team",
            game_types=["fps", "battle_royale"]
        )
    
    async def detect_violations(self, actions: List[UniversalPlayerAction], 
                              profile: GameProfile) -> List[UniversalViolation]:
        violations = []
        
        # 에임봇 탐지 로직
        headshot_actions = [a for a in actions if a.action_type == "headshot"]
        kill_actions = [a for a in actions if a.action_type == "player_kill"]
        
        if len(kill_actions) > 5 and len(headshot_actions) > 0:
            headshot_ratio = len(headshot_actions) / len(kill_actions)
            
            if headshot_ratio > 0.8:  # 80% 이상 헤드샷
                from ..core.universal_anti_cheat import UniversalViolation, ViolationType
                import time
                
                violation = UniversalViolation(
                    player_id=actions[0].player_id,
                    game_id=actions[0].game_id,
                    violation_type=ViolationType.CUSTOM_RULE,
                    rule_id="fps_aimbot_detection",
                    severity=4.5,
                    timestamp=time.time(),
                    details={
                        "headshot_ratio": headshot_ratio,
                        "total_kills": len(kill_actions),
                        "headshots": len(headshot_actions),
                        "detection_type": "aimbot"
                    },
                    action_context=headshot_actions[-3:] + kill_actions[-3:]
                )
                violations.append(violation)
        
        return violations

class DiscordNotificationPlugin(NotificationPlugin):
    """디스코드 알림 플러그인"""
    
    def __init__(self):
        super().__init__()
        self.webhook_url = None
    
    def get_metadata(self) -> PluginMetadata:
        return PluginMetadata(
            name="Discord Notification Plugin",
            version="1.0.0",
            description="디스코드 웹훅을 통해 치팅 탐지 알림을 발송합니다",
            author="BanHammer Team",
            game_types=["all"]
        )
    
    def initialize(self, config: Dict[str, Any] = None):
        super().initialize(config)
        if config:
            self.webhook_url = config.get("webhook_url")
    
    async def send_notification(self, violation: UniversalViolation, context: Dict[str, Any]):
        if not self.webhook_url:
            logger.warning("Discord webhook URL not configured")
            return
        
        # 실제 구현에서는 aiohttp 등을 사용하여 웹훅 호출
        message = {
            "content": f"🚨 **치팅 탐지 알림**\n"
                      f"게임: {violation.game_id}\n"
                      f"플레이어: {violation.player_id}\n"
                      f"위반 유형: {violation.violation_type.value}\n"
                      f"심각도: {violation.severity}/5.0\n"
                      f"시간: {violation.timestamp}"
        }
        
        logger.info(f"Discord 알림 전송 (시뮬레이션): {message['content']}")

class PlayerBehaviorAnalyticsPlugin(AnalyticsPlugin):
    """플레이어 행동 분석 플러그인"""
    
    def get_metadata(self) -> PluginMetadata:
        return PluginMetadata(
            name="Player Behavior Analytics",
            version="1.0.0",
            description="플레이어 행동 패턴을 분석하고 인사이트를 제공합니다",
            author="BanHammer Team",
            game_types=["all"]
        )
    
    async def analyze_player_behavior(self, player_id: str, game_id: str, 
                                    actions: List[UniversalPlayerAction]) -> Dict[str, Any]:
        if not actions:
            return {"error": "No actions to analyze"}
        
        # 기본 통계
        total_actions = len(actions)
        unique_action_types = len(set(a.action_type for a in actions))
        
        # 시간 분석
        if len(actions) > 1:
            time_span = actions[-1].timestamp - actions[0].timestamp
            actions_per_hour = (total_actions / time_span) * 3600 if time_span > 0 else 0
        else:
            actions_per_hour = 0
        
        # 액션 분포
        action_counts = {}
        for action in actions:
            action_counts[action.action_type] = action_counts.get(action.action_type, 0) + 1
        
        return {
            "player_id": player_id,
            "game_id": game_id,
            "total_actions": total_actions,
            "unique_action_types": unique_action_types,
            "actions_per_hour": round(actions_per_hour, 2),
            "action_distribution": action_counts,
            "analysis_timestamp": actions[-1].timestamp if actions else 0,
            "risk_indicators": self._calculate_risk_indicators(actions)
        }
    
    def _calculate_risk_indicators(self, actions: List[UniversalPlayerAction]) -> Dict[str, Any]:
        """위험 지표 계산"""
        indicators = {}
        
        # 액션 간격 분산 (봇 탐지용)
        if len(actions) > 3:
            intervals = []
            for i in range(1, len(actions)):
                interval = actions[i].timestamp - actions[i-1].timestamp
                intervals.append(interval)
            
            import numpy as np
            indicators["timing_variance"] = float(np.var(intervals))
            indicators["timing_regularity"] = "high" if indicators["timing_variance"] < 1.0 else "normal"
        
        # 세션 길이
        if len(actions) > 1:
            session_length = actions[-1].timestamp - actions[0].timestamp
            indicators["session_length_hours"] = round(session_length / 3600, 2)
            indicators["continuous_play"] = session_length > 4 * 3600  # 4시간 이상
        
        return indicators