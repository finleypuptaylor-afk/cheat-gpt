using System.Text.Json;

namespace CheatGPT;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if(args.Length==2 && args[0]=="--apply-update") { Environment.ExitCode=AppUpdater.RunInstaller(args[1]); return; }
        ApplicationConfiguration.Initialize();
        if(args.Length==2 && args[0]=="--updater-tests") {
            try { Directory.CreateDirectory(args[1]); var result=UpdaterTests.Run(args[1]); File.WriteAllText(Path.Combine(args[1],"updater-result.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true})); }
            catch(Exception error) { Directory.CreateDirectory(args[1]); File.WriteAllText(Path.Combine(args[1],"updater-error.txt"),error.ToString());Environment.ExitCode=1; }
            return;
        }
        var smoke = args.Length >= 2 && args[0] == "--smoke-test";
        using var instance = smoke ? null : new Mutex(false, "Local\\CheatGPT.Windows");
        var ownsInstance = false;
        if (instance is not null)
        {
            try { ownsInstance = instance.WaitOne(0); }
            catch (AbandonedMutexException) { ownsInstance = true; }
            if (!ownsInstance) { SidebarForm.ShowExisting(); return; }
        }
        using var sidebar = new SidebarForm(smoke);
        if (smoke)
        {
            sidebar.Shown += async (_, _) =>
            {
                try
                {
                    var result = await sidebar.SmokeTestAsync(args[1]);
                    File.WriteAllText(Path.Combine(args[1], "smoke-result.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
                    Environment.ExitCode = 0;
                }
                catch (Exception error)
                {
                    Directory.CreateDirectory(args[1]);
                    File.WriteAllText(Path.Combine(args[1], "smoke-error.txt"), error.ToString());
                    Environment.ExitCode = 1;
                }
                finally { sidebar.Quit(); }
            };
        }
        try { Application.Run(sidebar); }
        finally { if (ownsInstance) instance?.ReleaseMutex(); sidebar.SchedulePendingUpdate(); }
    }
}
