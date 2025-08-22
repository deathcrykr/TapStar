#!/usr/bin/env python3
"""
BanHammer 범용 게임 치팅 탐지 시스템 예제

다양한 장르의 게임을 등록하고 각각에 맞는 치팅 탐지를 시연합니다.
"""

import asyncio
import aiohttp
import time
import random
import json
from datetime import datetime
from typing import Dict, Any, List

API_BASE_URL = "http://localhost:8000/api/universal"

class UniversalBanHammerClient:
    def __init__(self, base_url: str = API_BASE_URL):
        self.base_url = base_url
    
    async def register_game(self, game_id: str, game_name: str, genre: str, description: str = ""):
        """게임 등록"""
        data = {
            "game_id": game_id,
            "game_name": game_name,
            "genre": genre,
            "description": description
        }
        
        async with aiohttp.ClientSession() as session:
            async with session.post(f"{self.base_url}/games/register", json=data) as response:
                return await response.json()
    
    async def get_supported_genres(self):
        """지원되는 장르 목록"""
        async with aiohttp.ClientSession() as session:
            async with session.get(f"{self.base_url}/supported-genres") as response:
                return await response.json()
    
    async def submit_action(self, player_id: str, game_id: str, action_type: str, 
                          value: Any = None, metadata: Dict[str, Any] = None):
        """액션 제출"""
        data = {
            "player_id": player_id,
            "game_id": game_id,
            "action_type": action_type,
            "value": value,
            "metadata": metadata or {},
            "session_id": f"session_{player_id}_{int(time.time())}",
            "client_info": {"platform": "demo", "version": "1.0.0"}
        }
        
        async with aiohttp.ClientSession() as session:
            async with session.post(f"{self.base_url}/action", json=data) as response:
                return await response.json()
    
    async def get_player_risk(self, game_id: str, player_id: str):
        """플레이어 위험도 조회"""
        async with aiohttp.ClientSession() as session:
            async with session.get(f"{self.base_url}/games/{game_id}/player/{player_id}/risk") as response:
                return await response.json()
    
    async def get_game_profile(self, game_id: str):
        """게임 프로파일 조회"""
        async with aiohttp.ClientSession() as session:
            async with session.get(f"{self.base_url}/games/{game_id}") as response:
                return await response.json()
    
    async def list_games(self):
        """게임 목록 조회"""
        async with aiohttp.ClientSession() as session:
            async with session.get(f"{self.base_url}/games") as response:
                return await response.json()
    
    async def add_custom_action(self, game_id: str, action_name: str, category: str, 
                              description: str, value_type: str = "float"):
        """커스텀 액션 추가"""
        data = {
            "name": action_name,
            "category": category,
            "description": description,
            "value_type": value_type,
            "required_fields": [],
            "optional_fields": ["location", "method"],
            "metadata_schema": {}
        }
        
        async with aiohttp.ClientSession() as session:
            async with session.post(f"{self.base_url}/games/{game_id}/actions", json=data) as response:
                return await response.json()
    
    async def add_custom_rule(self, game_id: str, rule_id: str, name: str, 
                            action_types: List[str], rule_type: str, parameters: Dict[str, Any]):
        """커스텀 탐지 규칙 추가"""
        data = {
            "rule_id": rule_id,
            "name": name,
            "description": f"Custom rule: {name}",
            "action_types": action_types,
            "rule_type": rule_type,
            "parameters": parameters,
            "severity": 3.5,
            "enabled": True
        }
        
        async with aiohttp.ClientSession() as session:
            async with session.post(f"{self.base_url}/games/{game_id}/rules", json=data) as response:
                return await response.json()

