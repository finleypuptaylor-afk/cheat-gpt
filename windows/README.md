# Cheat GPT for Windows

A native Windows sidebar with a text AI running on your PC. The engine, Qwen 2.5 0.5B model, and .NET runtime are included. No Ollama, API key, or account is needed for chat.

## Open

1. Download **Cheat-GPT-Windows-1.2-x64.zip** and choose **Extract All**. Keep the entire extracted folder together.
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

Starting with Windows 1.2, automatic updates are enabled by default. The app checks GitHub on startup and every 12 hours while it is running. New updates are downloaded in the background, verified with a pinned RSA signing key and SHA256, and installed after the app fully quits. The × button only hides the sidebar; choose **Quit Cheat GPT** to exit. Choose **Install update & restart** in the tray menu to install immediately when idle. Open **AI settings → Updates → Automatic updates** to switch automatic checks/install-on-quit off. **Check for updates…** in the tray menu works manually even when automatic checks are off.

Windows 1.0/1.1 have no updater, so replace them manually with 1.2 once. Subsequent signed Windows releases use the automatic channel below. Mac and Chrome updates remain separate.

Feed: https://github.com/finleypuptaylor-afk/cheat-gpt/releases/download/windows-updates/windows-update.json (detached RSA/SHA256 signature in windows-update.sig). Downloads come from the dedicated windows-updates release, not the repository's Mac latest release. Offline checks quietly retry next startup or the next scheduled check; chat still runs offline. Updates need write access to the extracted app folder. They never request administrator privileges.

The installer waits for the app to exit, takes its single-instance mutex, re-verifies the package, extracts into a new staging directory, rejects unsafe paths/duplicates/symlinks, and backs up replaced files. On a normal file-installation failure it restores the previous files. Preferences under LocalAppData and unrelated/model files are preserved by the runtime-only update package. Backups and installation errors are kept under `%LOCALAPPDATA%\Cheat GPT\Updates`. It does not guarantee recovery after a power failure or disk failure in the middle of file replacement.

## Source and build

`Sources/` contains the Windows Forms UI and local engine client. `Build/build.ps1` uses .NET 10 and checksum-pinned upstream assets to produce a self-contained Windows x64 bundle. It runs a Windows UI/inference smoke test, captures the rendered sidebar, checks cancellation and cleanup, then creates the ZIP.

The workflow in this repository builds and validates on a Windows runner before attaching the package to the shared release page. The runtime, model, and platform licenses remain in the package.

## AI settings (1.1)

Click the gear icon or choose AI settings from the tray menu. Installed RAM and CPU logical processor count are detected on this PC. **Use recommended** suggests context size, threads and maximum reply tokens for the bundled 0.5B model. **Save & apply** stores preferences under `%LOCALAPPDATA%\Cheat GPT\parameters.json` and restarts the engine automatically when context or threads change, retaining the open conversation.

Manual controls: context 4,096/8,192/16,384 tokens; CPU threads 1–16 capped by available processors; maximum reply tokens 64–1,024; temperature 0–1.5; Top P 0.1–1; Top K 1–100; repetition penalty 1–1.5. The property grid shows descriptions for each control. Values are validated before saving and inference. Other generation changes apply to the next reply. Settings persist; conversation text does not.

RAM recommendations are starting points, not benchmarks or memory reservations. Below 8 GB: 4,096 context, up to 2 threads, 256 reply tokens. At least 8 GB: 8,192 / 4 / 384. At least 16 GB: 16,384 / 6 / 768. Other apps need RAM too; detection reports total installed memory rather than free memory. Larger context retains up to 4/8/16 recent messages respectively, clipped to 700 characters each. It does not change the model’s intelligence.

This update adds parameter settings and cheerful replies to Windows. The 3.79 GB sectioned knowledge library/live Wikipedia retrieval and broader Chrome personalization controls remain in their existing Mac/Chrome implementations.
