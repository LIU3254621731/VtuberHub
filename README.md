<h1 align="center">🎭 VtuberHub</h1>

<p align="center">
  <strong>Cross-Platform VTuber Suite</strong> — Camera → MediaPipe Tracking → 3D Avatar Animation
</p>

<p align="center">
  <img src="https://img.shields.io/badge/C%23-.NET_8-512BD4?logo=.net" alt="C# .NET 8">
  <img src="https://img.shields.io/badge/C%2B%2B-17-00599C?logo=c%2B%2B" alt="C++17">
  <img src="https://img.shields.io/badge/Godot-4-478CBF?logo=godot-engine" alt="Godot 4">
  <img src="https://img.shields.io/badge/Unity-2022-000000?logo=unity" alt="Unity">
  <img src="https://img.shields.io/badge/MediaPipe-Tracking-FF6F00?logo=google" alt="MediaPipe">
  <img src="https://img.shields.io/badge/license-MIT-green" alt="License">
</p>

---

## 📖 Overview

VtuberHub is a comprehensive VTuber application suite with three major components:

1. **VtuberHub Studio** — WPF desktop app for real-time camera capture with MediaPipe full-body/hand/face tracking and gesture recognition
2. **Godot 4 GDExtension Bridge** — C++ native nodes (AvatarAssembler3D, HumanoidMapper3D, MediapipeBridge) for 3D avatar assembly
3. **Unity Editor Integration** — Scripts for visualizing MediaPipe keypoints and customizing avatars

The complete pipeline: **Webcam → MediaPipe Holistic Tracking → Godot/Unity → Animated 3D Avatar**

---

## ✨ Features

### 🎥 Real-Time Tracking
- Full-body tracking (33 pose landmarks)
- Hand tracking (21 landmarks per hand)  
- Face mesh (468 landmarks)
- Gesture recognition (thumbs-up, peace, OK, fist, pointing, open palm)

### 🏗️ 3D Avatar Assembly
- `AvatarAssembler3D` — build avatars from base model + outfit parts
- `HumanoidMapper3D` — map MediaPipe keypoints to 3D bone transforms
- `MediapipeBridge` — dynamic DLL loading for tracking data

### 🛠️ Developer Experience
- CMake 3.20+ build system with VS2022 workspace
- GoogleTest unit tests
- GitHub Actions CI
- NSIS Windows installer
- Cross-language: C++ DLL → C# P/Invoke → Unity MonoBehaviour

---

## 🏛️ Architecture

```
┌─────────────────────────────────────────────────┐
│              VtuberHub Studio (WPF)              │
│  Camera Capture │ Tracking Overlay │ 3D Preview │
└────────┬───────────────┬───────────────┬────────┘
         │               │               │
    ┌────▼────┐    ┌─────▼──────┐   ┌───▼──────┐
    │  Unity  │    │ Godot 4    │   │   C++    │
    │ Scripts │    │ GDExtension│   │  Bridge  │
    └─────────┘    └────────────┘   └──────────┘
         │               │               │
         └───────────────┼───────────────┘
                         │
              ┌──────────▼──────────┐
              │   MediaPipe DLLs    │
              │  (Holistic + Hand)  │
              └─────────────────────┘
```

---

## 🚀 Quick Start

### Prerequisites
- Windows 10/11
- Visual Studio 2022 (C++ and .NET workloads)
- CMake 3.20+
- Godot 4.x / Unity 2022 LTS

### Build

```powershell
# Build the MediaPipe tracking bridge
cd GDExtension/bridge
cmake -B build -S .
cmake --build build --config Release

# Build VtuberHub Studio
dotnet build Editor/VtuberHubStudio/VtuberHubStudio.csproj -c Release
```

---

## 📁 Project Structure

```
VtuberHub/
├── Editor/
│   ├── VtuberHubStudio/     # WPF main application
│   └── MediapipeDllTest/    # DLL integration tests
├── GDExtension/
│   ├── bridge/              # C++ Godot native nodes
│   └── src/                 # Godot 4 project
├── unity/                   # Unity editor scripts
├── 3dmouduls/               # 3D character models (FBX)
├── test/                    # C++ test programs
└── docs/                    # Documentation
```

---

## 🔧 Tech Stack

| Layer | Technology |
|-------|-----------|
| Desktop UI | C# (.NET 8), WPF, CommunityToolkit.Mvvm |
| 3D Engines | Godot 4 (GDExtension), Unity |
| Tracking | MediaPipe Holistic/Hand, OpenCV (OpenCvSharp) |
| Bridge | C++17, P/Invoke, dynamic DLL loading |
| Build | CMake 3.20+, MSBuild, vcpkg |
| Testing | GoogleTest |
| CI/CD | GitHub Actions, NSIS installer |

---

## 📝 License

MIT
