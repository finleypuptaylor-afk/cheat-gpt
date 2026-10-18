using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace CheatGPT;

internal sealed record UpdateFeed(string Version, string Url, string Sha256, long Size);
internal sealed record InstallPlan(string Root, string Stage, int ParentPid, long ParentStartTicks, bool Relaunch);

internal sealed class AppUpdater
{
    internal const string ReleaseBase = "https://github.com/finleypuptaylor-afk/cheat-gpt/releases/download/windows-updates/";
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    internal static Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0,0,0);
    private readonly string _root;
    private readonly string _updates;
    private string Marker => Path.Combine(_updates,"pending.txt");
    private readonly SemaphoreSlim _gate = new(1,1);
    private string? _stage;
    public bool Ready => _stage is not null;
    public event Action<string>? Status;
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(15) };

    internal AppUpdater(string root)
    {
        _root=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var identity=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(_root.ToUpperInvariant())))[..16];
        _updates=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Cheat GPT","Updates",identity);
        try { Directory.CreateDirectory(_updates);
        if (File.Exists(Marker)) {
            var saved=File.ReadAllText(Marker).Trim();
            var path=Path.GetFullPath(saved);
            if (path.StartsWith(_updates+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) && Directory.Exists(path)) _stage=path;
        } } catch { _stage=null; }
    }
    internal static string PublicKey {
        get { using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("CheatGPT.UpdatePublicKey") ?? throw new InvalidDataException("Update verification key is missing."); using var reader=new StreamReader(stream); return reader.ReadToEnd(); }
    }
    internal static UpdateFeed VerifyFeed(byte[] bytes,byte[] signature,string? key=null)
    {
        if (bytes.Length>65536 || signature.Length>4096) throw new InvalidDataException("Update metadata is too large.");
        using var rsa=RSA.Create(); rsa.ImportFromPem(key??PublicKey);
        if (!rsa.VerifyData(bytes,signature,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1)) throw new InvalidDataException("The update signature is invalid. Nothing was installed.");
        var feed=JsonSerializer.Deserialize<UpdateFeed>(bytes,Json) ?? throw new InvalidDataException("Update metadata is missing.");
        if (!Version.TryParse(feed.Version,out _) || !Uri.TryCreate(feed.Url,UriKind.Absolute,out var url) || url.Scheme!="https" || !feed.Url.StartsWith(ReleaseBase,StringComparison.Ordinal) || url.Query.Length!=0 || url.Fragment.Length!=0 || feed.Size<1 || feed.Size>536870912 || feed.Sha256 is null || feed.Sha256.Length!=64 || !feed.Sha256.All(Uri.IsHexDigit)) throw new InvalidDataException("Unsupported update metadata.");
        return feed;
    }
    internal static bool IsNewer(UpdateFeed feed,Version current) => Version.Parse(feed.Version)>current;
    private static async Task<byte[]> GetSmallAsync(string url,int max,CancellationToken cancellation)
    {
        using var response=await Client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,cancellation); response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength>max) throw new InvalidDataException("Update metadata is too large.");
        await using var stream=await response.Content.ReadAsStreamAsync(cancellation); using var output=new MemoryStream();
        var buffer=new byte[8192]; int read;
        while ((read=await stream.ReadAsync(buffer,cancellation))>0) { if (output.Length+read>max) throw new InvalidDataException("Update metadata is too large."); output.Write(buffer,0,read); }
        return output.ToArray();
    }
    public async Task<string> CheckAsync(CancellationToken cancellation)
    {
        if (!await _gate.WaitAsync(0,cancellation)) return "An update check is already running.";
        try {
            Status?.Invoke("Checking for updates…");
            using var metadataTimeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation); metadataTimeout.CancelAfter(TimeSpan.FromSeconds(15));
            var feedBytes=await GetSmallAsync(ReleaseBase+"windows-update.json",65536,metadataTimeout.Token);
            var signature=await GetSmallAsync(ReleaseBase+"windows-update.sig",4096,metadataTimeout.Token);
            var feed=VerifyFeed(feedBytes,signature);
            if (!IsNewer(feed,CurrentVersion)) { _stage=null; if(File.Exists(Marker))File.Delete(Marker); Status?.Invoke("Windows app is up to date"); return "Cheat GPT is up to date."; }
            if (_stage is not null) {
                try { var queued=VerifyFeed(File.ReadAllBytes(Path.Combine(_stage,"feed.json")),File.ReadAllBytes(Path.Combine(_stage,"feed.sig"))); if(queued.Version==feed.Version) { Status?.Invoke("Update ready · installs when you quit"); return "Update "+feed.Version+" is ready. Quit the app to install, or choose Install update & restart."; } } catch { _stage=null; }
            }
            Status?.Invoke("Downloading Windows update "+feed.Version+"…");
            var stage=Path.Combine(_updates,Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
            try {
                var package=Path.Combine(stage,"package.zip");
                using var response=await Client.GetAsync(feed.Url,HttpCompletionOption.ResponseHeadersRead,cancellation); response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is long size && size!=feed.Size) throw new InvalidDataException("Update size does not match its signed metadata.");
                await using(var input=await response.Content.ReadAsStreamAsync(cancellation))
                await using(var output=new FileStream(package,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                    var buffer=new byte[131072]; long count=0; int read;
                    while((read=await input.ReadAsync(buffer,cancellation))>0) { count+=read; if(count>feed.Size)throw new InvalidDataException("Update exceeds its signed size."); await output.WriteAsync(buffer.AsMemory(0,read),cancellation); }
                    if(count!=feed.Size)throw new InvalidDataException("Update download was incomplete.");
                }
                // Validate every entry before advertising the update; the helper verifies again at install time.
                await Task.Run(() => VerifyAndExtract(feed,package,Path.Combine(stage,"checked")),cancellation);
                await File.WriteAllBytesAsync(Path.Combine(stage,"feed.json"),feedBytes,cancellation);
                await File.WriteAllBytesAsync(Path.Combine(stage,"feed.sig"),signature,cancellation);
                await File.WriteAllTextAsync(Marker,stage,cancellation); _stage=stage;
            } catch { try { Directory.Delete(stage,true); } catch {} throw; }
            Status?.Invoke("Update ready · installs when you quit");
            return "Update "+feed.Version+" is downloaded and verified. Quit Cheat GPT to install it, or choose Install update & restart.";
        } finally { _gate.Release(); }
    }
    internal static void NoLinks(string path)
    {
        var current=new DirectoryInfo(Path.GetFullPath(path));
        for(var d=current;d is not null;d=d.Parent) if(d.Exists && (d.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Automatic updates cannot use a linked or redirected app directory.");
    }
    internal static void VerifyAndExtract(UpdateFeed feed,string package,string destination)
    {
        NoLinks(Path.GetDirectoryName(destination)!);
        if(Directory.Exists(destination))throw new IOException("Update extraction must use a new directory.");
        using var file=new FileStream(package,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(file.Length!=feed.Size || !Convert.ToHexString(SHA256.HashData(file)).Equals(feed.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Update checksum is invalid. Nothing was installed.");
        file.Position=0; using var archive=new ZipArchive(file,ZipArchiveMode.Read);
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase); long expanded=0;
        if(archive.Entries.Count>4096)throw new InvalidDataException("Too many update entries.");
        var entries=new List<(ZipArchiveEntry Entry,string Relative)>();
        foreach(var entry in archive.Entries) {
            var name=entry.FullName.Replace('\\','/');
            if(name=="CheatGPT-Windows/")continue;
            if(!name.StartsWith("CheatGPT-Windows/",StringComparison.Ordinal))throw new InvalidDataException("Invalid update folder.");
            var relative=name[17..];
            if(relative.Length==0)continue;
            var components=relative.TrimEnd('/').Split('/');
            if(components.Any(p=>p.Length==0 || p=="." || p==".." || p.EndsWith('.') || p.EndsWith(' ') || p.IndexOfAny(Path.GetInvalidFileNameChars())>=0) || ((entry.ExternalAttributes>>16)&0xf000)==0xa000)throw new InvalidDataException("Unsafe update path.");
            var full=Path.GetFullPath(Path.Combine(destination,relative));
            if(!full.StartsWith(Path.GetFullPath(destination)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) || !seen.Add(relative.TrimEnd('/')))throw new InvalidDataException("Duplicate or escaping update path.");
            if(name.EndsWith('/'))continue;
            expanded=checked(expanded+entry.Length); if(expanded>1073741824)throw new InvalidDataException("Update expands beyond its size limit.");
            entries.Add((entry,relative));
        }
        if(!seen.Contains("CheatGPT.exe") || !seen.Contains("CheatGPT.dll") || !seen.Contains("CheatGPT.runtimeconfig.json"))throw new InvalidDataException("Update is missing app files.");
        Directory.CreateDirectory(destination);
        foreach(var item in entries) {var target=Path.Combine(destination,item.Relative);Directory.CreateDirectory(Path.GetDirectoryName(target)!);item.Entry.ExtractToFile(target,false);}
    }
    internal static void ApplyTransaction(string payload,string root,string backup)
    {
        NoLinks(root); Directory.CreateDirectory(backup);
        var changed=new List<(string Target,string Backup,bool Original)>();
        try {
            foreach(var source in Directory.GetFiles(payload,"*",SearchOption.AllDirectories).OrderBy(p=>Path.GetRelativePath(payload,p),StringComparer.Ordinal)) {
                var relative=Path.GetRelativePath(payload,source); var target=Path.Combine(root,relative); var saved=Path.Combine(backup,relative);
                NoLinks(Path.GetDirectoryName(target)!); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if(File.Exists(target) && (File.GetAttributes(target)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked app file cannot be updated.");
                var original=File.Exists(target);
                if(original){Directory.CreateDirectory(Path.GetDirectoryName(saved)!);File.Move(target,saved);}
                changed.Add((target,saved,original)); File.Copy(source,target,false);
            }
        } catch(Exception installError) {
            var failures=new List<Exception>();
            foreach(var item in changed.AsEnumerable().Reverse())try {if(File.Exists(item.Target))File.Delete(item.Target);if(item.Original)File.Move(item.Backup,item.Target);}catch(Exception restoreError){failures.Add(restoreError);}
            if(failures.Count>0)throw new AggregateException("Update failed and some files need recovery from "+backup,failures.Prepend(installError));
            throw new IOException("Update could not be installed; previous app files were restored.",installError);
        }
    }
    public bool ScheduleInstall(bool relaunch)
    {
        if(_stage is null)return false;
        var feed=VerifyFeed(File.ReadAllBytes(Path.Combine(_stage,"feed.json")),File.ReadAllBytes(Path.Combine(_stage,"feed.sig")));
        if(!IsNewer(feed,CurrentVersion))return false;
        var helper=Path.Combine(_stage,"helper-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(helper);
        // Copy the current self-contained runtime outside the app folder, so it holds no app-file locks.
        foreach(var file in Directory.GetFiles(_root))File.Copy(file,Path.Combine(helper,Path.GetFileName(file)),false);
        var plan=new InstallPlan(_root,_stage,Environment.ProcessId,Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks,relaunch);
        var planPath=Path.Combine(_stage,"install-plan.json");File.WriteAllText(planPath,JsonSerializer.Serialize(plan,Json));
        var start=new ProcessStartInfo(Path.Combine(helper,"CheatGPT.exe")){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=helper};
        start.ArgumentList.Add("--apply-update");start.ArgumentList.Add(planPath);
        _ = Process.Start(start) ?? throw new IOException("Could not start the update installer.");return true;
    }
    internal static int RunInstaller(string planPath,string? testKey=null,bool interactive=true)
    {
        InstallPlan? plan=null;
        try {
            plan=JsonSerializer.Deserialize<InstallPlan>(File.ReadAllText(planPath),Json) ?? throw new InvalidDataException("Missing install plan.");
            var feed=VerifyFeed(File.ReadAllBytes(Path.Combine(plan.Stage,"feed.json")),File.ReadAllBytes(Path.Combine(plan.Stage,"feed.sig")),testKey);
            if(!IsNewer(feed,CurrentVersion))return 0;
            try { using var parent=Process.GetProcessById(plan.ParentPid); if(parent.StartTime.ToUniversalTime().Ticks==plan.ParentStartTicks && !parent.WaitForExit(120000))throw new IOException("Cheat GPT has not exited. Update was not installed."); }catch(ArgumentException){}
            using var instance=new Mutex(false,"Local\\CheatGPT.Windows"); bool owned;
            try{owned=instance.WaitOne(TimeSpan.FromSeconds(30));}catch(AbandonedMutexException){owned=true;}
            if(!owned)throw new IOException("Another Cheat GPT instance is open. Close it before updating.");
            try {
                NoLinks(plan.Root);
                if(!File.Exists(Path.Combine(plan.Root,"CheatGPT.exe")) || !Directory.Exists(Path.Combine(plan.Root,"Models")))throw new IOException("The original app folder is missing.");
                var payload=Path.Combine(plan.Stage,"install-"+Guid.NewGuid().ToString("N"));
                VerifyAndExtract(feed,Path.Combine(plan.Stage,"package.zip"),payload);
                ApplyTransaction(payload,plan.Root,Path.Combine(plan.Stage,"backup"));
                var marker=Path.Combine(Path.GetDirectoryName(plan.Stage)!,"pending.txt");if(File.Exists(marker))File.Delete(marker);
                File.WriteAllText(Path.Combine(plan.Stage,"installed.txt"),feed.Version);
            }finally{instance.ReleaseMutex();}
            if(plan.Relaunch)Process.Start(new ProcessStartInfo(Path.Combine(plan.Root,"CheatGPT.exe")){UseShellExecute=true,WorkingDirectory=plan.Root});
            return 0;
        }catch(Exception error) {
            if(plan is not null)try{File.WriteAllText(Path.Combine(plan.Stage,"install-error.txt"),error.ToString());var marker=Path.Combine(Path.GetDirectoryName(plan.Stage)!,"pending.txt");if(File.Exists(marker))File.Delete(marker);}catch{}
            if(interactive)MessageBox.Show(error.Message+"\nYour AI preferences are stored separately and were not changed.","Cheat GPT update",MessageBoxButtons.OK,MessageBoxIcon.Warning);return 1;
        }
    }
}
