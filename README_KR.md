# 🚢 해상환경에서의 자율운항선박 항법 시스템

[![Unity](https://img.shields.io/badge/Unity-2021.3+-black.svg?style=flat&logo=unity)](https://unity.com/)
[![ML-Agents](https://img.shields.io/badge/ML--Agents-2.0+-blue.svg)](https://github.com/Unity-Technologies/ml-agents)
<!-- [![Paper](https://img.shields.io/badge/Paper-arXiv-red.svg)](#) -->

<!-- > **연구 논문**: [심층 강화학습을 이용한 자율 수상 선박의 동적 및 정적 장애물 회피](#) -->
<!-- > *[학회지/학술대회명] 게재 - [년도]* -->

[English](README.md) | **한국어**

---

## 📖 프로젝트 개요

해상환경에서 동적 및 정적 장애물을 회피하며 목표 지점까지 안전하게 항해하는 **자율 운항 선박(ASV)** 시뮬레이션 플랫폼

---

## ✨ 주요 기능

### 🤖 항법 알고리즘
- **A\* 경로 탐색**: 그리드 기반 최적 경로 계산
- **강화학습 (PPO)**: 360° 센서 기반 실시간 장애물 회피
- **RRT\* (참고용)**: 샘플링 기반 경로 계획 (스크립트만 포함)

### 🌊 시뮬레이션 환경
- Crest Ocean System 기반 현실적인 해상 환경
- 정적 장애물 (0~8개) 및 동적 장애물 (0~4개) 설정 가능
- 6자유도 선박 물리 시뮬레이션

---

## 🚀 시작하기

### 사전 요구사항

- **Unity**: 2021.3 LTS 이상
- **Python**: 3.8 이상 (ML-Agents 학습용)
- **ML-Agents Toolkit**: 2.0 이상

### 설치 방법

1. **저장소 클론**
```bash
git clone https://github.com/your-username/unity-asv-navigation-rl.git
cd unity-asv-navigation-rl
```

2. **ML-Agents 설치 (RL 학습용)**
```bash
pip install mlagents==0.30.0
```

3. **Unity에서 프로젝트 열기**
   - Unity Hub 실행
   - "열기"를 클릭하고 클론한 프로젝트 폴더 선택
   - Unity가 모든 에셋을 가져올 때까지 대기

4. **패키지 의존성 확인**
   - 프로젝트가 Package Manager를 통해 필요한 패키지를 자동으로 가져옴
   - `Packages/manifest.json`에서 의존성 확인

---

## 🎮 사용 방법

### 방법 1: Unity 씬에서 실행

#### A\* 알고리즘
1. `Assets/Scenes/Train-AStar.unity` 열기
2. Unity 에디터에서 **Play** 버튼 클릭
3. 선박이 A* 경로 탐색을 이용하여 자동으로 항해
4. 결과는 콘솔에 로그로 기록됨

#### 강화학습
1. `Assets/Scenes/Train-RL.unity` 열기
2. `BoatAgent_RL` 컴포넌트에 학습된 모델이 로드되어 있는지 확인
3. **Play** 버튼 클릭
4. 선박이 학습된 RL 정책을 사용하여 항해

### 방법 2: 훈련 영역 프리팹 사용

1. 새 씬을 생성하거나 기존 씬을 열기
2. `Assets/Prefabs/`에서 원하는 프리팹을 드래그:
   - `TrainArea-AStar.prefab` - A* 항법 설정
   - `TrainArea_RL.prefab` - RL 에이전트 설정
3. Inspector에서 파라미터 설정
4. Play를 눌러 시뮬레이션 시작

### 새로운 RL 모델 학습

1. **학습 파라미터 설정**
   - `config/boat_rl_curriculum.yaml` 편집 (커리큘럼 학습용)

2. **학습 시작**
```bash
mlagents-learn config/boat_rl_curriculum.yaml --run-id=ASV_Navigation_v1
```

3. **학습 진행 상황 모니터링**
   - TensorBoard 열기: `tensorboard --logdir results/`
   - 학습 곡선, 보상, 에피소드 통계 확인

4. **학습된 모델 내보내기**
   - 모델은 `results/ASV_Navigation_v1/`에 저장됨
   - `.onnx` 파일을 `Assets/ML-Agents/`로 복사하여 추론에 사용

---

## 📁 프로젝트 구조

```
autonomous-usv-navigation/
├── Assets/
│   ├── Scenes/                      # Unity 씬
│   │   ├── Train-AStar.unity        # A* 항법 씬
│   │   └── Train-RL.unity           # RL 학습/테스트 씬
│   │
│   ├── Scripts/                     # C# 소스 코드
│   │   ├── BoatProbesBase.cs       # 선박 물리 베이스 클래스
│   │   ├── BoatProbes_AStar.cs     # A* 선박 컨트롤러
│   │   ├── BoatProbes_RL.cs        # RL 선박 컨트롤러
│   │   ├── BoatProbesAI.cs         # AI 선박 컨트롤러
│   │   ├── BoatAgent.cs            # 기본 ML-Agents 에이전트
│   │   ├── BoatAgent_RL.cs         # 고급 RL 에이전트
│   │   ├── BoatAIController_AStar.cs  # A* 경로 탐색 로직
│   │   ├── BoatAIController_RRT.cs    # RRT* 경로 탐색 로직
│   │   ├── BoatAIAdapter.cs        # AI 어댑터 인터페이스
│   │   ├── BoatMovementEnhancer.cs # 선박 이동 개선 유틸리티
│   │   ├── ObstacleController.cs   # 장애물 제어 스크립트
│   │   ├── MoveComponent.cs        # 이동 컴포넌트
│   │   ├── PlaytimeLogger.cs       # 실행 시간 로깅 유틸리티
│   │   └── DataScript/             # 데이터 수집 유틸리티
│   │       ├── AStar/              # A* 실험 데이터
│   │       │   └── Experimentmanager.cs  # 실험 관리자
│   │       └── Testmanager.cs      # 테스트 관리자
│   │
│   ├── Prefabs/                     # 재사용 가능한 게임 오브젝트
│   │   ├── TrainArea-AStar.prefab
│   │   ├── TrainArea_RL.prefab
│   │   └── [장애물 프리팹들]
│   │
│   ├── Materials/                   # 3D 머티리얼
│   ├── Models/                      # 3D 모델
│   └── ML-Agents/                   # 학습된 ML 모델
│
├── config/                          # ML-Agents 설정
│   ├── boat_rl_curriculum.yaml      # 커리큘럼 학습 설정
│   └── boat_rl_test.yaml            # 테스트 설정
│
├── Packages/                        # Unity 패키지 (자동 관리)
├── ProjectSettings/                 # Unity 프로젝트 설정
│
├── README.md                        # 영문 README
└── README_KR.md                     # 한글 README (본 파일)
```

---

## 🛠️ Unity 패키지

### Unity 패키지
- **[Crest Ocean System](https://github.com/wave-harmonic/crest)** 4
- **[A\* Pathfinding Project](https://arongranberg.com/astar/)** 4.2.17
- **[Unity ML-Agents](https://github.com/Unity-Technologies/ml-agents)** 2.0+

## 👥 제작자

- **김영준** - [GitHub](https://github.com/kims124)
- **김민재** - [GitHub](https://github.com/ashclothes01)
- **정민용** - [GitHub](https://github.com/factwy)

---

## 📚 참고 자료

- [Unity ML-Agents Documentation](https://unity-technologies.github.io/ml-agents/)
- [Crest Ocean Docs](https://crest.readthedocs.io/)
- [A* Pathfinding Docs](https://arongranberg.com/astar/docs/)

---
