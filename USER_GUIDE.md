# DOOM — User Guide

Welcome to **DOOM (Windows Edition)**, a high-performance, privacy-first desktop AI assistant designed for Windows 11.

---

## 1. Quick Start Guide

### Step 1: Launch DOOM
When DOOM launches, it minimizes directly to the Windows **Notification Area (System Tray)** near the clock in the bottom-right corner of your taskbar. Look for the dark shield icon with the green spark.

### Step 2: Open Settings & Configure API Key
1. Right-click the DOOM system tray icon.
2. Select **Settings…** (or press the Windows key and search for "DOOM").
3. In the **Setup Checklist** card at the top, verify the setup status.
4. In the **Gemini API & Model Configuration** card:
   - Enter your Gemini API key (obtain one free at [Google AI Studio](https://aistudio.google.com/)).
   - Click **Save Key**. The key is encrypted locally using the Windows Data Protection API (DPAPI).
   - Ensure the model is set to `gemini-3.5-flash` (recommended default).

### Step 3: Trigger Your First AI Analysis
1. Open any application (e.g. Windows Notepad, Visual Studio Code, Chrome, or Microsoft Edge).
2. Highlight a word, sentence, code snippet, or paragraph with your mouse or keyboard.
3. Press:
   ```
   Ctrl + Shift + Space
   ```
4. DOOM's floating response overlay will smoothly appear near the top-right corner of your display, streaming an immediate answer from Google Gemini.

---

## 2. Keyboard Shortcuts

DOOM is designed to be operated entirely via low-friction global keyboard shortcuts:

| Shortcut | Context | Behavior |
|---|---|---|
| **`Ctrl + Shift + Space`** | System-wide | **Capture & AI Stream:** Inspects the focused window's UI Automation selection, sends the text to Gemini, and opens the floating overlay. |
| **`Ctrl + Shift + H`** | System-wide | **Toggle Overlay Visibility:** Hides or shows the response overlay. Hiding the window does **not** interrupt active generation. |
| **`Escape`** | Inside Overlay | **Cancel Generation:** Instantly aborts the active HTTP streaming connection, halts model generation, and marks the state as Cancelled. |
| **`Escape`** | Inside Settings | **Close Settings:** Hides the Settings window without terminating the background process. |

---

## 3. The Floating Response Overlay

The DOOM response overlay is a specialized, topmost, non-activating window:

```
┌────────────────────────────────────────────────────────┐
│ [●] DOOM                    gemini-3.5-flash   [—] [✕] │
├────────────────────────────────────────────────────────┤
│                                                        │
│  The selected code defines an asynchronous pipeline    │
│  using **System.Threading.Channels**. Key points:      │
│                                                        │
│  1. Unbounded buffer avoids write deadlocks.           │
│  2. `WaitToReadAsync` yields worker thread efficiently.│
│                                                        │
├────────────────────────────────────────────────────────┤
│ [ESC] to Cancel                      Status: Completed │
└────────────────────────────────────────────────────────┘
```

### Key Window Behaviors
- **Non-Activating Focus (`WS_EX_NOACTIVATE`)**: When the overlay appears, keyboard focus remains in your active editor. You can continue typing without interruption.
- **Always-on-Top (`WS_EX_TOPMOST`)**: Stays visible above active documents, web pages, and IDEs.
- **Draggable**: Click and drag anywhere on the top header bar to reposition the overlay across any connected monitor.
- **Adaptive Height**: Automatically expands to accommodate streamed markdown content up to 560 pixels tall with an internal smooth scrollbar.
- **Close Button `[✕]`**: Hides the overlay. Note: Closing the overlay does **not** cancel background AI streaming; press `Ctrl + Shift + H` to unhide and view the completed response.

### Overlay Status States
- **Capturing**: Retrieving highlighted text from the focused application via UI Automation.
- **Streaming**: Receiving SSE response chunks incrementally from the Gemini Interactions API.
- **Completed**: Stream finished successfully; response fully formatted.
- **Cancelled**: Generation was cancelled via `Escape`.
- **Taking Longer Notice**: If Google Gemini takes longer than 15 seconds to reply, an ambient notice informs you that generation is progressing.
- **Error**: Network or API errors are displayed with clear diagnostic messages and an exponential backoff countdown before retry.

---

## 4. Settings Window (5-Card Layout)

Access Settings by right-clicking the system tray icon and selecting **Settings…**:

### Card 1: Setup Checklist
An interactive diagnostic card verifying system health:
- **Gemini API Key**: Green checkmark when configured in DPAPI store; yellow warning if missing.
- **Global Shortcuts**: Confirms `Ctrl + Shift + Space` and `Ctrl + Shift + H` were successfully bound via Win32 `RegisterHotKey`.
- **Privacy Mode**: Reports whether DWM capture exclusion is active.

### Card 2: Gemini API & Model Configuration
- **API Key Field**: Secure password field for entering your Google Gemini API key. Stored encrypted on disk.
- **Model Selection**: Switch between `gemini-3.5-flash` (recommended, high speed) and `gemini-3.5-pro` (complex reasoning).
- **Thinking Mode**: Configure low or standard reasoning depth.
- **Max Output Tokens**: Set maximum response ceiling (default: 4096 tokens).

### Card 3: Global Shortcuts Status
Displays the registered Win32 hotkeys. If another application (such as a gaming overlay or AMD Radeon software) has already bound `Ctrl + Shift + Space`, DOOM will alert you here.

### Card 4: Native Privacy Mode Controls
- **Toggle Privacy Mode (ON by default)**: When ON, applies `WDA_EXCLUDEFROMCAPTURE` to the overlay HWND.
- **Clear Explanation of Scope**:
  - Screen recorders, OBS Studio, and video conference window shares will capture a solid black rectangle instead of your AI response.
  - Documents technical limitations (hardware capture cards and direct physical display outputs cannot be intercepted by Win32).

### Card 5: About & System Diagnostics
Displays version metadata (v1.0.0 LTS), copyright, open source repository link, and application diagnostic logs. Includes a button to safely quit DOOM.

---

## 5. Application Compatibility Guide

DOOM extracts highlighted text via the native **Windows UI Automation (UIA)** framework without altering your system clipboard:

| Target Application | Compatibility | Notes |
|---|---|---|
| **Windows Notepad** | **Native (Instant)** | Win32/XAML Edit controls expose selections directly via `TextPattern`. |
| **Visual Studio Code** | **Native** | Supported via Electron/Chromium accessibility tree. |
| **Visual Studio 2022** | **Native** | WPF Roslyn editor exposes full selection patterns. |
| **Google Chrome / Edge** | **Native** | Web text selections expose standard UIA text patterns. |
| **Microsoft Word / Office** | **Native** | Rich text document selections supported out of the box. |
| **Password Fields** | **Protected** | Password boxes (`IsPassword == true` or `ES_PASSWORD`) are **unconditionally rejected** for security. |
| **Terminal / CMD** | **Native / Fallback** | Windows Terminal exposes UIA; classic conhost may require opting into clipboard fallback in settings. |

---

## 6. Troubleshooting & FAQs

### Q: Why does the overlay say "No text selected"?
**A:** Ensure you have highlighted text before pressing `Ctrl + Shift + Space`. If the target application does not expose UI Automation selections (e.g. legacy terminal windows), you can enable the optional **Clipboard Fallback** in Settings $\rightarrow$ Advanced.

### Q: Why is the DOOM window black in my screen recording or meeting share?
**A:** This is intentional! DOOM's **Privacy Mode** uses `WDA_EXCLUDEFROMCAPTURE` to protect sensitive AI discussions from being accidentally broadcasted or recorded. If you want the overlay visible on stream, turn Privacy Mode OFF in DOOM Settings.

### Q: How do I change the shortcut key?
**A:** By default, DOOM uses `Ctrl + Shift + Space` and `Ctrl + Shift + H`. You can customize shortcut bindings in `%APPDATA%\DOOM\settings.json`.

### Q: How do I completely quit DOOM?
**A:** Right-click the DOOM tray icon in the taskbar and click **Quit DOOM**, or open Settings and click **Quit DOOM** at the bottom of the About card.

