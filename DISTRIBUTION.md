# ResizeMe Distribution Guide

## What You Get

Two `.exe` files in the `releases/` folder:
- **ResizeMe-x64.exe** (~152 KB) — 64-bit version, recommended for most modern Windows systems
- **ResizeMe-x86.exe** (~121 KB) — 32-bit version, for older systems or 32-bit Windows

## Requirements

**Windows 10 (build 17763) or later**  
**.NET 8 Runtime** (downloads automatically on first run if not present)

## Installation

1. Download the `.exe` for your system (usually **x64**)
2. Run it directly—no unzip, no folder clutter
3. On first launch, Windows may ask for permission (this is normal; you can click "More info" → "Run anyway")
4. ResizeMe runs from the tray; use `Ctrl+Win+R` to toggle visibility

## Security & Windows Defender

**Windows Defender Warning on First Run:**
When you first run an unsigned `.exe` downloaded from the internet, Windows Defender SmartScreen may display a warning. This is normal and expected behavior for unsigned applications from unknown publishers.

### What You Might See
- **SmartScreen Popup:** *"Windows Defender SmartScreen prevented an unrecognized app from starting. Running this app might put your PC at risk."*
- This appears because ResizeMe is not yet code-signed or published through the Microsoft Store

### How to Safely Proceed

**Option 1: Run Anyway (Recommended for personal use)**
- Click **"More info"** in the SmartScreen dialog
- Click **"Run anyway"** to launch the application
- ResizeMe opens normally; the warning appears only on first download

**Option 2: Permanently Exclude the File (Alternative)**
- Open **Windows Defender Security Center** (search "Defender" in Start menu)
- Go to **Virus & threat protection** → **Manage settings**
- Scroll to **Exclusions** → **Add exclusions**
- Select **File** and choose the ResizeMe `.exe`
- Future runs will not trigger the warning

**Option 3: Code-Signed Release (For Team Distribution)**
- Self-signed code-signing available upon request
- Contact maintainers for signed `.exe` files (removes SmartScreen entirely)
- Signed releases remain functionally identical to unsigned builds

**Option 4: Windows Store (Long-term solution)**
- Planned for future release
- Store-published apps are pre-verified by Microsoft
- No SmartScreen warnings for Store-installed applications

## Building from Source

```bash
cd ResizeMe
./tools/build-releases.bat
```

Output: `releases/ResizeMe-x64.exe`, `releases/ResizeMe-x86.exe`

## Troubleshooting

**"The .NET runtime was not found"**  
→ Install .NET 8 from https://dotnet.microsoft.com/download/dotnet/8.0

**"Windows blocked this app"**  
→ Click "More info" → "Run anyway", or whitelist in Defender

**"App crashes on startup"**  
→ Run `dotnet --info` to verify .NET 8 is installed correctly

## What's Inside

ResizeMe uses **WinUI 3** (modern Windows UI framework) and includes:
- Global hotkey registration (Ctrl+Win+R)
- System tray integration
- Preset window sizes
- Settings window for customization

No telemetry, no ads—completely open-source.
