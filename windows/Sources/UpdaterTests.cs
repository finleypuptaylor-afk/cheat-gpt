using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CheatGPT;
internal static class UpdaterTests
{
    private static void Assert(bool value,string message) { if(!value)throw new InvalidOperationException(message); }
    private static void Reject(Action action,string message) { try{action();}catch(InvalidDataException){return;}throw new InvalidOperationException(message); }
    private static UpdateFeed FeedFor(string zip,string version="99.0.0") {using var file=File.OpenRead(zip);var hash=Convert.ToHexString(SHA256.HashData(file));return new(version,AppUpdater.ReleaseBase+"test-update.zip",hash,file.Length);}
    private static void Zip(string path,Dictionary<string,string> entries)
    {
        using var file=File.Create(path);using var zip=new ZipArchive(file,ZipArchiveMode.Create);
        foreach(var item in entries){var entry=zip.CreateEntry(item.Key);using var output=new StreamWriter(entry.Open());output.Write(item.Value);}
    }
    private static Dictionary<string,string> Files() => new() { ["CheatGPT-Windows/CheatGPT.exe"]="new exe",["CheatGPT-Windows/CheatGPT.dll"]="new dll",["CheatGPT-Windows/CheatGPT.runtimeconfig.json"]="{}" };
    internal static object Run(string directory)
    {
        Directory.CreateDirectory(directory);var root=Path.Combine(directory,"updater-cases-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        using var rsa=RSA.Create(3072);var key=rsa.ExportSubjectPublicKeyInfoPem();
        var zip=Path.Combine(root,"good.zip");Zip(zip,Files());var feed=FeedFor(zip);
        var bytes=JsonSerializer.SerializeToUtf8Bytes(feed);var signature=rsa.SignData(bytes,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        Assert(AppUpdater.VerifyFeed(bytes,signature,key).Sha256==feed.Sha256,"Valid signed metadata failed");
        var changed=bytes.ToArray();changed[^3]^=1;Reject(()=>AppUpdater.VerifyFeed(changed,signature,key),"Changed metadata accepted");
        var badSig=signature.ToArray();badSig[0]^=1;Reject(()=>AppUpdater.VerifyFeed(bytes,badSig,key),"Invalid signature accepted");
        using(var other=RSA.Create(2048))Reject(()=>AppUpdater.VerifyFeed(bytes,signature,other.ExportSubjectPublicKeyInfoPem()),"Untrusted signer accepted");
        Assert(!AppUpdater.IsNewer(feed,new Version(99,0,0)) && !AppUpdater.IsNewer(feed,new Version(100,0,0)),"Rollback/reinstall accepted");
        Assert(AppUpdater.IsNewer(feed,new Version(1,2,0)),"Newer release not detected");
        var badUrl=feed with {Url="https://example.com/code.zip"};var badUrlBytes=JsonSerializer.SerializeToUtf8Bytes(badUrl);
        Reject(()=>AppUpdater.VerifyFeed(badUrlBytes,rsa.SignData(badUrlBytes,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1),key),"Unsupported host accepted");
        var extracted=Path.Combine(root,"extracted");AppUpdater.VerifyAndExtract(feed,zip,extracted);
        Assert(File.ReadAllText(Path.Combine(extracted,"CheatGPT.dll"))=="new dll","Valid extraction failed");
        Reject(()=>AppUpdater.VerifyAndExtract(feed with {Sha256=new string('0',64)},zip,Path.Combine(root,"bad-hash")),"Invalid package hash accepted");
        Reject(()=>AppUpdater.VerifyAndExtract(feed with {Size=feed.Size+1},zip,Path.Combine(root,"bad-size")),"Invalid package size accepted");
        foreach(var name in new[]{"CheatGPT-Windows/../escape.txt","CheatGPT-Windows/C:/escape.txt","CheatGPT-Windows/Folder/../../escape.txt","CheatGPT-Windows/CHEATGPT.DLL"}) {
            var entries=Files();entries.Add(name,"unsafe");var unsafeZip=Path.Combine(root,Guid.NewGuid()+".zip");Zip(unsafeZip,entries);
            Reject(()=>AppUpdater.VerifyAndExtract(FeedFor(unsafeZip),unsafeZip,Path.Combine(root,Guid.NewGuid().ToString())),"Unsafe or duplicate path accepted: "+name);
        }
        var noExe=Path.Combine(root,"missing.zip");Zip(noExe,new(){["CheatGPT-Windows/README.md"]="incomplete"});Reject(()=>AppUpdater.VerifyAndExtract(FeedFor(noExe),noExe,Path.Combine(root,"missing")),"Incomplete update accepted");
        var install=Path.Combine(root,"install");Directory.CreateDirectory(install);File.WriteAllText(Path.Combine(install,"CheatGPT.exe"),"old exe");File.WriteAllText(Path.Combine(install,"notes.txt"),"user file");Directory.CreateDirectory(Path.Combine(install,"Models"));File.WriteAllText(Path.Combine(install,"Models","keep.gguf"),"model");
        AppUpdater.ApplyTransaction(extracted,install,Path.Combine(root,"backup"));
        Assert(File.ReadAllText(Path.Combine(install,"CheatGPT.exe"))=="new exe","Install failed");Assert(File.ReadAllText(Path.Combine(root,"backup","CheatGPT.exe"))=="old exe","Backup missing");
        Assert(File.ReadAllText(Path.Combine(install,"notes.txt"))=="user file" && File.ReadAllText(Path.Combine(install,"Models","keep.gguf"))=="model","Update changed unrelated/model files");
        var rollback=Path.Combine(root,"rollback");var payload=Path.Combine(root,"rollback-payload");Directory.CreateDirectory(rollback);Directory.CreateDirectory(payload);
        foreach(var n in new[]{"A.txt","Z.txt"}){File.WriteAllText(Path.Combine(rollback,n),"old "+n);File.WriteAllText(Path.Combine(payload,n),"new "+n);}
        using(var locked=new FileStream(Path.Combine(rollback,"Z.txt"),FileMode.Open,FileAccess.ReadWrite,FileShare.None)) {
            var failed=false;try{AppUpdater.ApplyTransaction(payload,rollback,Path.Combine(root,"rollback-backup"));}catch(IOException){failed=true;}
            Assert(failed,"Locked target did not fail installation");Assert(File.ReadAllText(Path.Combine(rollback,"A.txt"))=="old A.txt","Partial update was not rolled back");
        }
        Assert(File.ReadAllText(Path.Combine(rollback,"Z.txt"))=="old Z.txt","Locked file changed");
        // Exercise the real installer entry function: wait for an existing process, take the app mutex,
        // re-verify and extract the package, install, preserve models and remove pending state.
        var stage=Path.Combine(root,"stage");Directory.CreateDirectory(stage);File.Copy(zip,Path.Combine(stage,"package.zip"));File.WriteAllBytes(Path.Combine(stage,"feed.json"),bytes);File.WriteAllBytes(Path.Combine(stage,"feed.sig"),signature);
        using(var parent=Process.Start(new ProcessStartInfo("cmd.exe","/c ping -n 2 127.0.0.1 > nul"){UseShellExecute=false,CreateNoWindow=true})!) {
            var plan=new InstallPlan(install,stage,parent.Id,parent.StartTime.ToUniversalTime().Ticks,false);var planPath=Path.Combine(stage,"plan.json");File.WriteAllText(planPath,JsonSerializer.Serialize(plan));
            Assert(AppUpdater.RunInstaller(planPath,key,false)==0,"Installer entry failed");
            Assert(parent.HasExited,"Installer did not wait for parent exit");
        }
        Assert(File.Exists(Path.Combine(stage,"installed.txt")),"Install completion not recorded");
        return new {signature=true,metadataTamperingRejected=true,wrongSignerRejected=true,invalidHostRejected=true,downgradeBlocked=true,packageHash=true,zipTraversalRejected=true,caseDuplicatesRejected=true,incompletePackageRejected=true,backup=true,rollbackOnLockedFile=true,unrelatedFilesPreserved=true,parentExitWait=true,installerEntry=true};
    }
}
