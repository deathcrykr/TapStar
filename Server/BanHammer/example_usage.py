#!/usr/bin/env python3
"""
BanHammer Anti-Cheat API 사용 예제

이 파일은 BanHammer API를 사용하여 게임에서 치팅을 탐지하는 방법을 보여줍니다.
"""

import asyncio
import aiohttp
import time
import random
from datetime import datetime

API_BASE_URL = "http://localhost:8000/api"

class BanHammerClient:
    def __init__(self, base_url: str = API_BASE_URL):
        self.base_url = base_url
        
    async def submit_action(self, player_id: str, action_type: str, value: float = 0.0, 
                          username: str = None, metadata: dict = None):
        """플레이어 행동을 서버에 제출합니다."""
        async with aiohttp.ClientSession() as session:
            data = {
                "player_id": player_id,
                "action_type": action_type,
                "value": value,
                "username": username,
                "metadata": metadata or {}
            }
            
            async with session.post(f"{self.base_url}/action", json=data) as response:
                return await response.json()
    
    async def get_player_risk(self, player_id: str):
        """플레이어의 위험도를 조회합니다."""
        async with aiohttp.ClientSession() as session:
            async with session.get(f"{self.base_url}/player/{player_id}/risk") as response:
                return await response.json()
    
    async def get_violations(self, player_id: str):
        """플레이어의 위반 기록을 조회합니다."""
        async with aiohttp.ClientSession() as session:
            async with session.get(f"{self.base_url}/player/{player_id}/violations") as response:
                return await response.json()
    
    async def ban_player(self, player_id: str, reason: str, banned_by: str = "admin"):
        """플레이어를 수동으로 차단합니다."""
        async with aiohttp.ClientSession() as session:
            data = {
                "reason": reason,
                "ban_type": "permanent",
                "banned_by": banned_by
            }
            async with session.post(f"{self.base_url}/player/{player_id}/ban", json=data) as response:
                return await response.json()

async def simulate_normal_player(client: BanHammerClient, player_id: str):
    """정상적인 플레이어 행동을 시뮬레이션합니다."""
    print(f"정상 플레이어 시뮬레이션 시작: {player_id}")
    
    actions = ["resource_gather", "reward_collection", "level_progress"]
    
    for i in range(20):
        action_type = random.choice(actions)
        value = random.uniform(1, 50)  # 정상적인 범위의 값
        
        result = await client.submit_action(
            player_id=player_id,
            action_type=action_type,
            value=value,
            username=f"normal_player_{player_id}"
        )
        
        print(f"  액션 #{i+1}: {action_type}={value:.1f} -> 위험도: {result.get('current_risk_score', 0):.2f}")
        
        # 정상적인 간격으로 대기 (1-5초)
        await asyncio.sleep(random.uniform(1, 5))
    
    # 최종 위험도 확인
    risk = await client.get_player_risk(player_id)
    print(f"  최종 위험도: {risk['risk_score']:.2f}")

async def simulate_cheating_player(client: BanHammerClient, player_id: str):
    """치팅하는 플레이어 행동을 시뮬레이션합니다."""
    print(f"치팅 플레이어 시뮬레이션 시작: {player_id}")
    
    # 시나리오 1: 과도한 보상 수집 (1분에 너무 많은 돈 획득)
    print("  시나리오 1: 과도한 보상 수집")
    for i in range(15):  # 한계치(10)를 초과
        result = await client.submit_action(
            player_id=player_id,
            action_type="reward_collection",
            value=random.uniform(80, 120),  # 비정상적으로 높은 값
            username=f"cheater_{player_id}"
        )
        
        print(f"    보상 수집 #{i+1}: 위반={result.get('violations_detected', 0)}, "
              f"위험도={result.get('current_risk_score', 0):.2f}")
        
        await asyncio.sleep(0.5)  # 매우 짧은 간격
    
    await asyncio.sleep(2)
    
    # 시나리오 2: 완벽한 타이밍 (봇 의심)
    print("  시나리오 2: 봇 의심 행동 (완벽한 타이밍)")
    for i in range(10):
        result = await client.submit_action(
            player_id=player_id,
            action_type="resource_gather",
            value=25.0,  # 항상 같은 값
            username=f"cheater_{player_id}"
        )
        
        await asyncio.sleep(2.0)  # 정확히 2초 간격 (봇 의심)
    
    # 최종 위험도 확인
    risk = await client.get_player_risk(player_id)
    violations = await client.get_violations(player_id)
    
    print(f"  최종 위험도: {risk['risk_score']:.2f}")
    print(f"  총 위반 건수: {len(violations)}")
    
    # 위반 내역 출력
    for violation in violations[-5:]:  # 최근 5건만
        print(f"    위반: {violation['violation_type']} (심각도: {violation['severity']:.1f})")

