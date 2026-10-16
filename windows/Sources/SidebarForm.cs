using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace CheatGPT;

public sealed class SidebarForm : Form
{
    private static readonly Color Background = Color.FromArgb(19, 22, 22);
    private static readonly Color Muted = Color.FromArgb(150, 159, 152);
    private static readonly Color Accent = Color.FromArgb(176, 225, 151);
    private readonly EngineClient _engine = new(AppContext.BaseDirectory);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly List<ChatMessage> _history = [];
    private readonly FlowLayoutPanel _messages = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(14, 5, 0, 0) };
    private readonly Label _status = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(20, 0, 0, 0), Text = "Loading local AI…", ForeColor = Muted };
    private readonly Label _error = new() { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(18, 10, 18, 10), ForeColor = Color.FromArgb(239, 193, 136), Visible = false };
    private readonly TextBox _prompt = new() { Multiline = true, AcceptsReturn = true, BorderStyle = BorderStyle.None, Dock = DockStyle.Fill, MaxLength = 8000, BackColor = Color.FromArgb(35, 39, 37), ForeColor = Color.WhiteSmoke, Enabled = false, PlaceholderText = "Ask anything…", ScrollBars = ScrollBars.Vertical };
    private readonly Button _send = new() { Text = "↑", Width = 34, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Accent, ForeColor = Background, Enabled = false, AccessibleName = "Send message" };
    private readonly Button _new = SmallButton("＋", "New conversation");
    private readonly Button _retry = SmallButton("Retry", "Restart local AI");
    private readonly NotifyIcon _tray;
    private readonly bool _smoke;
    private Task? _initialization;
    private CancellationTokenSource? _replyCancellation;
    private bool _busy, _quitting, _hotkey;
    private Panel? _welcome;
    private const int HotkeyID = 0xC6A1;
    private static readonly uint ShowMessage = RegisterWindowMessage("CheatGPT.Windows.ShowSidebar.v1");
    public static void ShowExisting() => PostMessage((IntPtr)0xffff, ShowMessage, IntPtr.Zero, IntPtr.Zero);

    public SidebarForm(bool smoke = false)
    {
        _smoke = smoke;
        Text = "Cheat GPT"; BackColor = Background; ForeColor = Color.WhiteSmoke;
        Font = new Font("Segoe UI", 9); FormBorderStyle = FormBorderStyle.None;
        TopMost = true; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(340, 480); ClientSize = new Size(390, 740);
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? SystemIcons.Application;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Margin = Padding.Empty };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var height in new[] { 80, 36, 28 }) root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));
        var header = new Panel { Dock = DockStyle.Fill, Width = 390, Padding = new Padding(20) };
        var logo = new PictureBox { Image = Icon.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Location = new Point(20, 23), Size = new Size(28, 28) };
        var title = new Label { Text = "Cheat GPT", Font = new Font("Segoe UI", 13, FontStyle.Bold), Location = new Point(60, 17), AutoSize = true };
        var tagline = new Label { Text = "A little space to think.", ForeColor = Muted, Font = new Font("Segoe UI", 8), Location = new Point(61, 43), AutoSize = true };
        var hide = SmallButton("×", "Hide sidebar"); hide.Anchor = AnchorStyles.Top | AnchorStyles.Right; hide.SetBounds(348, 20, 28, 30); hide.Click += (_, _) => Hide();
        _new.Anchor = AnchorStyles.Top | AnchorStyles.Right; _new.SetBounds(312, 20, 28, 30); _new.Click += (_, _) => ClearChat();
        header.Controls.AddRange([logo, title, tagline, _new, hide]);
        header.MouseDown += DragHeader; title.MouseDown += DragHeader; tagline.MouseDown += DragHeader;
        var statusRow = new Panel { Dock = DockStyle.Fill };
        _status.AutoEllipsis = true;
        _retry.Dock = DockStyle.Right; _retry.Width = 65; _retry.Font = new Font("Segoe UI", 8); _retry.Visible = false;
        _retry.Click += async (_, _) => { _initialization = null; await EnsureLoadedAsync(); };
        statusRow.Controls.Add(_status); statusRow.Controls.Add(_retry);
        var model = new Label { Dock = DockStyle.Fill, Text = "Qwen 2.5 · 0.5B · On your PC", ForeColor = Muted, Padding = new Padding(20, 0, 0, 0), Font = new Font("Segoe UI", 8), TextAlign = ContentAlignment.MiddleLeft };
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(16, 12, 16, 10) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        var composer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = _prompt.BackColor, Padding = new Padding(12) };
        composer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); composer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 43));
        _prompt.Font = new Font("Segoe UI", 10.5f); _prompt.Margin = Padding.Empty;
        _send.Font = new Font("Segoe UI", 15, FontStyle.Bold); _send.FlatAppearance.BorderSize = 0; _send.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        composer.Controls.Add(_prompt, 0, 0); composer.Controls.Add(_send, 1, 0);
        var privacy = new Label { Text = "Local inference. No chat history saved.    Ctrl+Alt+Space", ForeColor = Muted, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 7), TextAlign = ContentAlignment.BottomLeft };
        footer.Controls.Add(composer, 0, 0); footer.Controls.Add(privacy, 0, 1);
        root.Controls.Add(header, 0, 0); root.Controls.Add(statusRow, 0, 1); root.Controls.Add(model, 0, 2);
        root.Controls.Add(_messages, 0, 3); root.Controls.Add(_error, 0, 4); root.Controls.Add(footer, 0, 5);
        Controls.Add(root);
        _prompt.TextChanged += (_, _) => UpdateControls();
        _prompt.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter && !e.Shift) { e.SuppressKeyPress = true; if (!_busy) await SendAsync(); } };
        _send.Click += async (_, _) => { if (_busy) _replyCancellation?.Cancel(); else await SendAsync(); };
        var menu = new ContextMenuStrip { BackColor = Background, ForeColor = ForeColor };
        menu.Items.Add("Show / Hide Cheat GPT", null, (_, _) => Toggle());
        menu.Items.Add("New conversation", null, (_, _) => { if (!_busy) { ClearChat(); ShowSidebar(); } });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Windows downloads", null, (_, _) => OpenUrl("https://github.com/finleypuptaylor-afk/cheat-gpt/releases/tag/v1.2"));
        menu.Items.Add("Install engine runtime", null, (_, _) => OpenUrl("https://aka.ms/vc14/vc_redist.x64.exe"));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit Cheat GPT", null, (_, _) => Quit());
        _tray = new NotifyIcon { Text = "Cheat GPT", Icon = Icon, ContextMenuStrip = menu, Visible = !smoke };
        _tray.DoubleClick += (_, _) => Toggle();
        _messages.SizeChanged += (_, _) => Reflow();
        Resize += (_, _) => { _error.MaximumSize = new Size(Math.Max(250, ClientSize.Width), 0); Reflow(); };
        Shown += async (_, _) => { PositionSidebar(); await EnsureLoadedAsync(); };
        ClearChat();
    }

    private static Button SmallButton(string text, string name)
    {
        var button = new Button { Text = text, AccessibleName = name, FlatStyle = FlatStyle.Flat, BackColor = Background, ForeColor = Muted, Font = new Font("Segoe UI", 15), TabStop = true };
        button.FlatAppearance.BorderSize = 0; return button;
    }
    private void PositionSidebar()
    {
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        var scale = DeviceDpi / 96f; var gap = (int)(10 * scale);
        var width = Math.Min((int)(390 * scale), area.Width - gap * 2);
        SetBounds(area.Right - width - gap, area.Top + gap, width, Math.Max(480, area.Height - gap * 2));
    }
    private void ShowSidebar() { PositionSidebar(); Show(); Activate(); _prompt.Focus(); }
    private void Toggle() { if (Visible) Hide(); else ShowSidebar(); }
    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception error) { MessageBox.Show(error.Message, "Cheat GPT"); }
    }
    private Task EnsureLoadedAsync() => _initialization ??= InitializeAsync();
    private async Task InitializeAsync()
    {
        _retry.Visible = false; ShowError(null); _status.Text = "Loading the model on your PC…"; _status.ForeColor = Muted;
        try { await _engine.StartAsync(_lifetime.Token); _status.Text = "●  On-device · Ready"; _status.ForeColor = Accent; _prompt.Focus(); }
        catch (OperationCanceledException) { }
        catch (Exception error) { _status.Text = "Local AI unavailable"; ShowError(error.Message); _retry.Visible = true; }
        UpdateControls();
    }
    private void ShowError(string? message) { _error.Text = message ?? ""; _error.Visible = !string.IsNullOrEmpty(message); }
    private void UpdateControls()
    {
        _prompt.Enabled = _engine.Ready && !_quitting;
        _send.Text = _busy ? "■" : "↑";
        _send.AccessibleName = _busy ? "Stop generating" : "Send message";
        _send.Enabled = !_quitting && (_busy || (_engine.Ready && !string.IsNullOrWhiteSpace(_prompt.Text)));
        _new.Enabled = !_busy; _retry.Enabled = !_busy;
    }
    private MessageCard AddMessage(string role, string text)
    {
        if (_welcome is not null) _welcome.Visible = false;
        var card = new MessageCard(role, text, role == "user" ? Color.FromArgb(30, 34, 32) : Background, Accent, Muted);
        card.Width = Math.Max(250, _messages.ClientSize.Width - 35); _messages.Controls.Add(card);
        _messages.ScrollControlIntoView(card); return card;
    }
    private async Task SendAsync()
    {
        var prompt = _prompt.Text.Trim(); if (_busy || !_engine.Ready || prompt.Length == 0) return;
        _busy = true; ShowError(null); _prompt.Clear();
        _history.Add(new ChatMessage("user", prompt));
        while (_history.Count > 16 || (_history.Count > 1 && _history.Sum(m => m.Content.Length) > 24_000)) _history.RemoveAt(0);
        if (_history.Count > 1 && _history[0].Role == "assistant") _history.RemoveAt(0);
        AddMessage("user", prompt);
        var reply = AddMessage("assistant", "Thinking locally…"); var content = new StringBuilder();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token); _replyCancellation = cancellation;
        _status.Text = "Thinking on your PC…"; UpdateControls();
        try
        {
            await foreach (var chunk in _engine.ChatAsync(_history.ToArray(), cancellation.Token))
            { content.Append(chunk); reply.SetText(content.ToString()); _messages.ScrollControlIntoView(reply); }
            if (content.Length == 0) ShowError("The model returned an empty reply. Try again.");
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { if (!_quitting) ShowError(error.Message); }
        finally
        {
            if (content.Length > 0) _history.Add(new ChatMessage("assistant", content.ToString()));
            else { _messages.Controls.Remove(reply); reply.Dispose(); }
            _busy = false; _replyCancellation = null;
            if (!_quitting) { _status.Text = _engine.Ready ? "●  On-device · Ready" : "Local AI unavailable"; UpdateControls(); _prompt.Focus(); }
        }
    }
    private void ClearChat()
    {
        if (_busy) return;
        _history.Clear(); ShowError(null);
        foreach (Control child in _messages.Controls.Cast<Control>().ToArray()) child.Dispose();
        _messages.Controls.Clear();
        _welcome = new Panel { Width = 340, Height = 335, BackColor = Background, Margin = new Padding(5, 12, 0, 0) };
        _welcome.Controls.Add(new Label { Text = "✧", Font = new Font("Segoe UI Symbol", 34), ForeColor = Accent, AutoSize = true, Location = new Point(2, 20) });
        _welcome.Controls.Add(new Label { Text = "Your thoughts.\nYour machine.", Font = new Font("Segoe UI", 25, FontStyle.Regular), Location = new Point(2, 103), Size = new Size(330, 100) });
        _welcome.Controls.Add(new Label { Text = "Write, ask, and work things out with an AI running on your PC.", ForeColor = Muted, Font = new Font("Segoe UI", 10), Location = new Point(5, 224), Size = new Size(300, 75) });
        _messages.Controls.Add(_welcome); Reflow();
    }
    private void Reflow()
    {
        foreach (Control card in _messages.Controls) { card.Width = Math.Max(250, _messages.ClientSize.Width - 35); if (card is MessageCard message) message.Reflow(); }
    }
    public void Quit()
    {
        if (_quitting) return; _quitting = true;
        _lifetime.Cancel(); _replyCancellation?.Cancel(); _engine.Dispose(); _tray.Visible = false; Close();
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_quitting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); return; }
        _quitting = true; _lifetime.Cancel(); _replyCancellation?.Cancel(); _engine.Dispose();
        base.OnFormClosing(e);
    }
    protected override void OnFormClosed(FormClosedEventArgs e) { _tray.Dispose(); _lifetime.Dispose(); base.OnFormClosed(e); }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!_smoke) _hotkey = RegisterHotKey(Handle, HotkeyID, 0x4003, 0x20);
        int dark = 1, rounded = 2;
        _ = DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
        _ = DwmSetWindowAttribute(Handle, 33, ref rounded, sizeof(int));
    }
    protected override void OnHandleDestroyed(EventArgs e) { if (_hotkey) UnregisterHotKey(Handle, HotkeyID); base.OnHandleDestroyed(e); }
    protected override void WndProc(ref Message m) { if (m.Msg == 0x0312 && m.WParam.ToInt32() == HotkeyID) Toggle(); if (ShowMessage != 0 && (uint)m.Msg == ShowMessage) ShowSidebar(); base.WndProc(ref m); }
    private void DragHeader(object? sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); } }

    public async Task<object> SmokeTestAsync(string directory)
    {
        Directory.CreateDirectory(directory); await EnsureLoadedAsync();
        if (!_engine.Ready) throw new InvalidOperationException(_error.Text);
        _prompt.Text = "Say hello in one short sentence."; await SendAsync();
        if (_history.Count != 2 || _history[^1].Role != "assistant" || string.IsNullOrWhiteSpace(_history[^1].Content)) throw new InvalidOperationException("No streamed assistant reply: " + _error.Text);
        var reply = _history[^1].Content; var enginePID = _engine.EngineProcessId;
        using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, ClientRectangle); bitmap.Save(Path.Combine(directory, "windows-sidebar.png"), System.Drawing.Imaging.ImageFormat.Png); }
        var cancelled = false;
        using (var stop = new CancellationTokenSource())
        {
            try { await foreach (var _ in _engine.ChatAsync([new ChatMessage("user", "Write a long story about a forest.")], stop.Token)) stop.Cancel(); }
            catch (OperationCanceledException) { cancelled = true; }
        }
        if (!cancelled) throw new InvalidOperationException("Cancellation did not interrupt generation.");
        ClearChat(); if (_history.Count != 0) throw new InvalidOperationException("New conversation did not clear history.");
        _engine.Dispose();
        if (enginePID is { } pid)
        {
            try { using var child = Process.GetProcessById(pid); if (!child.HasExited) throw new InvalidOperationException("Model process was not stopped."); }
            catch (ArgumentException) { }
        }
        return new { os = Environment.OSVersion.ToString(), architecture = RuntimeInformation.ProcessArchitecture.ToString(), streamedReply = reply, cancellation = cancelled, newConversation = true, engineStopped = true };
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr window, int id);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}

