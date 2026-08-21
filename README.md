<div align="center">

# 🎵 Spotify Overlay

**An aesthetic, ultra-lightweight, and buttery-smooth desktop overlay for Spotify on Windows.**

*Эстетичный, ультра-легковесный и плавный оверлей для Spotify на рабочий стол Windows.*

[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)
[![Windows](https://img.shields.io/badge/Platform-Windows_10%2B-0078D6?style=flat&logo=windows)](https://www.microsoft.com/windows)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

<br/>

<img src="assets/compact-overlay.png" alt="Spotify Compact Desktop Overlay" width="550" />

<p><em>Compact translucent widget with adaptive album color palette and infinite marquee scrolling</em></p>

<br/>

<p align="center">
  <img src="assets/fullscreen-overlay-1.png" alt="Fullscreen Mode Showcase 1" width="48%" />
  <img src="assets/fullscreen-overlay-2.png" alt="Fullscreen Mode Showcase 2" width="48%" />
</p>
<p><em>Cinematic fullscreen mode with deep Gaussian blurred background and monitor selector</em></p>

</div>

---

## 🌟 English Overview / Short Summary

**Spotify Overlay** is a modern Windows desktop widget that tracks your currently playing Spotify music in real-time with zero configuration:
- 💫 **Silky-Smooth Spinning Disc**: Delta-timed hardware-accelerated rendering (`CompositionTarget.Rendering`) with subpixel accuracy.
- 📜 **Continuous Marquee Scrolling**: Seamless 1-way infinite title scroll without truncation or jump glitches.
- 🎨 **Smart Adaptive Theme**: Extracts dominant album cover colors in < 1ms to dynamically tint the frosted glass card.
- ⭕ **Customizable Outer Ring**: Choose between Metallic Silver (CD), Adaptive cover color, Spotify Green, White, Black, Gold, Neon Cyan, or No Ring.
- 🖥️ **Cinematic Fullscreen Mode**: Covers any connected monitor with deep 75px Gaussian blurred artwork backdrop and minimal hover-activated controls.
- 📥 **System Tray & Autostart**: Runs unobtrusively in the Windows notification area with one-click startup integration.

---

## ✨ Основные возможности

- 🔄 **Плавная аппаратная анимация**: Вращающаяся круглая обложка текущего трека на базе `CompositionTarget.Rendering` с дельта-временем и субпиксельным рендерингом (без рывков и задержек при любом FPS).
- 📜 **Бесшовная бегущая строка (Marquee)**: Бесконечная прокрутка длинных названий треков на Canvas без прерываний и телепортаций. Возможность переключения на статический режим с троеточием (`...`).
- 🎨 **Адаптивный дизайн и дымка (Photoshop Smudge Blur)**: Автоматическое извлечение преобладающих и акцентных цветов обложки альбома (< 1 мс) с полупрозрачным фоном и мягким размытием краев.
- ⭕ **Настраиваемое внешнее кольцо**: Тонкий ободок с выбором цвета (Silver/Metallic, Adaptive Cover Color, Spotify Green, White, Black, Gold, Neon Cyan, No Ring).
- 🖥️ **Полноэкранный режим (Fullscreen Overlay)**:
  - Кинематографичный вид с глубоко размытой обложкой на весь экран (**Gaussian Blur 75px**).
  - Выбор конкретного монитора (мульти-мониторные конфигурации).
  - Минималистичный интерфейс со скрытым крестиком (появляется только при наведении мыши).
- 📥 **Системный трей (System Tray)**:
  - Фоновая работа без захламления панели задач.
  - Кастомная иконка в трее с всплывающим меню и подсказкой текущего трека.
  - Сворачивание и разворачивание по клику / двойному клику.
- 🚀 **Автозапуск с Windows**: Включение автозагрузки в один клик через контекстное меню.
- ⚡ **Настройка частоты кадров (FPS)**: Ограничение FPS (Auto/Native VSync, 30, 60, 120, 144, 240 или произвольное значение).

---

## 🎮 Controls & Shortcuts / Управление

| Action / Действие | Control / Управление |
|---|---|
| **Move widget / Перемещение** | Drag & drop with Left Mouse Button (when unlocked) |
| **Play / Pause / Пауза** | Double-click widget / Click progress bar / `Spacebar` |
| **Next / Previous Track** | `Right Arrow` / `Left Arrow` (in Fullscreen) or via Context Menu |
| **Context Menu / Меню** | Right-click on widget or System Tray icon |
| **Hide / Show Overlay** | Double-click System Tray icon or via menu |
| **Exit Fullscreen** | `ESC` key or **✕** button in top-right corner |

---

## 🛠️ Build & Run / Сборка и запуск

### Requirements
- **Windows 10 (19041+)** or **Windows 11**
- **.NET 9.0 SDK** (for compiling from source)
- Running **Spotify** desktop client or Microsoft Store app

### Quick Start
Ready standalone executable is located in the project root:
```bash
SpotifyOverlay.exe
```

### Build from Source
```bash
git clone https://github.com/your-username/SpotifyOverlay.git
cd SpotifyOverlay
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false -o ./
```

---

## 📁 Project Structure / Структура проекта

```
SpotifyOverlay/
├── SpotifyOverlay.exe              # Standalone executable / Готовый файл
├── SpotifyOverlay.csproj           # .NET 9 WPF Project Configuration
├── App.xaml / App.xaml.cs          # Application Entry Point
│
├── assets/                         # Documentation screenshots & media
│   ├── compact-overlay.png         # Compact widget screenshot
│   ├── fullscreen-overlay-1.png    # Fullscreen mode (Nujabes)
│   └── fullscreen-overlay-2.png    # Fullscreen mode (Blonde)
│
├── Views/                          # UI Windows & Dialogs
│   ├── MainWindow.xaml / .cs       # Compact overlay & tray controller
│   ├── FullscreenOverlayWindow.xaml/.cs # Fullscreen cinematic overlay
│   └── FpsDialog.xaml / .cs        # FPS limit configuration dialog
│
├── Services/                       # Background Services
│   ├── SpotifyMediaService.cs      # Native Windows GSMTC Media integration
│   └── ColorExtractor.cs           # Palette extraction & smart tinting
│
├── Helpers/                        # Utility Helpers
│   ├── AutoStartupHelper.cs        # Windows Registry autostart manager
│   └── RingColorHelper.cs          # Outer ring styling & color themes
│
├── Properties/                     # Assembly metadata
│   └── AssemblyInfo.cs
│
├── .gitignore                      # Git ignore rules
├── LICENSE                         # MIT License
└── README.md                       # Project Documentation
```

---

## 📄 License

Distributed under the [MIT](LICENSE) License.
