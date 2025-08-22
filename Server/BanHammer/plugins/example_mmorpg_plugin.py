"""
예시 MMORPG 게임 전용 플러그인

이 플러그인은 MMORPG 게임에서 발생하는 특수한 치팅을 탐지합니다:
- 골드 파밍 봇
- 레벨 부스팅
- 아이템 복사 버그
- 순간이동/텔레핵
"""

import time
import math
from collections import defaultdict, deque
from typing import List, Dict, Any
from ..app.plugins.plugin_system import DetectionPlugin, PluginMetadata
from ..app.core.universal_anti_cheat import UniversalPlayerAction, UniversalViolation, ViolationType
from ..app.core.game_profiles import GameProfile

class MMORPGAntiCheatPlugin(DetectionPlugin):
    """MMORPG 전용 치팅 탐지 플러그인"""
    
    def __init__(self):
        super().__init__()
        # 플레이어별 경제 활동 추적
        self.economic_tracker = defaultdict(lambda: {
            'gold_transactions': deque(maxlen=100),
            'item_acquisitions': deque(maxlen=200),
            'location_history': deque(maxlen=50),
            'quest_completions': deque(maxlen=30),
            'trade_history': deque(maxlen=50)
        })
        
    def get_metadata(self) -> PluginMetadata:
        return PluginMetadata(
            name="MMORPG Anti-Cheat Suite",
            version="1.5.0",
            description="MMORPG 게임의 골드 파밍, 봇, 복사 버그 등을 탐지합니다",
            author="BanHammer MMORPG Team",
            game_types=["mmorpg", "mmo", "rpg"],
            dependencies=["numpy"]
        )
    
    async def detect_violations(self, actions: List[UniversalPlayerAction], 
                              profile: GameProfile) -> List[UniversalViolation]:
        violations = []
        
        if not actions:
            return violations
            
        player_id = actions[0].player_id
        game_id = actions[0].game_id
        tracker = self.economic_tracker[player_id]
        
        # 액션 데이터 업데이트
        self._update_tracking_data(actions, tracker)
        
        # 골드 파밍 봇 탐지
        gold_farming_violation = self._detect_gold_farming_bot(player_id, game_id, tracker)
        if gold_farming_violation:
            violations.append(gold_farming_violation)
        
        # 아이템 복사 버그 탐지
        item_dupe_violation = self._detect_item_duplication(player_id, game_id, tracker)
        if item_dupe_violation:
            violations.append(item_dupe_violation)
        
        # 순간이동 탐지
        teleport_violation = self._detect_teleport_hack(player_id, game_id, tracker)
        if teleport_violation:
            violations.append(teleport_violation)
        
        # 레벨 부스팅 탐지
        level_boost_violation = self._detect_level_boosting(player_id, game_id, actions)
        if level_boost_violation:
            violations.append(level_boost_violation)
        
        # 퀘스트 봇 탐지
        quest_bot_violation = self._detect_quest_bot(player_id, game_id, tracker)
        if quest_bot_violation:
            violations.append(quest_bot_violation)
        
        return violations
    
    def _update_tracking_data(self, actions: List[UniversalPlayerAction], tracker: Dict[str, Any]):
        """추적 데이터 업데이트"""
        for action in actions[-20:]:  # 최근 20개 액션 처리
            if action.action_type == "trade_item" and isinstance(action.value, (int, float)):
                tracker['gold_transactions'].append({
                    'amount': action.value,
                    'timestamp': action.timestamp,
                    'metadata': action.metadata
                })
                
            elif action.action_type == "acquire_item":
                tracker['item_acquisitions'].append({
                    'item_id': action.metadata.get('item_id', 'unknown'),
                    'quantity': action.value,
                    'timestamp': action.timestamp,
                    'source': action.metadata.get('source', 'unknown')
                })
                
            elif action.action_type == "move_location":
                if 'coordinates' in action.metadata:
                    tracker['location_history'].append({
                        'coordinates': action.metadata['coordinates'],
                        'zone': action.metadata.get('zone', 'unknown'),
                        'timestamp': action.timestamp
                    })
                    
            elif action.action_type == "complete_quest":
                tracker['quest_completions'].append({
                    'quest_id': action.metadata.get('quest_id', 'unknown'),
                    'exp_reward': action.metadata.get('exp_reward', 0),
                    'timestamp': action.timestamp,
                    'completion_time': action.metadata.get('completion_time', 0)
                })
    
    def _detect_gold_farming_bot(self, player_id: str, game_id: str, 
                                tracker: Dict[str, Any]) -> UniversalViolation:
        """골드 파밍 봇 탐지"""
        gold_transactions = list(tracker['gold_transactions'])
        
        if len(gold_transactions) < 10:
            return None
        
        # 최근 1시간 내 거래 분석
        current_time = time.time()
        recent_transactions = [
            t for t in gold_transactions 
            if current_time - t['timestamp'] < 3600  # 1시간
        ]
        
        if len(recent_transactions) < 5:
            return None
        
        # 거래 패턴 분석
        amounts = [t['amount'] for t in recent_transactions]
        time_intervals = []
        
        for i in range(1, len(recent_transactions)):
            interval = recent_transactions[i]['timestamp'] - recent_transactions[i-1]['timestamp']
            time_intervals.append(interval)
        
        # 봇 특성 점수 계산
        bot_score = 0
        
        # 1. 일정한 금액 (같은 몬스터/아이템 반복 판매)
        if len(set(amounts)) <= 3 and len(amounts) >= 10:
            bot_score += 2
        
        # 2. 일정한 시간 간격
        if time_intervals:
            import numpy as np
            interval_variance = np.var(time_intervals)
            if interval_variance < 10:  # 매우 일정한 간격
                bot_score += 3
        
        # 3. 24시간 연속 활동
        if len(gold_transactions) >= 50:
            time_span = gold_transactions[-1]['timestamp'] - gold_transactions[0]['timestamp']
            if time_span > 20 * 3600:  # 20시간 이상
                bot_score += 2
        
        # 4. 같은 위치에서의 반복 활동
        same_zone_count = sum(
            1 for t in recent_transactions 
            if t.get('metadata', {}).get('zone') == 'farming_zone'
        )
        if same_zone_count / len(recent_transactions) > 0.8:
            bot_score += 1
        
        # 골드 파밍 봇 판정
        if bot_score >= 5:
            return UniversalViolation(
                player_id=player_id,
                game_id=game_id,
                violation_type=ViolationType.CUSTOM_RULE,
                rule_id="mmorpg_gold_farming_bot",
                severity=4.0,
                timestamp=time.time(),
                details={
                    "detection_type": "gold_farming_bot",
                    "bot_score": bot_score,
                    "transaction_count": len(recent_transactions),
                    "time_span_hours": (recent_transactions[-1]['timestamp'] - recent_transactions[0]['timestamp']) / 3600,
                    "avg_interval_seconds": sum(time_intervals) / len(time_intervals) if time_intervals else 0,
                    "unique_amounts": len(set(amounts)),
                    "plugin": "MMORPGAntiCheatPlugin"
                }
            )
        
        return None
    
    def _detect_item_duplication(self, player_id: str, game_id: str, 
                                tracker: Dict[str, Any]) -> UniversalViolation:
        """아이템 복사 버그 탐지"""
        acquisitions = list(tracker['item_acquisitions'])
        
        if len(acquisitions) < 5:
            return None
        
        # 짧은 시간 내 동일 아이템 대량 획득 분석
        current_time = time.time()
        recent_acquisitions = [
            a for a in acquisitions
            if current_time - a['timestamp'] < 300  # 5분 이내
        ]
        
        # 아이템별 획득 수량 집계
        item_counts = defaultdict(int)
        for acquisition in recent_acquisitions:
            item_id = acquisition['item_id']
            quantity = acquisition.get('quantity', 1)
            
            # 희귀 아이템의 경우 더 엄격하게 판정
            if 'rare' in item_id or 'epic' in item_id or 'legendary' in item_id:
                item_counts[item_id] += quantity * 3  # 가중치 적용
            else:
                item_counts[item_id] += quantity
        
        # 의심스러운 아이템 획득 탐지
        for item_id, total_quantity in item_counts.items():
            # 희귀 아이템 10개 이상 또는 일반 아이템 100개 이상
            suspicious_threshold = 10 if any(rarity in item_id for rarity in ['rare', 'epic', 'legendary']) else 100
            
            if total_quantity >= suspicious_threshold:
                return UniversalViolation(
                    player_id=player_id,
                    game_id=game_id,
                    violation_type=ViolationType.CUSTOM_RULE,
                    rule_id="mmorpg_item_duplication",
                    severity=4.8,
                    timestamp=time.time(),
                    details={
                        "detection_type": "item_duplication",
                        "item_id": item_id,
                        "quantity": total_quantity,
                        "time_window_minutes": 5,
                        "acquisition_count": len([a for a in recent_acquisitions if a['item_id'] == item_id]),
                        "plugin": "MMORPGAntiCheatPlugin"
                    }
                )
        
        return None
    
    def _detect_teleport_hack(self, player_id: str, game_id: str, 
                             tracker: Dict[str, Any]) -> UniversalViolation:
        """순간이동/텔레핵 탐지"""
        locations = list(tracker['location_history'])
        
        if len(locations) < 3:
            return None
        
        # 연속된 위치 간 이동 분석
        for i in range(1, len(locations)):
            prev_loc = locations[i-1]
            curr_loc = locations[i]
            
            if 'coordinates' in prev_loc and 'coordinates' in curr_loc:
                prev_coords = prev_loc['coordinates']
                curr_coords = curr_loc['coordinates']
                
                # 거리 계산
                if (isinstance(prev_coords, (list, tuple)) and 
                    isinstance(curr_coords, (list, tuple)) and
                    len(prev_coords) >= 2 and len(curr_coords) >= 2):
                    
                    distance = math.sqrt(
                        (curr_coords[0] - prev_coords[0])**2 + 
                        (curr_coords[1] - prev_coords[1])**2
                    )
                    
                    # 시간 차이
                    time_diff = curr_loc['timestamp'] - prev_loc['timestamp']
                    
                    if time_diff > 0:
                        speed = distance / time_diff
                        
                        # MMORPG에서 불가능한 이동 속도 (맵 크기와 이동 수단에 따라 조정 필요)
                        max_possible_speed = 200  # 게임 단위/초
                        
                        # 다른 존으로의 순간이동은 정상적일 수 있으므로 제외
                        if (speed > max_possible_speed and 
                            prev_loc.get('zone') == curr_loc.get('zone')):
                            
                            return UniversalViolation(
                                player_id=player_id,
                                game_id=game_id,
                                violation_type=ViolationType.CUSTOM_RULE,
                                rule_id="mmorpg_teleport_hack",
                                severity=4.5,
                                timestamp=time.time(),
                                details={
                                    "detection_type": "teleport_hack",
                                    "distance": round(distance, 2),
                                    "time_seconds": round(time_diff, 2),
                                    "speed": round(speed, 2),
                                    "max_possible_speed": max_possible_speed,
                                    "zone": curr_loc.get('zone'),
                                    "plugin": "MMORPGAntiCheatPlugin"
                                }
                            )
        
        return None
    
    def _detect_level_boosting(self, player_id: str, game_id: str, 
                              actions: List[UniversalPlayerAction]) -> UniversalViolation:
        """레벨 부스팅 탐지"""
        # 경험치 및 레벨업 액션 분석
        exp_actions = [a for a in actions[-30:] if a.action_type == "gain_exp"]
        level_actions = [a for a in actions[-10:] if a.action_type == "level_up"]
        
        if len(exp_actions) < 10:
            return None
        
        # 경험치 획득 패턴 분석
        recent_exp = []
        for action in exp_actions:
            if isinstance(action.value, (int, float)):
                recent_exp.append({
                    'amount': action.value,
                    'timestamp': action.timestamp,
                    'source': action.metadata.get('source', 'unknown')
                })
        
        if not recent_exp:
            return None
        
        # 비정상적인 경험치 획득 패턴 탐지
        high_exp_events = [e for e in recent_exp if e['amount'] > 10000]  # 높은 경험치
        
        # 1시간 내에 고경험치 이벤트가 많은 경우
        current_time = time.time()
        recent_high_exp = [
            e for e in high_exp_events 
            if current_time - e['timestamp'] < 3600
        ]
        
        # 레벨 부스팅 의심 조건
        if len(recent_high_exp) >= 5 and len(level_actions) >= 3:
            # 파워레벨링 또는 경험치 핵 의심
            total_exp = sum(e['amount'] for e in recent_high_exp)
            avg_exp_per_event = total_exp / len(recent_high_exp)
            
            return UniversalViolation(
                player_id=player_id,
                game_id=game_id,
                violation_type=ViolationType.CUSTOM_RULE,
                rule_id="mmorpg_level_boosting",
                severity=3.5,
                timestamp=time.time(),
                details={
                    "detection_type": "level_boosting",
                    "high_exp_events": len(recent_high_exp),
                    "level_ups": len(level_actions),
                    "total_exp_gained": total_exp,
                    "avg_exp_per_event": round(avg_exp_per_event, 2),
                    "time_window_hours": 1,
                    "plugin": "MMORPGAntiCheatPlugin"
                }
            )
        
        return None
    
    def _detect_quest_bot(self, player_id: str, game_id: str, 
                         tracker: Dict[str, Any]) -> UniversalViolation:
        """퀘스트 봇 탐지"""
        quest_completions = list(tracker['quest_completions'])
        
        if len(quest_completions) < 8:
            return None
        
        # 퀘스트 완료 패턴 분석
        completion_times = [q.get('completion_time', 0) for q in quest_completions]
        time_intervals = []
        
        for i in range(1, len(quest_completions)):
            interval = quest_completions[i]['timestamp'] - quest_completions[i-1]['timestamp']
            time_intervals.append(interval)
        
        # 봇 의심 지표
        bot_indicators = 0
        
        # 1. 완료 시간이 너무 일정함
        if completion_times:
            import numpy as np
            time_variance = np.var(completion_times)
            if time_variance < 5 and len(completion_times) >= 5:  # 완료 시간이 매우 일정
                bot_indicators += 2
        
        # 2. 퀘스트 간 간격이 일정함
        if time_intervals:
            import numpy as np
            interval_variance = np.var(time_intervals)
            if interval_variance < 30 and len(time_intervals) >= 5:
                bot_indicators += 2
        
        # 3. 같은 타입 퀘스트 반복
        quest_types = [q['quest_id'].split('_')[0] if '_' in q['quest_id'] else q['quest_id'] for q in quest_completions]
        if len(set(quest_types)) <= 2 and len(quest_types) >= 8:  # 2가지 타입의 퀘스트만 반복
            bot_indicators += 1
        
        # 4. 너무 빠른 퀘스트 완료
        fast_completions = sum(1 for t in completion_times if 0 < t < 60)  # 1분 미만
        if fast_completions / len(completion_times) > 0.7:  # 70% 이상이 1분 미만
            bot_indicators += 2
        
        # 퀘스트 봇 판정
        if bot_indicators >= 4:
            return UniversalViolation(
                player_id=player_id,
                game_id=game_id,
                violation_type=ViolationType.CUSTOM_RULE,
                rule_id="mmorpg_quest_bot",
                severity=3.8,
                timestamp=time.time(),
                details={
                    "detection_type": "quest_bot",
                    "bot_indicators": bot_indicators,
                    "quest_completions": len(quest_completions),
                    "avg_completion_time": sum(completion_times) / len(completion_times) if completion_times else 0,
                    "unique_quest_types": len(set(quest_types)),
                    "fast_completion_ratio": fast_completions / len(completion_times) if completion_times else 0,
                    "plugin": "MMORPGAntiCheatPlugin"
                }
            )
        
        return None