internal sealed class MessageCard : Panel
{
    private readonly RichTextBox _text;
    private readonly Label _role;
    private readonly Button _copy;
    public MessageCard(string role, string text, Color background, Color accent, Color muted)
    {
        BackColor = background; Margin = new Padding(0, 0, 0, 12);
        _role = new Label { Text = role == "user" ? "YOU" : "LOCAL AI", ForeColor = role == "user" ? muted : accent, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), AutoSize = true };
        _copy = new Button { Text = "Copy", FlatStyle = FlatStyle.Flat, BackColor = background, ForeColor = muted, Font = new Font("Segoe UI", 7.5f), Width = 42, Height = 23, AccessibleName = "Copy message" };
        _copy.FlatAppearance.BorderSize = 0;
        _text = new RichTextBox { ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = background, ForeColor = Color.WhiteSmoke, Font = new Font("Segoe UI", 10.5f), DetectUrls = false, ScrollBars = RichTextBoxScrollBars.None, Text = text, TabStop = false };
        _copy.Click += (_, _) => { try { if (_text.TextLength > 0) Clipboard.SetText(_text.Text); } catch (ExternalException) { MessageBox.Show("The clipboard is busy. Select the text and copy it again.", "Cheat GPT"); } };
        Controls.AddRange([_role, _copy, _text]); SizeChanged += (_, _) => Reflow();
    }
    public void SetText(string text) { _text.Text = text; Reflow(); }
    public void Reflow()
    {
        if (_text is null || Width < 50) return;
        var width = Math.Max(100, Width - 24);
        var size = TextRenderer.MeasureText(_text.Text.Length > 0 ? _text.Text : " ", _text.Font, new Size(width, 100_000), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix);
        _role.Location = new Point(12, 12); _copy.Location = new Point(Width - 53, 6);
        _text.SetBounds(12, 37, width, size.Height + 12);
        var height = _text.Bottom + 10; if (Height != height) Height = height;
    }
}