async def demo_rate_limiting():
    """속도 제한 탐지 데모"""
    print("\n=== 속도 제한 탐지 데모 ===")
    client = BanHammerClient()
    
    # 정상 사용자
    await simulate_normal_player(client, "normal_user_001")
    
    await asyncio.sleep(2)
    
    # 치팅 사용자  
    await simulate_cheating_player(client, "cheater_001")

async def demo_statistical_analysis():
    """통계적 이상 탐지 데모"""
    print("\n=== 통계적 이상 탐지 데모 ===")
    client = BanHammerClient()
    
    player_id = "test_stats_001"
    
    # 먼저 정상적인 패턴을 구축
    print("정상 패턴 구축 중...")
    for i in range(15):
        await client.submit_action(
            player_id=player_id,
            action_type="purchase",
            value=random.uniform(10, 30),  # 정상 구매 금액
            username="stats_tester"
        )
        await asyncio.sleep(0.3)
    
    await asyncio.sleep(1)
    
    # 이상한 값 제출 (통계적 이상치)
    print("이상치 탐지 테스트...")
    result = await client.submit_action(
        player_id=player_id,
        action_type="purchase", 
        value=500.0,  # 비정상적으로 높은 구매 금액
        username="stats_tester"
    )
    
    print(f"이상치 결과: 위반={result.get('violations_detected', 0)}, "
          f"위험도={result.get('current_risk_score', 0):.2f}")

async def demo_system_overview():
    """시스템 전체 개요 확인"""
    print("\n=== 시스템 현황 ===")
    
    async with aiohttp.ClientSession() as session:
        # 전체 통계 조회
        async with session.get(f"{API_BASE_URL}/stats/overview") as response:
            stats = await response.json()
            
        print(f"총 플레이어 수: {stats.get('total_players', 0)}")
        print(f"차단된 플레이어 수: {stats.get('banned_players', 0)}")
        print(f"24시간 내 위반 건수: {stats.get('recent_violations_24h', 0)}")
        print(f"고위험 플레이어 수: {stats.get('high_risk_players', 0)}")
        
        violation_breakdown = stats.get('violation_breakdown', {})
        if violation_breakdown:
            print("위반 유형별 현황:")
            for violation_type, count in violation_breakdown.items():
                print(f"  {violation_type}: {count}건")

async def main():
    """메인 데모 함수"""
    print("BanHammer Anti-Cheat API 데모 시작")
    print("=" * 50)
    
    try:
        # 건강 상태 확인
        async with aiohttp.ClientSession() as session:
            async with session.get("http://localhost:8000/health") as response:
                health = await response.json()
                print(f"API 상태: {health['status']}")
                print(f"데이터베이스: {health['components']['database']}")
                print(f"Redis: {health['components']['redis']}")
        
        # 각종 데모 실행
        await demo_rate_limiting()
        await demo_statistical_analysis()
        await demo_system_overview()
        
        print("\n데모 완료! 🎉")
        print("\nAPI 문서는 http://localhost:8000/docs 에서 확인하실 수 있습니다.")
        
    except aiohttp.ClientError as e:
        print(f"❌ API 서버에 연결할 수 없습니다: {e}")
        print("먼저 다음 명령으로 서버를 시작하세요:")
        print("  python main.py")

if __name__ == "__main__":
    asyncio.run(main())