async def demo_mmorpg_game(client: UniversalBanHammerClient):
    """MMORPG 게임 시연"""
    print("\n🏰 MMORPG 게임 시연 - 'Fantasy Quest'")
    
    # 게임 등록
    await client.register_game(
        game_id="fantasy_quest",
        game_name="Fantasy Quest",
        genre="mmorpg",
        description="대규모 판타지 MMORPG"
    )
    
    # 게임 프로파일 확인
    profile = await client.get_game_profile("fantasy_quest")
    print(f"  등록된 액션 수: {len(profile['actions'])}")
    print(f"  기본 탐지 규칙: {len(profile['detection_rules'])}")
    
    # 정상 플레이어 시뮬레이션
    print("\n  📊 정상 플레이어 'hero123' 시뮬레이션:")
    player_id = "hero123"
    
    for i in range(10):
        # 다양한 MMORPG 액션
        actions = [
            ("kill_monster", random.randint(1, 5)),
            ("gain_exp", random.uniform(100, 500)),
            ("acquire_item", random.randint(1, 3)),
            ("complete_quest", 1),
            ("use_skill", random.randint(1, 5))
        ]
        
        action_type, value = random.choice(actions)
        result = await client.submit_action(
            player_id=player_id,
            game_id="fantasy_quest",
            action_type=action_type,
            value=value,
            metadata={"zone": "forest", "level": 25 + i}
        )
        
        print(f"    {action_type}={value} -> 위반:{result.get('violations_detected', 0)}, "
              f"위험도:{result.get('current_risk_score', 0):.2f}")
        
        await asyncio.sleep(0.5)
    
    # 치팅 플레이어 시뮬레이션 
    print("\n  🚨 치팅 플레이어 'gold_farmer_bot' 시뮬레이션:")
    cheater_id = "gold_farmer_bot"
    
    # 골드 파밍 봇 행동 패턴
    for i in range(15):
        # 동일한 패턴 반복 (kill -> exp -> item)
        actions_sequence = [
            ("kill_monster", 3),  # 항상 3마리
            ("gain_exp", 150.0),  # 항상 같은 경험치
            ("acquire_item", 1)   # 항상 1개 아이템
        ]
        
        for action_type, value in actions_sequence:
            result = await client.submit_action(
                player_id=cheater_id,
                game_id="fantasy_quest",
                action_type=action_type,
                value=value,
                metadata={"zone": "gold_farm_spot", "automated": True}
            )
            
            if result.get('violations_detected', 0) > 0:
                print(f"    🔴 {action_type} -> 위반 탐지! 위험도:{result.get('current_risk_score', 0):.2f}")
                for violation in result.get('violations', []):
                    print(f"      ⚠️ {violation['type']}: 심각도 {violation['severity']}")
            
            await asyncio.sleep(0.1)  # 매우 빠른 간격
    
    # 최종 위험도 확인
    risk_data = await client.get_player_risk("fantasy_quest", cheater_id)
    print(f"  최종 치팅 플레이어 위험도: {risk_data['risk_score']:.2f}")
    print(f"  자동 차단 권장: {'예' if risk_data['should_ban'] else '아니오'}")

async def demo_mobile_rpg_game(client: UniversalBanHammerClient):
    """모바일 RPG 게임 시연"""
    print("\n📱 모바일 RPG 게임 시연 - 'Idle Heroes Mobile'")
    
    # 게임 등록
    await client.register_game(
        game_id="idle_heroes_mobile",
        game_name="Idle Heroes Mobile", 
        genre="mobile_rpg",
        description="방치형 모바일 RPG"
    )
    
    print("\n  💰 보상 수집 남용 시뮬레이션:")
    cheater_id = "reward_abuser"
    
    # 짧은 시간에 과도한 보상 수집
    for i in range(20):  # 제한 (15회)을 초과
        result = await client.submit_action(
            player_id=cheater_id,
            game_id="idle_heroes_mobile",
            action_type="collect_reward",
            value=random.uniform(5000, 15000),  # 높은 보상값
            metadata={"reward_type": "daily_login", "multiplier": 10}
        )
        
        if result.get('violations_detected', 0) > 0:
            print(f"    #{i+1} 보상 수집 -> 위반 탐지! ({result['violations'][0]['type']})")
        
        await asyncio.sleep(0.2)

async def demo_fps_game(client: UniversalBanHammerClient):
    """FPS 게임 시연"""
    print("\n🔫 FPS 게임 시연 - 'Battle Arena FPS'")
    
    # 게임 등록
    await client.register_game(
        game_id="battle_arena_fps",
        game_name="Battle Arena FPS",
        genre="fps",
        description="경쟁 FPS 게임"
    )
    
    print("\n  🎯 에임봇 의심 행동 시뮬레이션:")
    cheater_id = "aimbot_user"
    
    # 비정상적으로 높은 헤드샷 비율
    for i in range(20):
        if i % 3 == 0:  # 3번 중 2번이 헤드샷 (66% 헤드샷 비율)
            action_type = "player_kill"
            value = 1
        else:
            action_type = "headshot"
            value = 1
        
        result = await client.submit_action(
            player_id=cheater_id,
            game_id="battle_arena_fps",
            action_type=action_type,
            value=value,
            metadata={"weapon": "sniper_rifle", "distance": random.uniform(100, 500)}
        )
        
        await asyncio.sleep(0.3)
    
    # 위험도 확인
    risk_data = await client.get_player_risk("battle_arena_fps", cheater_id)
    print(f"  최종 위험도: {risk_data['risk_score']:.2f}")

async def demo_puzzle_game(client: UniversalBanHammerClient):
    """퍼즐 게임 시연"""
    print("\n🧩 퍼즐 게임 시연 - 'Brain Puzzle Master'")
    
    # 게임 등록
    await client.register_game(
        game_id="brain_puzzle",
        game_name="Brain Puzzle Master",
        genre="puzzle",
        description="두뇌 트레이닝 퍼즐 게임"
    )
    
    print("\n  ⚡ 비인간적 해결 속도 시뮬레이션:")
    cheater_id = "puzzle_bot"
    
    # 너무 빠른 퍼즐 해결
    for i in range(10):
        # 복잡한 퍼즐을 1-2초만에 해결
        result = await client.submit_action(
            player_id=cheater_id,
            game_id="brain_puzzle", 
            action_type="solve_time",
            value=random.uniform(0.5, 2.0),  # 0.5-2초 (임계값 5초보다 훨씬 빠름)
            metadata={"puzzle_difficulty": "expert", "level": 100 + i}
        )
        
        if result.get('violations_detected', 0) > 0:
            print(f"    퍼즐 #{i+1} -> 위반 탐지! (해결 시간: {result['violations'][0]['details'].get('actual_value', 0):.1f}초)")

