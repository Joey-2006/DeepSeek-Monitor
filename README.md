# DeepSeek Monitor 🟦

A lightweight Windows system tray application that monitors your DeepSeek API balance and token usage.

## Features

- 🟦 **System tray icon** — sits in your taskbar notification area
- 💰 **Balance monitoring** — displays your DeepSeek account balance in real-time
- 📊 **Usage tracking** — shows today's token consumption (when API supports it)
- 🔄 **Auto-refresh** — updates every 60 seconds
- 🚀 **Auto-start** — launches on boot

## Requirements

- Windows 7+ / .NET Framework 4.0+
- A DeepSeek API key

## Installation

1. Download the latest release
2. Run DeepSeekMonitor.exe
3. Enter your DeepSeek API key when prompted (or create %LOCALAPPDATA%\DeepSeekMonitor\config.txt with pi_key=YOUR_KEY)

## Screenshot

*(Coming soon)*

## Build from Source

`ash
csc /target:winexe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:Microsoft.VisualBasic.dll /out:DeepSeekMonitor.exe DeepSeekMonitor.cs
`

## Privacy

- API key is stored locally only (%LOCALAPPDATA%\DeepSeekMonitor\config.txt)
- All data stays on your machine
- No telemetry or analytics
