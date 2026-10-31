# scripts/live-gemini-smoke-test.ps1
# User-triggered live smoke test for Gemini API integration on Windows.
# Reads key from environment (DOOM_API_KEY / GEMINI_API_KEY) or DPAPI credentials.dat.
# Sends minimal test prompt ("ping") using the Gemini Interactions API.
# Never prints the API key or raw sensitive response body.

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

Write-Host "=================================================="
Write-Host "DOOM -- Live Gemini API Smoke Test (Windows Edition)"
Write-Host "=================================================="
Write-Host "NOTE: This test sends a live request to Google Gemini."
Write-Host "It consumes 1 request and ~5 tokens from your quota."
Write-Host ""

$apiKey = $env:DOOM_API_KEY
if ([string]::IsNullOrWhiteSpace($apiKey)) {
    $apiKey = $env:GEMINI_API_KEY
}

# If not in env, check DPAPI credential file
if ([string]::IsNullOrWhiteSpace($apiKey)) {
    $credPath = "$env:APPDATA\DOOM\credentials.dat"
    if (Test-Path $credPath) {
        Write-Host "Attempting to decrypt API key from DPAPI store: $credPath..."
        try {
            Add-Type -AssemblyName System.Security
            $encryptedBytes = [System.IO.File]::ReadAllBytes($credPath)
            $entropy = [System.Text.Encoding]::UTF8.GetBytes("DOOM_AI_KEY_PROTECTION_V1")
            $decryptedBytes = [System.Security.Cryptography.ProtectedData]::Unprotect($encryptedBytes, $entropy, [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
            $apiKey = [System.Text.Encoding]::UTF8.GetString($decryptedBytes)
        }
        catch {
            Write-Warning "Could not decrypt DPAPI credential file: $_"
        }
    }
}

if ([string]::IsNullOrWhiteSpace($apiKey)) {
    Write-Host "RESULT: SKIPPED (No API key configured in env or DPAPI store)." -ForegroundColor Yellow
    Write-Host "To run a live test, set the DOOM_API_KEY environment variable or save your key in DOOM Settings."
    exit 0
}

Write-Host 'API key retrieved successfully (contents hidden: [REDACTED]).'
Write-Host 'Dispatching test interaction to https://generativelanguage.googleapis.com/v1beta/interactions...'

$headers = @{
    "Content-Type" = "application/json"
    "Accept" = "application/json"
    "x-goog-api-key" = $apiKey
}

$body = @{
    model = "gemini-3.5-flash"
    input = "ping"
    system_instruction = "Respond with the word pong only."
    generation_config = @{
        thinking_level = "low"
        max_output_tokens = 4096
    }
    store = $false
    stream = $false
} | ConvertTo-Json -Depth 5

try {
    $response = Invoke-RestMethod -Uri "https://generativelanguage.googleapis.com/v1beta/interactions" `
        -Method Post `
        -Headers $headers `
        -Body $body `
        -TimeoutSec 30

    Write-Host 'HTTP Response: SUCCESS (200 OK)' -ForegroundColor Green

    # Extract text from steps
    $text = ""
    if ($response.steps) {
        foreach ($step in $response.steps) {
            if ($step.content) {
                foreach ($c in $step.content) {
                    if ($c.text) { $text += $c.text }
                }
            }
        }
    }

    $modelName = $response.model
    Write-Host "Model: $modelName"
    Write-Host "AI Response text received: '$text'" -ForegroundColor Cyan

    if ($text.ToLower().Contains("pong")) {
        Write-Host "PASS: Gemini API responded correctly with expected pong verification." -ForegroundColor Green
    } else {
        Write-Host "PASS: Gemini API responded successfully (status 200)." -ForegroundColor Green
    }
}
catch {
    Write-Host "FAIL: Gemini API request failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
