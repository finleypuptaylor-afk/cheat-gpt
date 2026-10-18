# Cheat GPT

A text AI running locally on your computer, available as a native Mac sidebar, a Windows sidebar, and a Chrome sidebar extension for Mac. The desktop apps include the engine and Qwen 2.5 0.5B model. No Ollama, account, or API key is needed for chat.

## Downloads

- [Windows app 1.2 — x64, automatic updates](https://github.com/finleypuptaylor-afk/cheat-gpt/releases/download/windows-updates/Cheat-GPT-Windows-1.2-x64.zip)
- [Mac app 1.2 — Apple Silicon](https://github.com/finleypuptaylor-afk/cheat-gpt/releases/download/v1.2/Cheat-GPT-1.2.zip)
- [Chrome sidebar extension 1.0 — Mac companion](https://github.com/finleypuptaylor-afk/cheat-gpt/releases/download/v1.2/Cheat-GPT-Chrome-1.0.zip)
- [Release notes and checksums](https://github.com/finleypuptaylor-afk/cheat-gpt/releases/tag/v1.2)

## Windows

For 64-bit Intel/AMD Windows 10 or 11 PCs. Download the Windows ZIP, choose **Extract All**, and open **CheatGPT.exe** inside **CheatGPT-Windows**. Keep the whole extracted folder together. The .NET runtime, inference engine, and model are included; no dedicated GPU is required.

Press **Ctrl–Alt–Space** to show or hide the sidebar, or use its system tray icon. Enter sends a message; Shift–Enter adds a line. Stop cancels an answer, Copy copies a message, and + starts a new conversation. The × button hides it. **Quit Cheat GPT** in the tray menu stops the app and model.

If a PC needs Microsoft's Visual C++ runtime, choose **Install engine runtime** from the tray menu for Microsoft's official installer, then Retry. The executable is unsigned; its updater verifies separately signed release metadata and package hashes. Windows may show its normal downloaded-software prompt.

The package was built and tested on a Windows x64 runner: streamed local replies, cancellation, clearing chat, and stopping the model process passed. SHA-256 checksums are attached to the Windows release. Signing, path validation, rollback on locked files, and preserving existing model/preferences were checked on the Windows runner. Windows source and build scripts are in [windows/](windows/).

## Mac app

Requires an Apple Silicon Mac running macOS 13.3 or later. Extract the Mac ZIP and move **Cheat GPT.app** into Applications. Press **Control–Option–Space** to show or hide it. Type a message and press Return or click send. Quit from its menu bar icon.

The app is ad-hoc signed and has not been Apple notarized. Your Mac may require its normal approval to open downloaded software.

## Chrome sidebar

Requires Chrome 116+, an Apple Silicon Mac with macOS 13.3+, and the Mac app's bundled model.

1. Put the Mac app in Applications.
2. Extract the Chrome ZIP and keep its folder somewhere permanent.
3. Run **Install Chrome Bridge.command** once.
4. Open `chrome://extensions`, enable **Developer mode**, choose **Load unpacked**, and select the download's **Extension** folder.
5. Pin Cheat GPT in Chrome and click its icon.

The extension has streaming replies, stop/copy controls, new conversation, and no premade prompts. It requests only side-panel and native-messaging access; it does not read webpages, tabs, or browsing history. It is an unpacked extension, not a Chrome Web Store installation. If you move the Mac app, run the companion installer again.

## Updates

The Mac app checks this repository's signed feed automatically. Updates download in the background and install when the app quits. Use **Check for Updates…** in its menu for a manual check. Earlier Mac builds need version 1.2 installed once to switch feeds.

Windows 1.2 checks its separate signed Windows update feed on startup and every 12 hours while running. Verified updates install when you quit; during Windows shutdown, installation is deferred to the next app launch. Choose **Install update & restart** from the tray menu to apply a downloaded update immediately, or **Check for updates…** for a manual check. **AI settings → Updates → Automatic updates** controls automatic checks and installation. Windows 1.0/1.1 need one manual replacement with 1.2 to gain the updater. [Windows update release and checksums](https://github.com/finleypuptaylor-afk/cheat-gpt/releases/tag/windows-updates).

The unpacked Chrome extension still updates manually and does not inherit either desktop updater.

## Privacy

Chat inference stays on your computer and works offline after setup. Conversations remain in memory; quitting a desktop app or starting a new conversation clears them. Hiding a desktop sidebar keeps it ready. Closing the Chrome sidebar clears its conversation and disconnects its companion. No transcripts are saved. The small model is intended for basic text tasks. Update checks and downloads contact GitHub; no private signing keys are published.

## Included software

- [llama.cpp](https://github.com/ggml-org/llama.cpp), MIT license.
- [Qwen 2.5 0.5B Instruct](https://huggingface.co/Qwen/Qwen2.5-0.5B-Instruct), Apache 2.0 license, Q4_K_M GGUF.
- [Sparkle](https://sparkle-project.org/) in the Mac app.
- [Microsoft .NET](https://github.com/dotnet/runtime) Windows Desktop runtime in the Windows package.

Third-party licenses and notices are preserved with the desktop packages. This repository hosts the downloads and signed Mac update feed.
