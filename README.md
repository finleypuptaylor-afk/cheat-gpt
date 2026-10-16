# Cheat GPT

A native macOS sidebar for text chat with an AI running on your Mac. The AI engine and Qwen 2.5 0.5B model are included. No Ollama, account, or API key is needed for chat.

## Download

Get the ZIP from the [latest release](https://github.com/finleypuptaylor-afk/cheat-gpt/releases/latest), extract it, and move **Cheat GPT.app** into Applications. This build supports Apple Silicon Macs running macOS 13.3 or later.

The app is ad-hoc signed and has not been Apple notarized. Your Mac may require its normal approval to open a downloaded app.

## Use

- Press **Control–Option–Space** to show or hide the sidebar.
- Type a message and press Return or click the send arrow.
- Stop an answer with the stop square.
- Quit from the sidebar icon in the menu bar.

Chat runs locally and does not need internet. Conversations stay in memory and disappear when you quit or start a new conversation. The included small model is intended for basic text tasks.

## Automatic updates

Version 1.2 includes automatic checks against this repository's releases. Signed updates download in the background and install when the app quits. Use **Check for Updates…** in the menu bar to check manually, or **Automatic Updates** to enable or disable automatic updates.

Older builds must be replaced with version 1.2 once to switch to this release feed. Update checks and downloads need internet; chat inference stays on the Mac. Update feeds and downloads require signatures from the publisher's signing key. The private key is not published.

## Included software

- [llama.cpp](https://github.com/ggml-org/llama.cpp), MIT license.
- [Qwen 2.5 0.5B Instruct](https://huggingface.co/Qwen/Qwen2.5-0.5B-Instruct), Apache 2.0 license, quantized as Q4_K_M GGUF.
- [Sparkle](https://sparkle-project.org/), with its license and notices included.

Licenses are preserved inside the application bundle. This repository hosts the public release downloads and signed update feeds.
