# DOOM — Privacy & Security Architecture

> **Security & Transparency Report**  
> Technical specification of privacy protections, memory security, hardware encryption, and capture exclusion mechanisms in **DOOM (Windows Edition)**.

---

## 1. Core Privacy Philosophy

DOOM was built on a single, uncompromising principle:
**Ambient AI assistance must never compromise user privacy, leak confidential context, or persist sensitive data.**

To enforce this, DOOM implements five architectural defense layers:
1. **Hardware-accelerated window capture masking** via the Windows DWM.
2. **Volatile RAM-only processing** with zero disk persistence for prompts or answers.
3. **Dual-layer password field rejection** before data reaches network buffers.
4. **Hardware-bound credential encryption** via the Windows Data Protection API (DPAPI).
5. **Real-time regex logging redaction** preventing sensitive tokens from entering event traces.

---

## 2. Window Capture Masking (`WDA_EXCLUDEFROMCAPTURE`)

### Win32 Display Affinity Integration
DOOM leverages the native Windows 11 Desktop Window Manager (DWM) display affinity subsystem through the Win32 API:

```csharp
[DllImport("user32.dll", SetLastError = true)]
public static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);
```

When Privacy Mode is enabled (active by default), DOOM applies:
```csharp
const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;
```

### How It Works Under the Hood
1. When Windows renders the desktop, DWM composites application HWNDs into the display buffer.
2. For capture APIs (`PrintWindow`, `BitBlt`, Windows Graphics Capture `Windows.Graphics.Capture`, and DXGI Desktop Duplication), DWM evaluates the affinity mask of each window.
3. If `WDA_EXCLUDEFROMCAPTURE` is active, DWM omits the overlay's texture entirely or renders a solid black rectangle in the captured video frame, while rendering the window normally on the user's physical display monitor.

### Empirical Windows 11 Verification
DOOM's capture exclusion was tested and validated on Windows 11 via native Win32 capture harnesses (`scripts/test-live-windows-capture-exclusion.ps1`):

- **Baseline Capture (Privacy OFF):** Window surface captured intact with full text content visible.
- **Protected Capture (`WDA_EXCLUDEFROMCAPTURE` ON):** Window rendered on screen locally; screen recording, window capture, and `PrintWindow` capture returned pure black pixels (`RGB = 0, 0, 0`).
- **Affinity Verification:** `GetWindowDisplayAffinity(hWnd)` returned `0x00000011`, confirming kernel-level affinity enforcement.

### Protection Scope Matrix

| Capture Scenario | Masked by DOOM? | Notes |
|---|---|---|
| **OBS Studio / Streamlabs (Window Capture)** | **YES (Masked to Black)** | DWM strips the window texture from the capture pipeline. |
| **Microsoft Teams / Zoom / Slack (Window Share)** | **YES (Masked to Black)** | Only the shared application is visible; DOOM overlay is invisible. |
| **Discord (Application Screen Share)** | **YES (Masked to Black)** | Participants cannot see the DOOM overlay floating over your code. |
| **Windows Snipping Tool (Window Screenshot)** | **YES (Masked to Black)** | Screen captures of the window return black pixels. |
| **Full Desktop Screen Sharing (Older WebRTC)** | **Partial / Depends on Client** | Some legacy conferencing tools capture the entire desktop framebuffer directly via unmanaged GDI. *Recommendation:* Share a specific application window rather than full desktop. |
| **Hardware Capture Cards (HDMI splitters, Elgato)** | **NO (Hardware Limitation)** | Hardware devices intercept the final HDMI/DisplayPort GPU signal after composition; Win32 software cannot block hardware signal splitters. |
| **Virtual Machine Hypervisors** | **NO (Virtualization Limitation)** | Hypervisors capturing the guest framebuffer directly bypass guest OS composition rules. |

---

## 3. Zero Data Persistence Architecture

DOOM enforces a strict **Zero-Persistence Guarantee**:

