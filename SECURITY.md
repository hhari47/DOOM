# Security Policy

## 1. Supported Versions

Security updates are actively maintained for the following versions of DOOM:

| Version | Supported | Operating System |
|---------|-----------|------------------|
| 1.0.x   | Yes       | Windows 11 (x64) |
| < 1.0.0 | No        | N/A              |

---

## 2. Reporting Security Vulnerabilities

We take the security and privacy of DOOM seriously. If you discover a potential vulnerability, please report it responsibly:

- **GitHub Security Advisories**: Please use [GitHub Private Vulnerability Reporting](https://github.com/hhari47/DOOM/security/advisories/new) on the DOOM repository.
- **Do not open public issues** for suspected vulnerabilities or sensitive disclosures.
- Provide a clear description, reproduction steps, affected environment, and impact assessment.

Reports will be reviewed, and verified fixes will be released in coordinated patch versions.

---

## 3. Credential Handling & Storage

DOOM is designed to protect your Google Gemini API key:

- **Local Storage via Windows DPAPI**: Credentials are encrypted at rest using the **Windows Data Protection API (DPAPI)** with `DataProtectionScope.CurrentUser` and custom cryptographic entropy (`credentials.dat`).
- **No Plaintext Persistence**: API keys are never written to `settings.json`, unencrypted files, application configuration, or SQLite databases.
- **In-Memory Redaction**: The domain model `ApiKey` overrides `ToString()` to unconditionally return `<redacted>`, preventing leakage via string interpolation or debugging tools.
- **Log Sanitization**: `SensitiveDataRedactor` scans all log streams and scrubs Google API key patterns, URL parameters, and `x-goog-api-key` headers before output is produced.
- **Transport Security**: All API traffic is routed exclusively over TLS 1.3 / HTTPS to Google's official endpoint (`https://generativelanguage.googleapis.com`). HTTP redirects are disabled (`AllowAutoRedirect = false`) to prevent token leakage across unverified redirects.

---

## 4. Privacy & Data Handling

- **Zero Server-Side Storage**: Requests to Gemini include `store: false` to ensure inputs and outputs are not retained for model training or stored on Google's servers.
- **Volatile Execution**: Selected text and generated responses reside strictly in volatile process memory. No chat history, selected documents, or prompts are written to disk.
- **Dual-Layer Password Shield**: Text selection inspection checks both Windows UI Automation (`IsPassword == true`) and Win32 control style masks (`ES_PASSWORD`). Password fields are unconditionally rejected before text acquisition.
- **No Telemetry**: DOOM does not include telemetry, analyticsSDKs, crash-reporting daemons, or user tracking.

---

## 5. Known Security & Privacy Limitations

1. **Local Administrator Access**: DPAPI encrypts credentials per Windows user account. Any process running in your active user session or elevated with Administrator rights can access DPAPI keys belonging to that user.
2. **Screen Capture Hooks**: `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` instructs the Windows Desktop Window Manager (DWM) compositor to exclude DOOM from capture. However, low-level kernel drivers or unmanaged screen scrapers running with elevated privileges may bypass DWM composition. For maximum privacy during screen sharing, use single-window sharing rather than entire-desktop sharing.

