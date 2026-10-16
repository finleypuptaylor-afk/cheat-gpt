# Included software

- **llama.cpp b11669**, MIT license: https://github.com/ggml-org/llama.cpp. Windows CPU archive SHA-256: `37b3efcf962847ca869745fd4ccf7f5e7e8dacfeff9fa3523b4ca8cdc66d632b`. The engine's license is preserved under Engine/LICENSE.txt.
- **Qwen 2.5 0.5B Instruct, Q4_K_M GGUF**, Apache 2.0: https://huggingface.co/Qwen/Qwen2.5-0.5B-Instruct. Standalone GGUF SHA-256: `c5396e06af294bd101b30dce59131a76d2b773e76950acc870eda801d3ab0515`. The existing verified model is extracted from the published Mac package during the build; that Mac ZIP is SHA-256 pinned to `ecb8ce4938df2a36b84e359353c12453479473a2312b50439709a13ba63e221a`. Its Apache license remains beside the weights. The Windows app has no Mac or Ollama runtime dependency.
- **Microsoft .NET 10 Windows Desktop runtime**, distributed through Microsoft's self-contained deployment mechanism. Runtime license and third-party notices provided in the publish output are preserved. See https://github.com/dotnet/windowsdesktop and https://github.com/dotnet/runtime.

Microsoft's Visual C++ Redistributable is not bundled or modified. If needed, the user installs the official runtime directly from Microsoft. No private signing keys or GitHub credentials are included in the app or package.
