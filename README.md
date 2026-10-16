# Cheat GPT

A local text AI for your Mac, available as a native sidebar and a Chrome sidebar extension. The Mac app includes its inference engine and Qwen 2.5 0.5B model. No Ollama, account, or API key is needed for chat.

## Downloads

- [Mac app 1.2](https://github.com/finleypuptaylor-afk/cheat-gpt/releases/download/v1.2/Cheat-GPT-1.2.zip)
- [Chrome sidebar extension 1.0](https://github.com/finleypuptaylor-afk/cheat-gpt/releases/download/v1.2/Cheat-GPT-Chrome-1.0.zip)
- [Release notes](https://github.com/finleypuptaylor-afk/cheat-gpt/releases/tag/v1.2)

Both versions require an Apple Silicon Mac running macOS 13.3 or later. The Chrome version also requires Chrome 116+ and the Mac app's bundled model.

## Mac app

Extract the Mac ZIP and move **Cheat GPT.app** into Applications. Press **Control–Option–Space** to show or hide the sidebar. Type a message and press Return or click the send arrow. Stop an answer with the stop square. Quit from the sidebar icon in the menu bar.

The app is ad-hoc signed and has not been Apple notarized. Your Mac may require its normal approval to open downloaded software.

## Chrome sidebar

1. Install the Mac app above and put it in Applications.
2. Extract the Chrome ZIP and keep its folder somewhere permanent.
3. Run **Install Chrome Bridge.command** from that folder once.
4. Open `chrome://extensions`, turn on **Developer mode**, click **Load unpacked**, and choose the download's **Extension** folder.
5. Pin Cheat GPT in Chrome's extension menu, then click its icon.

The extension supports streamed replies, stopping generation, copying messages, and starting a new conversation. It has no premade prompts. It requests only side-panel and native-messaging access; it does not read your webpages, tabs, or browsing history.

This is an unpacked developer extension, not a Chrome Web Store installation. If you move the Mac app, run the companion installer again. The companion reuses the Mac app's local model; no second model download is needed.

## Updates

The Mac app checks this repository's signed release feed automatically. Updates download in the background and install when the app quits. Use **Check for Updates…** in the menu bar for a manual check, or **Automatic Updates** to change the setting. Earlier Mac builds need version 1.2 installed once to switch to this feed.

The unpacked Chrome extension is updated manually by installing future Chrome packages. It does not inherit the Mac app's updater.

## Privacy

Chat inference stays on the Mac and does not need internet. Conversations remain in memory and disappear when the app or sidebar is closed or a new conversation is started. The small bundled model is intended for basic text tasks. Mac update checks and downloads contact GitHub; no private signing key is published.

## Included software

- [llama.cpp](https://github.com/ggml-org/llama.cpp), MIT license.
- [Qwen 2.5 0.5B Instruct](https://huggingface.co/Qwen/Qwen2.5-0.5B-Instruct), Apache 2.0 license, Q4_K_M GGUF.
- [Sparkle](https://sparkle-project.org/), with its license and notices included.

Third-party licenses are preserved inside the Mac application bundle. This repository hosts the public downloads and signed Mac update feed.
