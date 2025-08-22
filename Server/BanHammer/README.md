# BanHammer - Advanced Anti-Cheat System for Games

BanHammer는 게임 서버를 위한 강력한 실시간 치팅 탐지 및 방지 시스템입니다. FastAPI 기반으로 구축되어 높은 성능과 확장성을 제공합니다.

## 🎯 주요 기능

### 다층 탐지 시스템
- **속도 제한 및 임계값 검사**: 1분 내 과도한 보상 획득 등 기본적인 치팅 탐지
- **행동 패턴 분석**: 24시간 연속 활동, 반복적인 행동 패턴 분석
- **통계적 이상 탐지**: 머신러닝 기반 이상치 탐지로 새로운 치팅 방식 대응
- **봇 행동 탐지**: 완벽한 타이밍, 동일한 행동 시퀀스 반복 등 자동화 탐지

### 🤖 고급 머신러닝 탐지
- **Random Forest 분류기**: 95% 정확도로 다차원 행동 패턴 분석
- **회귀 모델**: 자원 축적 예측 및 누적 잔차로 미세 버그 탐지  
- **CNN 딥러닝**: 복잡한 시퀀스 패턴 및 시간 기반 이상 탐지
- **앙상블 시스템**: 여러 모델 결과를 종합하여 정확도 향상

### 실시간 모니터링
- 실시간 플레이어 행동 분석
- 위험도 점수 기반 자동 조치
- 상세한 위반 기록 및 추적
- REST API를 통한 시스템 통합

### 자동화된 대응
- 위험도 기반 자동 경고 및 차단
- 임시/영구 차단 시스템
- 관리자 검토 시스템
- 구성 가능한 처벌 정책

## 🚀 빠른 시작

### 1. 설치

```bash
# 저장소 클론
git clone <repository-url>
cd BanHammer

# 의존성 설치
pip install -r requirements.txt
```

### 2. 환경 설정

`.env` 파일 생성:

```env
DATABASE_URL=sqlite:///./banhammer.db
REDIS_URL=redis://localhost:6379/0
SECRET_KEY=your-secret-key-here
DEBUG_MODE=false
LOG_LEVEL=INFO
```

### 3. 서버 시작

```bash
# 개발 모드
python main.py

# 프로덕션 모드
uvicorn main:app --host 0.0.0.0 --port 8000
```

### 4. API 문서 확인

브라우저에서 `http://localhost:8000/docs` 접속

## 📊 사용 예제

### 기본적인 플레이어 행동 제출

```python
import aiohttp

async def submit_player_action():
    data = {
        "player_id": "player_123",
        "username": "player_name", 
        "action_type": "reward_collection",
        "value": 100.0,
        "metadata": {"location": "level_1", "item": "gold_coin"}
    }
    
    async with aiohttp.ClientSession() as session:
        async with session.post("http://localhost:8000/api/action", json=data) as response:
            result = await response.json()
            print(f"위험도: {result['current_risk_score']}")
```

### 플레이어 위험도 조회

```python
async def check_player_risk(player_id: str):
    async with aiohttp.ClientSession() as session:
        async with session.get(f"http://localhost:8000/api/player/{player_id}/risk") as response:
            risk_data = await response.json()
            print(f"현재 위험도: {risk_data['risk_score']}")
            print(f"차단 상태: {risk_data['is_banned']}")
```

### 데모 실행

**기본 시스템 데모:**
```bash
python example_usage.py
```

**고급 머신러닝 데모:**
```bash
python ml_example_usage.py
```

## 🔧 API 엔드포인트

### 플레이어 행동 분석
- `POST /api/action` - 플레이어 행동 제출 및 분석
- `GET /api/player/{player_id}/risk` - 플레이어 위험도 조회
- `GET /api/player/{player_id}/violations` - 위반 기록 조회

### 관리 기능
- `POST /api/player/{player_id}/ban` - 플레이어 차단
- `POST /api/player/{player_id}/unban` - 차단 해제
- `GET /api/violations/recent` - 최근 위반 기록
- `GET /api/stats/overview` - 시스템 전체 통계

### 🤖 머신러닝 API
- `POST /api/ml/train/all` - 모든 모델 훈련
- `GET /api/ml/models/status` - 모델 상태 확인
- `POST /api/ml/predict/single` - 단일 행동 ML 예측
- `GET /api/ml/analytics/model-performance` - 모델 성능 분석
- `GET /api/ml/analytics/feature-importance` - 특징 중요도 분석

## 🎮 탐지 가능한 치팅 유형

### 1. 전통적 치팅
```python
# 예시: 1분에 10회 이상 보상 획득
# 자동으로 rate_limit_exceeded 위반으로 탐지
```

### 2. 봇/자동화 프로그램
- 완벽한 타이밍 (분산 < 0.05초)
- 동일한 행동 시퀀스 반복
- 24시간 연속 활동

