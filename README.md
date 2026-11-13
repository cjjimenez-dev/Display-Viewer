# Display 2 Live Viewer (Stealth Edition)

A high-performance, ultra-lightweight (~17 KB) desktop application for Windows that captures Display 2 in real time, allows direct mouse and keyboard control, and uses Windows' native **`SetWindowDisplayAffinity` API (`0x00000011`)** to remain completely invisible to AnyDesk, TeamViewer, Zoom, OBS, and screen recorders.

---

## Files Overview

| File | Purpose | Size / Tech |
|---|---|---|
| **`Display2Viewer.exe`** | **The main standalone viewer app** with embedded desktop icon (zero installation needed). | ~24 KB (.NET Win32) |
| **`app.ico`** | Application icon file (dual-display theme). | ~5 KB |
| **`Display2Viewer.cs`** | Full C# source code with Win32 P/Invoke APIs. | ~18 KB |
| **`build.bat`** | 1-click compiler using the native Windows C# compiler (`csc.exe`). | Batch script |
| **`viewer.html`** | Alternative browser-based screen viewer using WebRTC. | HTML5 / JS |
| **`display2_viewer.py`** | Alternative Python script (uses `mss`, `opencv-python`). | Python 3 |

---

## Quick Start (How to Run)

1. Double-click **`Display2Viewer.exe`**.
2. The viewer window immediately appears on **Display 1** showing a live 60 FPS stream of **Display 2**.
3. **Stealth Mode is ON by default**: If someone views your desktop via AnyDesk, this window will be completely invisible or blacked out on their screen, but fully visible to you!

---

## Features & Controls

### 1. Stealth Mode & System Tray Invisibility
- **Hidden from Windows Taskbar**: The app runs with `ShowInTaskbar = false`. No taskbar tab or button appears on your screen or on AnyDesk/TeamViewer.
- **System Tray Icon**: Lives quietly in your Windows system tray (bottom-right near the clock).
  - **Double-click Tray Icon**: Instantly Show or Hide the viewer.
  - **Right-click Tray Icon**: Context menu to toggle Stealth Mode, toggle Always on Top, send/pull apps, or cleanly Exit.
  - **Close Button (`X`) or Minimize**: Minimizes the app to the System Tray instead of terminating it.
  - **`Ctrl + H` or "Hide to Tray" Button**: Instantly hides the viewer window to the tray.
- **AnyDesk Shield**: Window display affinity is set to `WDA_EXCLUDEFROMCAPTURE (0x00000011)`. Any remote viewer looking at Display 1 cannot see this window even when it is open right in front of you.

### 2. Interactive Control of Display 2
- **Click Inside the Preview**: Left-click, right-click, middle-click, drag, or scroll your mouse wheel inside the viewer canvas. Your action will be forwarded to that exact location on Display 2.
- **Type into Apps on Display 2**: Clicking on any app inside the preview brings that app into focus on Display 2, so you can immediately type into it.
- **Snap Cursor Back (`[✓] Snap Cursor Back`)**: When checked, your mouse automatically jumps back to your viewer window on Display 1 after clicking, so your cursor never gets stranded off-screen.

### 2. Built-in App Mover
Move windows between displays without blindly dragging them with the mouse:
- **`Send App -> Display 2`**: Select an open window from the dropdown and send it to Display 2.
- **`<- Pull App to Display 1`**: Bring any window from Display 2 back to your main monitor.
- **Native Windows Keyboard Shortcut**: Click any window and press **`Win + Shift + Right Arrow`** (or **`Left Arrow`**) to instantly jump it between monitors.

### 3. Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| **`Ctrl + H`** | Quickly Hide / Show viewer (toggle system tray) |
| **`F11`** | Toggle Fullscreen / Borderless mode |
| **`Ctrl + S`** | Toggle Stealth Mode (Invisible / Visible to AnyDesk) |
| **`Ctrl + T`** | Toggle Always on Top (Keep pinned over other windows) |
| **`Esc`** | Exit Fullscreen (or hide to tray) |

---

## How to Share with Colleagues or Clients

1. Copy or send **`Display2Viewer.exe`** (it is only **17 KB**).
2. The recipient simply double-clicks `Display2Viewer.exe`.
3. **No prerequisites required**: It runs out-of-the-box on any Windows 10 or Windows 11 PC (uses the pre-installed `.NET Framework 4.8`).

---

## How to Recompile from Source

If you edit [Display2Viewer.cs](file:///z:/Christian/SELF%20PROJECTS/Display%20Viewer/Display2Viewer.cs):
1. Double-click **`build.bat`**.
2. It will automatically call `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` and update `Display2Viewer.exe` in 1 second.
