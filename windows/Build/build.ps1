param([string]$OutputRoot = (Join-Path $PSScriptRoot '..\dist'))
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$PSNativeCommandUseErrorActionPreference = $true
$root = Split-Path $PSScriptRoot -Parent
$version = '1.2'
$bundle = Join-Path $OutputRoot 'CheatGPT-Windows'
$cache = Join-Path $OutputRoot 'build-cache'
$tests = Join-Path $OutputRoot 'validation'
New-Item -ItemType Directory -Force -Path $bundle,$cache,$tests | Out-Null

function Download-Verified($Url, $Path, $Hash) {
    if (!(Test-Path $Path) -or (Get-FileHash $Path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $Hash) {
        Invoke-WebRequest -Uri $Url -OutFile $Path
    }
    if ((Get-FileHash $Path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $Hash) { throw "Checksum mismatch: $Url" }
}
$engineZip = Join-Path $cache 'llama-windows.zip'
Download-Verified 'https://github.com/ggml-org/llama.cpp/releases/download/b11669/llama-b11669-bin-win-cpu-x64.zip' $engineZip '37b3efcf962847ca869745fd4ccf7f5e7e8dacfeff9fa3523b4ca8cdc66d632b'
$seedZip = Join-Path $cache 'Cheat-GPT-Mac-model-seed.zip'
Download-Verified 'https://github.com/finleypuptaylor-afk/cheat-gpt/releases/download/v1.2/Cheat-GPT-1.2.zip' $seedZip 'ecb8ce4938df2a36b84e359353c12453479473a2312b50439709a13ba63e221a'
[IO.File]::WriteAllBytes((Join-Path $root 'Sources\app.ico'), [Convert]::FromBase64String((Get-Content (Join-Path $PSScriptRoot 'app-icon.base64') -Raw).Trim()))
dotnet publish (Join-Path $root 'Sources\CheatGPT.csproj') -c Release -r win-x64 --self-contained true -o $bundle --nologo
if ($LASTEXITCODE -ne 0) { throw 'Windows app compilation failed' }
$engine = Join-Path $bundle 'Engine'
$model = Join-Path $bundle 'Models'
New-Item -ItemType Directory -Force -Path $engine,$model | Out-Null
$expanded = Join-Path $cache 'llama'
Expand-Archive -Path $engineZip -DestinationPath $expanded -Force
Get-ChildItem $expanded -Recurse -File | Where-Object { $_.Extension -eq '.dll' -or $_.Name -eq 'llama-server.exe' } | Copy-Item -Destination $engine
Copy-Item (Join-Path $PSScriptRoot 'LICENSE-llama.cpp.txt') (Join-Path $engine 'LICENSE.txt')
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($seedZip)
try {
    foreach ($name in @('qwen2.5-0.5b-instruct-q4_k_m.gguf','LICENSE-QWEN.txt')) {
        $entry = $archive.GetEntry("Cheat GPT.app/Contents/Resources/Models/$name")
        if (!$entry) { throw "Model seed is missing $name" }
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $model $name), $true)
    }
} finally { $archive.Dispose() }
if ((Get-FileHash (Join-Path $model 'qwen2.5-0.5b-instruct-q4_k_m.gguf') -Algorithm SHA256).Hash.ToLowerInvariant() -ne 'c5396e06af294bd101b30dce59131a76d2b773e76950acc870eda801d3ab0515') { throw 'Model checksum mismatch' }
Copy-Item (Join-Path $root 'README.md') (Join-Path $bundle 'README.md')
Copy-Item (Join-Path $root 'THIRD-PARTY.md') (Join-Path $bundle 'THIRD-PARTY.md')
$process = Start-Process -FilePath (Join-Path $bundle 'CheatGPT.exe') -ArgumentList @('--smoke-test', ('"' + $tests + '"')) -PassThru
if (!$process.WaitForExit(180000)) { $process.Kill($true); throw 'Windows UI and local inference test timed out' }
if ($process.ExitCode -ne 0 -or !(Test-Path (Join-Path $tests 'smoke-result.json'))) {
    if (Test-Path (Join-Path $tests 'smoke-error.txt')) { Get-Content (Join-Path $tests 'smoke-error.txt') }
    throw 'Windows UI and local inference test failed'
}
Get-Content (Join-Path $tests 'smoke-result.json')
$updateTests = Start-Process -FilePath (Join-Path $bundle 'CheatGPT.exe') -ArgumentList @('--updater-tests', ('"' + $tests + '"')) -PassThru
if (!$updateTests.WaitForExit(60000)) { $updateTests.Kill($true); throw 'Updater tests timed out' }
if ($updateTests.ExitCode -ne 0 -or !(Test-Path (Join-Path $tests 'updater-result.json'))) {
    if (Test-Path (Join-Path $tests 'updater-error.txt')) { Get-Content (Join-Path $tests 'updater-error.txt') }
    throw 'Updater verification and rollback tests failed'
}
Get-Content (Join-Path $tests 'updater-result.json')
Copy-Item (Join-Path $tests 'windows-sidebar.png') (Join-Path $OutputRoot 'Cheat-GPT-Windows-preview.png')
$zip = Join-Path $OutputRoot "Cheat-GPT-Windows-$version-x64.zip"
Compress-Archive -Path $bundle -DestinationPath $zip -CompressionLevel Optimal -Force
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $(Split-Path $zip -Leaf)" | Set-Content -Encoding ascii (Join-Path $OutputRoot 'Cheat-GPT-Windows-SHA256.txt')
Copy-Item $zip (Join-Path $tests (Split-Path $zip -Leaf))
Copy-Item (Join-Path $OutputRoot 'Cheat-GPT-Windows-SHA256.txt') $tests
$patchRoot = Join-Path $cache 'update-content'
$patchApp = Join-Path $patchRoot 'CheatGPT-Windows'
New-Item -ItemType Directory -Force -Path $patchApp | Out-Null
Get-ChildItem $bundle | Where-Object { $_.Name -ne 'Engine' -and $_.Name -ne 'Models' } | Copy-Item -Destination $patchApp -Recurse
$patch = Join-Path $tests "Cheat-GPT-Windows-$version-update.zip"
Compress-Archive -Path $patchApp -DestinationPath $patch -CompressionLevel Optimal -Force
"$((Get-FileHash $patch -Algorithm SHA256).Hash.ToLowerInvariant())  $(Split-Path $patch -Leaf)" | Set-Content -Encoding ascii (Join-Path $tests 'Windows-update-SHA256.txt')
Write-Host "Validated and packaged: $zip"
