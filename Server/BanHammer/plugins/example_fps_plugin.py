"""
예시 FPS 게임 전용 플러그인

이 플러그인은 FPS 게임에서 발생하는 특수한 치팅을 탐지합니다:
- 에임봇 (높은 헤드샷 비율)
- 월핵 (벽 너머 킬)
- 스피드핵 (빠른 이동)
"""

import time
import math
from typing import List, Dict, Any
from ..app.plugins.plugin_system import DetectionPlugin, PluginMetadata
from ..app.core.universal_anti_cheat import UniversalPlayerAction, UniversalViolation, ViolationType
from ..app.core.game_profiles import GameProfile

class AdvancedFPSPlugin(DetectionPlugin):
    """고급 FPS 치팅 탐지 플러그인"""
    
    def __init__(self):
        super().__init__()
        # 플레이어별 통계 추적
        self.player_stats = {}
        
    def get_metadata(self) -> PluginMetadata:
        return PluginMetadata(
            name="Advanced FPS Anti-Cheat Plugin",
            version="2.0.0",
            description="FPS 게임의 에임봇, 월핵, 스피드핵 등 고급 치팅을 탐지합니다",
            author="BanHammer Community",
            game_types=["fps", "battle_royale", "tactical_shooter"],
            dependencies=["numpy", "scipy"]
        )
    
    async def detect_violations(self, actions: List[UniversalPlayerAction], 
                              profile: GameProfile) -> List[UniversalViolation]:
        violations = []
        
        if not actions:
            return violations
            
        player_id = actions[0].player_id
        game_id = actions[0].game_id
        
        # 플레이어 통계 초기화
        if player_id not in self.player_stats:
            self.player_stats[player_id] = {
                'kills': 0,
                'headshots': 0,
                'shots_fired': 0,
                'hits': 0,
                'positions': [],
                'kill_positions': []
            }
        
        stats = self.player_stats[player_id]
        
        # 액션별로 통계 업데이트
        for action in actions[-50:]:  # 최근 50개 액션만 분석
            self._update_player_stats(action, stats)
        
        # 에임봇 탐지
        aimbot_violation = self._detect_aimbot(player_id, game_id, stats, actions)
        if aimbot_violation:
            violations.append(aimbot_violation)
        
        # 월핵 탐지
        wallhack_violation = self._detect_wallhack(player_id, game_id, stats, actions)
        if wallhack_violation:
            violations.append(wallhack_violation)
        
        # 스피드핵 탐지
        speedhack_violation = self._detect_speedhack(player_id, game_id, actions)
        if speedhack_violation:
            violations.append(speedhack_violation)
        
        # 트리거봇 탐지
        triggerbot_violation = self._detect_triggerbot(player_id, game_id, actions)
        if triggerbot_violation:
            violations.append(triggerbot_violation)
        
        return violations
    
    def _update_player_stats(self, action: UniversalPlayerAction, stats: Dict[str, Any]):
        """플레이어 통계 업데이트"""
        if action.action_type == "player_kill":
            stats['kills'] += 1
            # 킬 위치 저장
            if 'position' in action.metadata:
                stats['kill_positions'].append({
                    'position': action.metadata['position'],
                    'timestamp': action.timestamp,
                    'target_position': action.metadata.get('target_position')
                })
        elif action.action_type == "headshot":
            stats['headshots'] += 1
        elif action.action_type == "fire_weapon":
            stats['shots_fired'] += action.value if action.value else 1
        elif action.action_type == "accuracy_shot":
            if action.value and action.value > 0:
                stats['hits'] += 1
        elif action.action_type == "move_position":
            if action.value and isinstance(action.value, (list, tuple)) and len(action.value) >= 2:
                stats['positions'].append({
                    'position': action.value,
                    'timestamp': action.timestamp
                })
    
    def _detect_aimbot(self, player_id: str, game_id: str, stats: Dict[str, Any], 
                      actions: List[UniversalPlayerAction]) -> UniversalViolation:
        """에임봇 탐지"""
        if stats['kills'] < 10:  # 최소 10킬 필요
            return None
        
        # 헤드샷 비율 계산
        headshot_ratio = stats['headshots'] / stats['kills'] if stats['kills'] > 0 else 0
        
        # 명중률 계산
        accuracy = stats['hits'] / stats['shots_fired'] if stats['shots_fired'] > 0 else 0
        
        # 에임봇 의심 임계값
        suspicious_headshot_ratio = 0.75  # 75% 이상 헤드샷
        suspicious_accuracy = 0.90  # 90% 이상 명중률
        
        # 에임봇 판정
        is_aimbot = (headshot_ratio >= suspicious_headshot_ratio and 
                    accuracy >= suspicious_accuracy and
                    stats['kills'] >= 15)  # 충분한 샘플 크기
        
        if is_aimbot:
            return UniversalViolation(
                player_id=player_id,
                game_id=game_id,
                violation_type=ViolationType.CUSTOM_RULE,
                rule_id="fps_aimbot_advanced",
                severity=4.8,
                timestamp=time.time(),
                details={
                    "detection_type": "aimbot",
                    "headshot_ratio": round(headshot_ratio, 3),
                    "accuracy": round(accuracy, 3),
                    "total_kills": stats['kills'],
                    "total_headshots": stats['headshots'],
                    "confidence": min((headshot_ratio - 0.5) * 2 + (accuracy - 0.5) * 2, 1.0),
                    "plugin": "AdvancedFPSPlugin"
                }
            )
        
        return None
    
    def _detect_wallhack(self, player_id: str, game_id: str, stats: Dict[str, Any], 
                        actions: List[UniversalPlayerAction]) -> UniversalViolation:
        """월핵 탐지"""
        kill_positions = stats.get('kill_positions', [])
        
        if len(kill_positions) < 5:  # 최소 5킬 필요
            return None
        
        # 벽 너머 킬 패턴 분석
        suspicious_kills = 0
        total_analyzed = 0
        
        for kill_data in kill_positions[-20:]:  # 최근 20킬 분석
            if not kill_data.get('target_position'):
                continue
                
            player_pos = kill_data['position']
            target_pos = kill_data['target_position']
            
            # 거리 계산
            if isinstance(player_pos, (list, tuple)) and isinstance(target_pos, (list, tuple)):
                distance = math.sqrt(
                    (player_pos[0] - target_pos[0])**2 + 
                    (player_pos[1] - target_pos[1])**2
                )
                
                # 각도 분석 (직선 관통 킬 탐지)
                # 실제 게임에서는 맵 데이터를 활용해야 하지만, 여기서는 간단한 휴리스틱 사용
                
                # 너무 먼 거리에서의 정확한 킬은 의심스러움
                if distance > 200 and 'headshot' in str(kill_data):
                    suspicious_kills += 1
                
                total_analyzed += 1
        
        if total_analyzed > 0:
            suspicious_ratio = suspicious_kills / total_analyzed
            
            if suspicious_ratio > 0.4:  # 40% 이상이 의심스러운 킬
                return UniversalViolation(
                    player_id=player_id,
                    game_id=game_id,
                    violation_type=ViolationType.CUSTOM_RULE,
                    rule_id="fps_wallhack_detection",
                    severity=4.2,
                    timestamp=time.time(),
                    details={
                        "detection_type": "wallhack",
                        "suspicious_kill_ratio": round(suspicious_ratio, 3),
                        "suspicious_kills": suspicious_kills,
                        "total_analyzed": total_analyzed,
                        "pattern": "impossible_angle_kills",
                        "plugin": "AdvancedFPSPlugin"
                    }
                )
        
        return None
    
    def _detect_speedhack(self, player_id: str, game_id: str, 
                         actions: List[UniversalPlayerAction]) -> UniversalViolation:
        """스피드핵 탐지"""
        move_actions = [a for a in actions[-30:] if a.action_type == "move_position"]
        
        if len(move_actions) < 3:
            return None
        
        # 이동 속도 분석
        speeds = []
        for i in range(1, len(move_actions)):
            prev_action = move_actions[i-1]
            curr_action = move_actions[i]
            
            if (isinstance(prev_action.value, (list, tuple)) and 
                isinstance(curr_action.value, (list, tuple)) and
                len(prev_action.value) >= 2 and len(curr_action.value) >= 2):
                
                # 거리 계산
                distance = math.sqrt(
                    (curr_action.value[0] - prev_action.value[0])**2 + 
                    (curr_action.value[1] - prev_action.value[1])**2
                )
                
                # 시간 차이
                time_diff = curr_action.timestamp - prev_action.timestamp
                
                if time_diff > 0:
                    speed = distance / time_diff
                    speeds.append(speed)
        
        if speeds:
            max_speed = max(speeds)
            avg_speed = sum(speeds) / len(speeds)
            
            # 일반적인 FPS 게임에서 불가능한 속도 (게임마다 다를 수 있음)
            max_possible_speed = 500  # 단위/초
            
            if max_speed > max_possible_speed or avg_speed > max_possible_speed * 0.8:
                return UniversalViolation(
                    player_id=player_id,
                    game_id=game_id,
                    violation_type=ViolationType.CUSTOM_RULE,
                    rule_id="fps_speedhack_detection",
                    severity=4.5,
                    timestamp=time.time(),
                    details={
                        "detection_type": "speedhack",
                        "max_speed": round(max_speed, 2),
                        "avg_speed": round(avg_speed, 2),
                        "max_possible_speed": max_possible_speed,
                        "speed_samples": len(speeds),
                        "plugin": "AdvancedFPSPlugin"
                    }
                )
        
        return None
    
    def _detect_triggerbot(self, player_id: str, game_id: str, 
                          actions: List[UniversalPlayerAction]) -> UniversalViolation:
        """트리거봇 탐지 (적이 조준선에 들어오면 자동으로 발사)"""
        fire_actions = [a for a in actions[-20:] if a.action_type == "fire_weapon"]
        kill_actions = [a for a in actions[-20:] if a.action_type == "player_kill"]
        
        if len(fire_actions) < 10 or len(kill_actions) < 5:
            return None
        
        # 발사 후 즉시 킬 패턴 분석
        instant_kills = 0
        
        for kill_action in kill_actions:
            # 킬 직전 0.1초 이내의 발사 찾기
            recent_fires = [
                f for f in fire_actions 
                if abs(f.timestamp - kill_action.timestamp) < 0.1 and f.timestamp <= kill_action.timestamp
            ]
            
            if recent_fires:
                instant_kills += 1
        
        instant_kill_ratio = instant_kills / len(kill_actions)
        
        # 트리거봇 의심 임계값 (90% 이상의 킬이 발사 직후)
        if instant_kill_ratio > 0.9 and len(kill_actions) >= 8:
            return UniversalViolation(
                player_id=player_id,
                game_id=game_id,
                violation_type=ViolationType.CUSTOM_RULE,
                rule_id="fps_triggerbot_detection",
                severity=3.8,
                timestamp=time.time(),
                details={
                    "detection_type": "triggerbot",
                    "instant_kill_ratio": round(instant_kill_ratio, 3),
                    "instant_kills": instant_kills,
                    "total_kills": len(kill_actions),
                    "reaction_time_ms": "< 100",
                    "plugin": "AdvancedFPSPlugin"
                }
            )
        
        return None