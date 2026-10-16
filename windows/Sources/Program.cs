using System.Text.Json;

namespace CheatGPT;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var smoke = args.Length >= 2 && args[0] == "--smoke-test";
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
        Application.Run(sidebar);
    }
}
