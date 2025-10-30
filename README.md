# 🚢 Autonomous Surface Vehicle Navigation System

[![Unity](https://img.shields.io/badge/Unity-2021.3+-black.svg?style=flat&logo=unity)](https://unity.com/)
[![ML-Agents](https://img.shields.io/badge/ML--Agents-2.0+-blue.svg)](https://github.com/Unity-Technologies/ml-agents)
<!-- [![Paper](https://img.shields.io/badge/Paper-arXiv-red.svg)](#) -->

<!-- > **Research Paper**: [Dynamic and Static Obstacle Avoidance for Autonomous Surface Vessels Using Deep Reinforcement Learning](#) -->
<!-- > *[Conference/Journal Name] - [Year]* -->

**[한국어](README_KR.md)** | English

---

## 📖 Overview

A simulation platform for **Autonomous Surface Vessels (ASV)** that navigate safely to goal positions while avoiding dynamic and static obstacles in maritime environments

---

## ✨ Key Features

### 🤖 Navigation Algorithms
- **A\* Pathfinding**: Grid-based optimal path calculation
- **Reinforcement Learning (PPO)**: Real-time obstacle avoidance with 360° sensors
- **RRT\* (Reference)**: Sampling-based path planning (scripts only)

### 🌊 Simulation Environment
- Realistic maritime environment using Crest Ocean System
- Configurable static (0~8) and dynamic (0~4) obstacles
- 6-DOF boat physics simulation

---

## 🚀 Quick Start

### Prerequisites

- **Unity**: 2021.3 LTS or higher
- **Python**: 3.8+ (for ML-Agents training)
- **ML-Agents Toolkit**: 2.0+

### Installation

1. **Clone repository**
```bash
git clone https://github.com/your-username/unity-asv-navigation-rl.git
cd unity-asv-navigation-rl
```

2. **Install ML-Agents (for RL training)**
```bash
pip install mlagents==0.30.0
```

3. **Open project in Unity**
   - Launch Unity Hub
   - Click "Open" and select the cloned project folder
   - Wait for Unity to import all assets

4. **Verify package dependencies**
   - The project will automatically import required packages via Package Manager
   - Check dependencies in `Packages/manifest.json`

---

## 🎮 Usage

### Method 1: Run in Unity Scenes

#### A\* Algorithm
1. Open `Assets/Scenes/Train-AStar.unity`
2. Click **Play** button in Unity Editor
3. The boat will automatically navigate using A* pathfinding
4. Results are logged to the console

#### Reinforcement Learning
1. Open `Assets/Scenes/Train-RL.unity`
2. Ensure a trained model is loaded in the `BoatAgent_RL` component
3. Click **Play** button
4. The boat will navigate using the trained RL policy

### Method 2: Use Training Area Prefabs

1. Create a new scene or open an existing scene
2. Drag the desired prefab from `Assets/Prefabs/`:
   - `TrainArea-AStar.prefab` - A* navigation setup
   - `TrainArea_RL.prefab` - RL agent setup
3. Configure parameters in the Inspector
4. Press Play to start the simulation

### Train New RL Model

1. **Configure training parameters**
   - Edit `config/boat_rl_curriculum.yaml` (for curriculum learning)

2. **Start training**
```bash
mlagents-learn config/boat_rl_curriculum.yaml --run-id=ASV_Navigation_v1
```

3. **Monitor training progress**
   - Open TensorBoard: `tensorboard --logdir results/`
   - View learning curves, rewards, and episode statistics

4. **Export trained model**
   - Model will be saved in `results/ASV_Navigation_v1/`
   - Copy `.onnx` file to `Assets/ML-Agents/` for inference

---

## 📁 Project Structure

```
autonomous-usv-navigation/
├── Assets/
│   ├── Scenes/                      # Unity scenes
│   │   ├── Train-AStar.unity        # A* navigation scene
│   │   └── Train-RL.unity           # RL training/test scene
│   │
│   ├── Scripts/                     # C# source code
│   │   ├── BoatProbesBase.cs       # Boat physics base class
│   │   ├── BoatProbes_AStar.cs     # A* boat controller
│   │   ├── BoatProbes_RL.cs        # RL boat controller
│   │   ├── BoatProbesAI.cs         # AI boat controller
│   │   ├── BoatAgent.cs            # Basic ML-Agents agent
│   │   ├── BoatAgent_RL.cs         # Advanced RL agent
│   │   ├── BoatAIController_AStar.cs  # A* pathfinding logic
│   │   ├── BoatAIController_RRT.cs    # RRT* pathfinding logic
│   │   ├── BoatAIAdapter.cs        # AI adapter interface
│   │   ├── BoatMovementEnhancer.cs # Boat movement enhancement utility
│   │   ├── ObstacleController.cs   # Obstacle control script
│   │   ├── MoveComponent.cs        # Movement component
│   │   ├── PlaytimeLogger.cs       # Runtime logging utility
│   │   └── DataScript/             # Data collection utilities
│   │       ├── AStar/              # A* experiment data
│   │       │   └── Experimentmanager.cs  # Experiment manager
│   │       └── Testmanager.cs      # Test manager
│   │
│   ├── Prefabs/                     # Reusable game objects
│   │   ├── TrainArea-AStar.prefab
│   │   ├── TrainArea_RL.prefab
│   │   └── [Obstacle prefabs]
│   │
│   ├── Materials/                   # 3D materials
│   ├── Models/                      # 3D models
│   └── ML-Agents/                   # Trained ML models
│
├── config/                          # ML-Agents configs
│   ├── boat_rl_curriculum.yaml      # Curriculum learning config
│   └── boat_rl_test.yaml            # Test config
│
├── Packages/                        # Unity packages (auto-managed)
├── ProjectSettings/                 # Unity project settings
│
├── README.md                        # English README (this file)
└── README_KR.md                     # Korean README
```

---

## 🛠️ Unity Packages

### Unity Packages
- **[Crest Ocean System](https://github.com/wave-harmonic/crest)** 4
- **[A\* Pathfinding Project](https://arongranberg.com/astar/)** 4.2.17
- **[Unity ML-Agents](https://github.com/Unity-Technologies/ml-agents)** 2.0+

## 👥 Authors

- **김영준** - [GitHub](https://github.com/kims124)
- **김민재** - [GitHub](https://github.com/ashclothes01)
- **정민용** - [GitHub](https://github.com/factwy)

---

## 📚 References

- [Unity ML-Agents Documentation](https://unity-technologies.github.io/ml-agents/)
- [Crest Ocean Docs](https://crest.readthedocs.io/)
- [A* Pathfinding Docs](https://arongranberg.com/astar/docs/)

---

<div align="center">
  <sub>Built with ❤️ for maritime autonomous research</sub>
</div>
