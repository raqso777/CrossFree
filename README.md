# CrossFree v1.0

**Language / Język:** [🇬🇧 English](#-english) | [🇵🇱 Polski](#-polski)

---

## 🇬🇧 English

**CrossFree** is a free, lightweight, open-source custom crosshair overlay for Windows games. Built with C# and WPF (.NET 10), it renders a smooth, click-through crosshair on top of your screen without interfering with gameplay or mouse input.

### ✨ Key Features

* **5 Built-in Crosshair Shapes:** Choose between `Cross`, `Dot`, `Circle`, `Diamond`, and `Sniper` scope styles.
* **Real-Time Customization:** Adjust size, line thickness, center gap, X/Y screen offset, black outline, neon glow, center dot, and any HEX color (with quick color swatches and live preview).
* **Custom Profiles:** Save unlimited custom crosshairs, overwrite existing ones, or reset back to defaults—automatically persisted in `crosshairs.json`.
* **Game Presets (`GAME` tab):** Ready-to-use presets inspired by pro players and popular setups for:
  * **CS2** (*Pro Green*, *Pro Cyan*, *Pro Yellow*, *Classic Outline*)
  * **Valorant** (*TenZ Plus*, *Clean White*, *Green + Dot*)
  * **Fortnite** (*Neon Dot*, *Button*, *Static Cross*)
  * **FiveM / GTA RP** (*RP Dot*, *Red Dot*, *PvP Cross*)
  * **Other** (*Sniper*)
* **Custom App Background & Frosted Glass UI:** Upload your own wallpaper (`PNG`, `JPG`, `BMP`, `GIF`, `TIFF`) and adjust background opacity and real-time Gaussian panel blur.
* **Click-Through Overlay & System Tray:** Uses Win32 extended window styles (`WS_EX_TRANSPARENT`, `WS_EX_NOACTIVATE`) so clicks pass straight through to your game. Minimize the panel to the system tray while keeping the crosshair active.
* **Interactive Onboarding Tutorial:** Built-in 5-step walkthrough on first launch (re-accessible anytime from Settings).

### 📸 Preview

> *Add your screenshot to the repository (e.g. `screenshot.png`) and uncomment the line below:*
> `![CrossFree Preview](screenshot.png)`

### 🚀 Getting Started

#### Option 1: Download & Run (Users)
1. Go to the [**Releases**](../../releases) section on the right.
2. Download the latest `CrossFree` release archive and extract it.
3. Run the executable (`CrossFree.exe`).
4. Toggle the **Celownik (Crosshair)** switch in the top-right corner and launch your game in **Borderless Windowed** mode.

#### Option 2: Build from Source (Developers)
**Requirements:** Windows 10/11 and [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone [https://github.com/raqso777/CrossFree.git](https://github.com/raqso777/CrossFree.git)
cd CrossFree
dotnet run
