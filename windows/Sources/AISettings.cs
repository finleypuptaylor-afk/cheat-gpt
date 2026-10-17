using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CheatGPT;
public enum ContextWindow { Tokens4096 = 4096, Tokens8192 = 8192, Tokens16384 = 16384 }

public sealed class AISettings
{
    [Category("Memory & CPU"), Description("Restart applies this context capacity. Larger context uses more memory, not a smarter model.")]
    public ContextWindow Context { get; set; } = ContextWindow.Tokens4096;
    [Category("Memory & CPU"), Description("CPU generation threads, capped at available logical processors and 16. Restart applies changes.")]
    public int Threads { get; set; } = Math.Min(4, Environment.ProcessorCount);
    [Category("Generation"), DisplayName("Maximum reply tokens"), Description("64–1024. Applies to the next reply.")]
    public int Tokens { get; set; } = 384;
    [Category("Generation"), Description("0–1.5. Lower favors predictable wording; higher adds variety.")]
    public double Temperature { get; set; } = 0.2;
    [Category("Generation"), DisplayName("Top P"), Description("0.1–1. Narrows the probability mass considered for the next word.")]
    public double TopP { get; set; } = 0.9;
    [Category("Generation"), DisplayName("Top K"), Description("1–100. Narrows the number of next-word candidates.")]
    public int TopK { get; set; } = 40;
    [Category("Generation"), DisplayName("Repetition penalty"), Description("1–1.5. 1 means no penalty; higher discourages repetition.")]
    public double RepeatPenalty { get; set; } = 1.05;
    [JsonIgnore, Category("Your computer"), DisplayName("Installed RAM (GB)")]
    public double InstalledRam => Math.Round(RamGB,1);
    [JsonIgnore, Category("Your computer"), DisplayName("CPU logical processors")]
    public int Cores => Environment.ProcessorCount;
    private static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Cheat GPT","parameters.json");
    public static double RamGB {
        get { var state = new MemoryStatus(); state.Length = (uint)Marshal.SizeOf<MemoryStatus>(); return GlobalMemoryStatusEx(ref state) ? state.TotalPhysical / 1073741824.0 : 0; }
    }
    public static AISettings Recommended {
        get {
            var ram = RamGB;
            return new AISettings { Context = ram >= 16 ? ContextWindow.Tokens16384 : (ram >= 8 ? ContextWindow.Tokens8192 : ContextWindow.Tokens4096), Threads = Math.Min(Environment.ProcessorCount,ram >= 16 ? 6 : (ram >= 8 ? 4 : 2)), Tokens = ram >= 16 ? 768 : (ram >= 8 ? 384 : 256) };
        }
    }
    public AISettings Validated() => new() {
        Context = Enum.IsDefined(Context) ? Context : ContextWindow.Tokens4096,
        Threads = Math.Clamp(Threads,1,Math.Max(1,Math.Min(16,Environment.ProcessorCount))),
        Tokens = Math.Clamp(Tokens,64,1024), TopK = Math.Clamp(TopK,1,100),
        Temperature = double.IsFinite(Temperature) ? Math.Clamp(Temperature,0,1.5) : 0.2,
        TopP = double.IsFinite(TopP) ? Math.Clamp(TopP,0.1,1) : 0.9,
        RepeatPenalty = double.IsFinite(RepeatPenalty) ? Math.Clamp(RepeatPenalty,1,1.5) : 1.05
    };
    public static AISettings Load() { try { return (JsonSerializer.Deserialize<AISettings>(File.ReadAllText(SettingsPath)) ?? new()).Validated(); } catch { return new(); } }
    public void Save() { Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!); File.WriteAllText(SettingsPath,JsonSerializer.Serialize(Validated())); }
    [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus { public uint Length, Load; public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual; }
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
}

public sealed class AISettingsForm : Form
{
    private readonly PropertyGrid _grid;
    public AISettings Value { get; private set; }
    public AISettingsForm(AISettings current)
    {
        Value = current.Validated(); Text = "Cheat GPT — AI settings";
        Width = 500; Height = 650; MinimumSize = new Size(400,500); StartPosition = FormStartPosition.CenterParent;
        var layout = new TableLayoutPanel { Dock=DockStyle.Fill, RowCount=3, ColumnCount=1, Padding=new Padding(14) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,130)); layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,65));
        var suggested = AISettings.Recommended;
        var info = new Label { Dock=DockStyle.Fill, Text=$"{AISettings.RamGB:F1} GB total installed RAM · {Environment.ProcessorCount} CPU logical processors\n\nSuggested: {(int)suggested.Context:N0} context tokens · {suggested.Threads} threads · {suggested.Tokens} reply tokens\n\nSuggestions use installed RAM, not free memory, and are starting points for the bundled 0.5B model. Other apps need memory too." };
        _grid = new PropertyGrid { Dock=DockStyle.Fill, SelectedObject=Value, ToolbarVisible=false, PropertySort=PropertySort.Categorized, HelpVisible=true };
        var buttons = new FlowLayoutPanel { Dock=DockStyle.Fill, AutoScroll=true, Padding=new Padding(0,8,0,0) };
        var recommended = new Button { Text="Use recommended", AutoSize=true, Enabled=AISettings.RamGB>0 };
        recommended.Click += (_,_) => { Value=AISettings.Recommended; _grid.SelectedObject=Value; };
        var reset = new Button { Text="Defaults", AutoSize=true }; reset.Click += (_,_) => { Value=new(); _grid.SelectedObject=Value; };
        var save = new Button { Text="Save & apply", AutoSize=true }; save.Click += (_,_) => {
            _grid.Focus(); Value=Value.Validated();
            try { Value.Save(); DialogResult=DialogResult.OK; Close(); } catch(Exception error) { MessageBox.Show(error.Message,"Could not save settings"); }
        };
        var cancel=new Button { Text="Cancel",AutoSize=true,DialogResult=DialogResult.Cancel }; CancelButton=cancel;
        buttons.Controls.AddRange([recommended,reset,save,cancel]); layout.Controls.Add(info,0,0); layout.Controls.Add(_grid,0,1); layout.Controls.Add(buttons,0,2); Controls.Add(layout);
    }
}
