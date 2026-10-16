# Cheat GPT for Windows

A native Windows sidebar with a text AI running on your PC. The engine, Qwen 2.5 0.5B model, and .NET runtime are included. No Ollama, API key, or account is needed for chat.

## Open

1. Download **Cheat-GPT-Windows-1.0-x64.zip** and choose **Extract All**. Keep the entire extracted folder together.
2. Open **CheatGPT.exe** inside **CheatGPT-Windows**.
3. Wait for **On-device · Ready**, then type a message and press Enter. Shift–Enter adds a new line.

Press **Ctrl–Alt–Space** to show or hide the sidebar. Its tray icon also opens it; if Windows hides the icon, look under the tray's arrow. The × button hides the sidebar. Choose **Quit Cheat GPT** from its tray menu to stop the app and model.

Use the stop square to cancel an answer, Copy to copy a message, and + for a new conversation. There are no premade prompts.

## Requirements

- Windows 10 or 11, 64-bit Intel/AMD PC. This is not a native Windows ARM build.
- At least 4 GB of RAM is recommended; close other memory-heavy applications if loading fails.
- The engine uses the CPU. A dedicated graphics card is not required.
- Some PCs need Microsoft's Visual C++ v14 x64 runtime. If loading reports missing VCRUNTIME/MSVCP libraries or error 0xC0000135, choose **Install engine runtime** from the tray menu. It opens Microsoft's official download: https://aka.ms/vc14/vc_redist.x64.exe. Install it, then click Retry in Cheat GPT. That first-time setup needs internet; chat runs offline afterwards.

The app is not Microsoft Store distributed or signed with a Windows publisher certificate. Windows may show its normal downloaded-software prompt.

## Privacy and updates

The app launches its bundled model on loopback with a fresh authentication token, offline mode, no web UI, proxies disabled, and engine output logging disabled. Messages stay in memory, and no transcript is saved. Quitting clears the conversation and stops the engine; hiding the sidebar keeps it ready.

The small starter model is intended for basic text tasks. Start a new conversation if the context becomes too long.

This first Windows version uses manual updates: quit the app and replace the extracted folder with a newer Windows release. The Mac updater and Chrome extension do not update this Windows app.

## Source and build

`Sources/` contains the Windows Forms UI and local engine client. `Build/build.ps1` uses .NET 10 and checksum-pinned upstream assets to produce a self-contained Windows x64 bundle. It runs a Windows UI/inference smoke test, captures the rendered sidebar, checks cancellation and cleanup, then creates the ZIP.

The workflow in this repository builds and validates on a Windows runner before attaching the package to the shared release page. The runtime, model, and platform licenses remain in the package.
