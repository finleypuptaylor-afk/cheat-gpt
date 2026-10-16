using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace CheatGPT;

public sealed record ChatMessage(string Role, string Content);

public sealed class EngineClient : IDisposable
{
    private readonly string _directory;
    private readonly HttpClient _http = new(new SocketsHttpHandler { UseProxy = false }) { Timeout = TimeSpan.FromMinutes(10) };
    private readonly SemaphoreSlim _startup = new(1, 1);
    private readonly string _token = Guid.NewGuid().ToString("N");
    private Process? _process;
    private Uri? _base;
    public bool Ready { get; private set; }
    public int? EngineProcessId => _process is { HasExited: false } ? _process.Id : null;
    private bool _disposed;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public EngineClient(string directory) => _directory = directory;

    public async Task StartAsync(CancellationToken cancellation)
    {
        await _startup.WaitAsync(cancellation);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (Ready && _process is { HasExited: false }) return;
            var engine = Path.Combine(_directory, "Engine", "llama-server.exe");
            var model = Path.Combine(_directory, "Models", "qwen2.5-0.5b-instruct-q4_k_m.gguf");
            if (!File.Exists(engine) || !File.Exists(model))
                throw new InvalidOperationException("The model or engine is missing. Extract the whole ZIP before opening CheatGPT.exe.");
            StopEngine();
            var reservation = new TcpListener(IPAddress.Loopback, 0);
            reservation.Start();
            var port = ((IPEndPoint)reservation.LocalEndpoint).Port;
            reservation.Stop();
            _base = new Uri($"http://127.0.0.1:{port}/");
            var start = new ProcessStartInfo(engine)
            {
                WorkingDirectory = Path.GetDirectoryName(engine)!, UseShellExecute = false,
                CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                RedirectStandardInput = true
            };
            foreach (var key in start.Environment.Keys.Where(k => k.StartsWith("LLAMA_", StringComparison.OrdinalIgnoreCase)).ToList())
                start.Environment.Remove(key);
            foreach (var arg in new[] { "--model", model, "--alias", "local", "--host", "127.0.0.1", "--port", port.ToString(),
                "--api-key", _token, "--ctx-size", "4096", "--parallel", "1", "--n-gpu-layers", "0", "--offline", "--no-webui", "--log-disable" })
                start.ArgumentList.Add(arg);
            _process = new Process { StartInfo = start };
            _process.OutputDataReceived += (_, _) => { };
            _process.ErrorDataReceived += (_, _) => { };
            if (!_process.Start()) throw new InvalidOperationException("The local AI engine could not start.");
            _process.StandardInput.Close();
            _process.BeginOutputReadLine(); _process.BeginErrorReadLine();
            for (var attempt = 0; attempt < 180; attempt++)
            {
                cancellation.ThrowIfCancellationRequested();
                if (_process.HasExited)
                    throw new InvalidOperationException($"The engine stopped (0x{unchecked((uint)_process.ExitCode):X8}). If a Visual C++ runtime is missing, use Install engine runtime from the tray menu, then Retry.");
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                    timeout.CancelAfter(TimeSpan.FromSeconds(2));
                    using var request = Request("health");
                    using var response = await _http.SendAsync(request, timeout.Token);
                    if (response.IsSuccessStatusCode)
                    {
                        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
                        if (json.RootElement.TryGetProperty("status", out var state) && state.GetString() == "ok")
                        { Ready = true; return; }
                    }
                }
                catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { }
                catch (HttpRequestException) { }
                await Task.Delay(500, cancellation);
            }
            throw new TimeoutException("The model took too long to load. Close memory-heavy programs and click Retry.");
        }
        catch { StopEngine(); throw; }
        finally { _startup.Release(); }
    }

    private HttpRequestMessage Request(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_base ?? throw new InvalidOperationException("Model is not ready."), path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return request;
    }

    public async IAsyncEnumerable<string> ChatAsync(IReadOnlyList<ChatMessage> history, [EnumeratorCancellation] CancellationToken cancellation)
    {
        if (!Ready || _process is not { HasExited: false }) throw new InvalidOperationException("The engine is not running. Click Retry to restart it.");
        using var request = Request("v1/chat/completions");
        request.Method = HttpMethod.Post;
        request.Content = new StringContent(JsonSerializer.Serialize(new { model = "local", messages = history.TakeLast(16), stream = true, max_tokens = 1024 }, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("The model could not answer. If this conversation is long, start a new one and try again.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellation);
        using var reader = new StreamReader(stream);
        var completed = false;
        while (await reader.ReadLineAsync(cancellation) is { } line)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!line.StartsWith("data: ", StringComparison.Ordinal)) continue;
            var payload = line[6..];
            if (payload == "[DONE]") { completed = true; break; }
            using var chunk = JsonDocument.Parse(payload);
            if (chunk.RootElement.TryGetProperty("error", out _)) throw new InvalidOperationException("The local model returned an error.");
            if (chunk.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("delta", out var delta) && delta.TryGetProperty("content", out var text) && text.GetString() is { Length: > 0 } content)
                yield return content;
        }
        if (!completed) throw new IOException("The local connection ended before the reply finished. Try your message again.");
    }

    private void StopEngine()
    {
        Ready = false;
        if (_process is null) return;
        try { if (!_process.HasExited) { _process.Kill(entireProcessTree: true); _process.WaitForExit(5000); } }
        catch (InvalidOperationException) { }
        finally { _process.Dispose(); _process = null; }
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; StopEngine(); _http.Dispose();
    }
}