async def demo_custom_game_setup(client: UniversalBanHammerClient):
    """커스텀 게임 설정 시연"""
    print("\n⚙️ 커스텀 게임 설정 시연")
    
    # 새로운 게임 타입 등록
    await client.register_game(
        game_id="my_racing_game",
        game_name="Speed Racing Championship",
        genre="racing", 
        description="커스터마이징된 레이싱 게임"
    )
    
    print("  📝 커스텀 액션 추가:")
    
    # 커스텀 액션 추가
    custom_actions = [
        ("finish_race", "competitive", "레이스 완주", "float"),
        ("use_nitro", "resource", "니트로 사용", "int"),
        ("crash_incident", "movement", "충돌 사건", "bool")
    ]
    
    for action_name, category, description, value_type in custom_actions:
        result = await client.add_custom_action(
            game_id="my_racing_game",
            action_name=action_name,
            category=category,
            description=description,
            value_type=value_type
        )
        print(f"    ✅ {action_name} 액션 추가됨")
    
    print("\n  🔧 커스텀 탐지 규칙 추가:")
    
    # 커스텀 탐지 규칙 추가
    await client.add_custom_rule(
        game_id="my_racing_game",
        rule_id="impossible_lap_time",
        name="불가능한 랩타임 탐지",
        action_types=["finish_race"],
        rule_type="threshold",
        parameters={"min_value": 60.0}  # 60초 미만은 불가능
    )
    print("    🔍 불가능한 랩타임 탐지 규칙 추가됨")
    
    # 커스텀 규칙 테스트
    print("\n  🧪 커스텀 규칙 테스트:")
    result = await client.submit_action(
        player_id="speed_hacker",
        game_id="my_racing_game", 
        action_type="finish_race",
        value=30.0,  # 30초 (60초 임계값보다 낮음)
        metadata={"track": "professional_circuit", "weather": "clear"}
    )
    
    if result.get('violations_detected', 0) > 0:
        print("    🚨 커스텀 규칙 작동! 불가능한 랩타임 탐지됨")

async def demo_multi_game_analytics(client: UniversalBanHammerClient):
    """다중 게임 분석 시연"""
    print("\n📊 다중 게임 분석 대시보드")
    
    # 등록된 게임 목록
    games_data = await client.list_games()
    print(f"\n  등록된 게임 수: {len(games_data['games'])}")
    
    for game in games_data['games']:
        print(f"    🎮 {game['game_name']} ({game['genre']})")
        print(f"       - 액션 수: {game['action_count']}")
        print(f"       - 규칙 수: {game['rule_count']}")

async def main():
    """메인 데모 함수"""
    print("🌍 BanHammer 범용 게임 치팅 탐지 시스템 데모")
    print("=" * 60)
    
    client = UniversalBanHammerClient()
    
    try:
        # 지원되는 장르 확인
        genres = await client.get_supported_genres()
        print("🎯 지원되는 게임 장르:")
        for genre in genres['genres'][:5]:  # 처음 5개만 표시
            print(f"  • {genre['name']}: {genre['description']}")
        
        # 다양한 게임 장르 시연
        await demo_mmorpg_game(client)
        await demo_mobile_rpg_game(client)
        await demo_fps_game(client)
        await demo_puzzle_game(client)
        await demo_custom_game_setup(client)
        await demo_multi_game_analytics(client)
        
        print("\n" + "=" * 60)
        print("🎉 범용 게임 치팅 탐지 시스템 데모 완료!")
        
        print("\n🎮 주요 특징:")
        print("  • 14개 게임 장르 지원 (MMORPG, FPS, 모바일 RPG 등)")
        print("  • 장르별 최적화된 기본 탐지 규칙")
        print("  • 커스텀 액션 & 탐지 규칙 추가 가능")
        print("  • 플러그인 시스템으로 확장 가능")
        print("  • 다중 게임 통합 관리")
        
        print("\n🔗 주요 API 엔드포인트:")
        print("  • POST /api/universal/games/register - 게임 등록")
        print("  • POST /api/universal/action - 액션 제출 & 탐지")
        print("  • GET /api/universal/games/{id}/player/{id}/risk - 위험도 조회")
        print("  • POST /api/universal/games/{id}/actions - 커스텀 액션 추가")
        print("  • POST /api/universal/games/{id}/rules - 커스텀 규칙 추가")
        print("  • GET /api/universal/supported-genres - 지원 장르 목록")
        print("\n📖 API 문서: http://localhost:8000/docs")
        
    except aiohttp.ClientError as e:
        print(f"❌ API 서버에 연결할 수 없습니다: {e}")
        print("먼저 다음 명령으로 서버를 시작하세요:")
        print("  python main.py")
    except Exception as e:
        print(f"❌ 예상치 못한 오류: {e}")

if __name__ == "__main__":
    asyncio.run(main())