1. **Memory-Only Processing**:
   Selected text captured from your active application is held in volatile C# string buffers only for the duration of the active HTTP request.
2. **Immediate Garbage Collection**:
   Upon response completion or stream cancellation, string references are unlinked and subject to .NET CLR generational garbage collection.
3. **No Disk Caching**:
   - Captured prompts are **never** written to `%APPDATA%\DOOM\settings.json`.
   - AI responses are **never** cached, logged, or saved to local storage.
   - Audit verified: `%APPDATA%\DOOM\settings.json` contains only user preferences (model name, privacy toggle, theme selection).
4. **Google Gemini Interactions API `store = false`**:
   Requests to Google Gemini explicitly pass `"store": false` in the payload, instructing Google not to persist user prompts for model training.

---

## 4. Dual-Layer Password & Credential Shield

To prevent accidental submission of passwords, API keys, or secret tokens, DOOM employs two independent verification barriers before any captured text is processed:

### Barrier 1: UI Automation `IsPassword` Pattern
DOOM queries the native Windows UI Automation tree for the focused element:
```csharp
if (element.Current.IsPassword)
{
    throw new SecurityException("Password and secure entry fields are excluded from AI capture.");
}
```

### Barrier 2: Win32 `ES_PASSWORD` Window Style Check
Certain legacy Win32, WinForms, or custom frameworks fail to set the modern UI Automation `IsPassword` property. DOOM queries the native Win32 window style:
```csharp
const int GWL_STYLE = -16;
const int ES_PASSWORD = 0x0020;
var style = GetWindowLong(hWnd, GWL_STYLE);
if ((style & ES_PASSWORD) == ES_PASSWORD)
{
    throw new SecurityException("Secure text control rejected via Win32 ES_PASSWORD style.");
}
```

If either check triggers, the capture is rejected immediately with a user notification and **zero bytes are transmitted to Gemini**.

---

## 5. Hardware-Backed DPAPI Credential Storage

Your Google Gemini API key is encrypted using the **Windows Data Protection API (DPAPI)**:

```csharp
byte[] encrypted = ProtectedData.Protect(
    plaintextBytes,
    optionalEntropy: CryptographicEntropy,
    scope: DataProtectionScope.CurrentUser
);
```

### Security Properties:
- **No Plaintext on Disk**: Keys are written to `%APPDATA%\DOOM\credentials.dat` as encrypted ciphertext blocks.
- **User-Specific Binding**: Encrypted using a master key derived from your Windows user account credentials and backed by the local Windows LSA (Local Security Authority).
- **Cryptographic Entropy**: Uses an internal secondary salt, preventing other standard applications running under your user account from decrypting the file without knowing the entropy key.
- **Cannot be Copied to Other Machines**: If `%APPDATA%\DOOM\credentials.dat` is copied to another PC or VM, decryption fails with `CryptographicException`.

---

## 6. Clipboard Fallback Safeguards

DOOM relies primarily on **Windows UI Automation** to read text directly from the active application's accessibility tree without altering your system clipboard.

If you choose to opt into the secondary **Clipboard Fallback**:
1. **Disabled by Default**: You must explicitly enable clipboard fallback in Settings.
2. **Sequence Number Preservation**: DOOM queries `GetClipboardSequenceNumber` before backing up your clipboard. If another application or the user updates the clipboard during the capture window, DOOM detects the sequence discrepancy and **aborts restoration**, ensuring newer user clipboard data is never overwritten with stale content.

---

## 7. Structured Log Redaction

All internal application logs produced by DOOM pass through an automated `SensitiveDataRedactor`:

- **API Keys**: Any pattern resembling `AIzaSy...` or 39-character alphanumeric tokens is replaced with `[REDACTED_API_KEY]`.
- **Bearer Tokens**: JWTs and Authorization headers are masked.
- **Captured Text**: Prompt text and selected excerpts are never piped into log sinks.
- **Log Files**: Logs are formatted without user identifiers or captured context.