### 3. 🤖 ML 기반 고급 탐지
- **미세한 자원 축적 이상**: 회귀 모델로 3-5% 차이도 탐지
- **복잡한 시퀀스 패턴**: CNN으로 A-B-C-A-B-C 같은 반복 탐지
- **다차원 행동 이상**: Random Forest로 여러 특징 동시 분석
- **적응형 치팅**: 기존 규칙을 회피하는 새로운 패턴

### 4. 물리 법칙 위반
- 불가능한 이동 속도
- 순간이동
- 동시에 불가능한 행동

### 5. 경제 시스템 악용
- 누적 잔차 분석으로 미세한 복제 버그 탐지
- 비정상적인 거래 패턴
- 실시간 예측값과 실제값 편차 모니터링

## ⚙️ 설정 옵션

### 기본 속도 제한 설정

```python
rate_limits = {
    "reward_collection": {
        "max_per_minute": 10,      # 분당 최대 횟수
        "max_value_per_minute": 1000  # 분당 최대 값
    },
    "resource_gather": {
        "max_per_minute": 30,
        "max_value_per_minute": 500
    }
}
```

### 행동 분석 설정

```python
behavior_analysis = {
    "window_seconds": 300,           # 분석 시간 윈도우 (5분)
    "anomaly_threshold": 2.5,        # 이상치 탐지 임계값 (표준편차)
    "perfect_timing_threshold": 0.05, # 완벽한 타이밍 임계값
    "continuous_activity_hours": 3    # 연속 활동 시간 임계값
}
```

## 🗄️ 데이터베이스 스키마

### 주요 테이블
- `players` - 플레이어 정보 및 위험도
- `player_actions` - 모든 플레이어 행동 기록
- `violations` - 탐지된 위반 사항
- `ban_history` - 차단 기록
- `player_stats` - 플레이어 통계

### 인덱스 최적화
성능을 위해 다음 필드들에 인덱스가 설정되어 있습니다:
- `player_id`, `timestamp` 조합
- `violation_type`, `severity`
- 시간 기반 쿼리 최적화

## 🔒 보안 고려사항

### 미들웨어 보안
- Rate limiting으로 API 남용 방지
- Security headers 자동 추가
- 요청/응답 로깅으로 감사 추적

### 데이터 보호
- 민감한 정보는 메타데이터에 JSON 형태로 안전하게 저장
- Redis를 통한 임시 데이터 관리 (TTL 설정)

## 🚀 성능 최적화

### 메모리 관리
- 플레이어당 최대 1,000개 행동 기록 유지
- 24시간 후 자동 데이터 정리
- Redis 캐싱으로 빠른 조회

### 비동기 처리
- FastAPI async/await 활용
- 백그라운드 태스크로 무거운 작업 처리
- 데이터베이스 커넥션 풀링

### 🤖 ML 최적화
- **특징 추출 캐싱**: 실시간 버퍼로 중복 계산 방지
- **모델 앙상블**: 가중 평균으로 정확도와 속도 균형
- **배치 예측**: 여러 플레이어 동시 분석으로 처리량 향상
- **자동 재훈련**: 주간 스케줄링으로 성능 유지

## 📈 모니터링 및 알림

### 로깅
- 구조화된 로깅으로 분석 용이
- 위반 사항 실시간 로깅
- 시스템 성능 메트릭

### 대시보드
- `/api/stats/overview`로 전체 현황 파악
- 실시간 위험도 추이
- 위반 유형별 통계

## 🤝 통합 가이드

### 게임 서버 통합

```python
# 게임 내에서 중요한 행동마다 BanHammer에 보고
async def on_player_action(player_id, action_type, value):
    await banhammer_client.submit_action(
        player_id=player_id,
        action_type=action_type,
        value=value
    )
    
    # 위험도 확인
    risk = await banhammer_client.get_player_risk(player_id)
    if risk['risk_score'] > 7.0:
        # 관리자에게 알림
        notify_admin(f"고위험 플레이어 감지: {player_id}")
```

### 실시간 모니터링

```python
# 주기적으로 시스템 상태 확인
async def monitor_system():
    stats = await get_overview_stats()
    if stats['high_risk_players'] > 10:
        send_alert("고위험 플레이어 급증!")
```

## 🧪 테스트

### 유닛 테스트 실행

```bash
pytest tests/
```

### 통합 테스트

```bash
python -m pytest tests/integration/
```

### 부하 테스트

```bash
# locust 등의 도구로 API 부하 테스트
locust -f tests/load_test.py --host=http://localhost:8000
```

## 📝 라이센스

MIT License - 자세한 내용은 LICENSE 파일을 참조하세요.

## 🤝 기여하기

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📞 지원

- GitHub Issues: 버그 리포트 및 기능 요청
- 문서: API 문서는 `/docs` 엔드포인트에서 확인
- 예제: `example_usage.py`에서 다양한 사용 사례 확인

---

**BanHammer**로 공정하고 안전한 게임 환경을 만들어보세요! 🎮⚡