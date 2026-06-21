using System.Text.Json;
using Ard.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Ard.AwardApp;

/// <summary>
/// Hosts the challenge-3 MCP App in a WebView2, drives the MCP Apps host handshake, and can capture
/// the rendered award as a PNG (interactively via a button, or headlessly via <c>--screenshot</c>).
/// </summary>
public sealed class AwardForm : Form
{
    private readonly AwardOptions _opts;
    private readonly WebView2 _web = new();
    private readonly TaskCompletionSource<bool> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private TextBox? _nameBox;
    private Label? _status;
    private Button? _saveButton;

    private string? _tempContentDir;
    private string? _tempUserDataDir;
    private AwardArtifact? _award;
    private RectangleF? _cardRect;

    public int ExitCode { get; private set; }

    public AwardForm(AwardOptions opts)
    {
        _opts = opts;
        Text = "ARD Treasure Hunt — MCP App Award";
        BackColor = Color.FromArgb(0x0F, 0x10, 0x20);
        _web.Dock = DockStyle.Fill;
        _web.DefaultBackgroundColor = Color.FromArgb(0x0F, 0x10, 0x20);

        if (opts.Headless)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-6000, -6000); // render offscreen
            ClientSize = new Size(opts.Width, opts.Height);
            Controls.Add(_web);
        }
        else
        {
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(opts.Width, opts.Height + 52);
            Controls.Add(_web);
            Controls.Add(BuildToolbar());
        }

        Load += async (_, _) => await RunAsync();
    }

    private Panel BuildToolbar()
    {
        var bar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Color.FromArgb(0x1A, 0x1B, 0x2E), Padding = new Padding(10, 10, 10, 10) };

        var nameLabel = new Label { Text = "Name:", AutoSize = true, ForeColor = Color.Gainsboro, Location = new Point(12, 18) };
        _nameBox = new TextBox { Text = _opts.Name, Location = new Point(58, 14), Width = 240 };

        var reveal = new Button { Text = "Reveal / Reload", Location = new Point(312, 12), Width = 120, FlatStyle = FlatStyle.Flat, ForeColor = Color.Black, BackColor = Color.FromArgb(0xFF, 0xD2, 0x4A) };
        reveal.Click += async (_, _) => await ReloadAsync();

        _saveButton = new Button { Text = "Save PNG…", Location = new Point(440, 12), Width = 110, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(0x3A, 0x3C, 0x55), Enabled = false };
        _saveButton.Click += async (_, _) => await SaveInteractiveAsync();

        _status = new Label { Text = "Starting…", AutoSize = true, ForeColor = Color.Gainsboro, Location = new Point(566, 18) };

        bar.Controls.AddRange(new Control[] { nameLabel, _nameBox, reveal, _saveButton, _status });
        return bar;
    }

    private async Task RunAsync()
    {
        try
        {
            Log("Acquiring the award…");
            _award = await AwardSource.AcquireAsync(_opts, Log);
            Log($"Award code: {_award.Code}");

            await InitWebViewAsync();
            await NavigateAwardAsync(_award);

            await WaitForReadyAsync();

            if (_opts.Headless)
            {
                await CaptureToFileAsync(_opts.ScreenshotPath!);
                Close();
            }
            else
            {
                if (_saveButton is not null) _saveButton.Enabled = true;
                SetStatus($"Code: {_award.Code}");
            }
        }
        catch (Exception ex)
        {
            ExitCode = 2;
            Log($"✖ {ex.Message}");
            if (_opts.Headless) Close();
            else MessageBox.Show(this, ex.ToString(), "ARD Award App — error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task InitWebViewAsync()
    {
        _tempUserDataDir = Path.Combine(Path.GetTempPath(), "ard-award-wv2-" + Guid.NewGuid().ToString("N"));
        var env = await CoreWebView2Environment.CreateAsync(null, _tempUserDataDir, null);
        await _web.EnsureCoreWebView2Async(env);
        _web.CoreWebView2.WebMessageReceived += OnWebMessage;
    }

    private async Task NavigateAwardAsync(AwardArtifact award)
    {
        _tempContentDir ??= Path.Combine(Path.GetTempPath(), "ard-award-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempContentDir);

        var name = _nameBox?.Text is { Length: > 0 } n ? n : _opts.Name;
        await File.WriteAllTextAsync(Path.Combine(_tempContentDir, "award.html"),
            AwardHostHtml.BuildAwardDocument(award.Html, name), new System.Text.UTF8Encoding(false));
        await File.WriteAllTextAsync(Path.Combine(_tempContentDir, "host.html"),
            AwardHostHtml.BuildHostPage(award), new System.Text.UTF8Encoding(false));

        const string vhost = "appassets.ard";
        _web.CoreWebView2.SetVirtualHostNameToFolderMapping(vhost, _tempContentDir, CoreWebView2HostResourceAccessKind.Allow);
        _web.CoreWebView2.Navigate($"https://{vhost}/host.html");
    }

    private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.TryGetWebMessageAsString();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
            if (type == "ard-ready") _ready.TrySetResult(true);
            else if (type == "card-rect")
            {
                _cardRect = new RectangleF(
                    (float)root.GetProperty("left").GetDouble(),
                    (float)root.GetProperty("top").GetDouble(),
                    (float)root.GetProperty("width").GetDouble(),
                    (float)root.GetProperty("height").GetDouble());
            }
        }
        catch { /* ignore non-JSON messages */ }
    }

    private async Task WaitForReadyAsync()
    {
        await Task.WhenAny(_ready.Task, Task.Delay(8000));
        await Task.Delay(_opts.Headless ? 500 : 150); // let paint + emoji settle
    }

    private async Task ReloadAsync()
    {
        if (_award is null) return;
        SetStatus("Reloading…");
        await NavigateAwardAsync(_award);
        SetStatus($"Code: {_award.Code}");
    }

    private async Task SaveInteractiveAsync()
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "PNG image (*.png)|*.png",
            FileName = $"ard-award-{SafeFileName(_nameBox?.Text ?? _opts.Name)}.png",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        await CaptureToFileAsync(dlg.FileName);
        SetStatus($"Saved {Path.GetFileName(dlg.FileName)}");
    }

    /// <summary>Capture the rendered award via the DevTools Protocol (works offscreen; crisp via deviceScaleFactor).</summary>
    private async Task CaptureToFileAsync(string path)
    {
        var core = _web.CoreWebView2;

        // Wait past navigation for real paint: two animation frames + fonts.
        await core.ExecuteScriptAsync("new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))");
        try { await core.ExecuteScriptAsync("(document.fonts && document.fonts.ready) || Promise.resolve()"); } catch { }
        if (_cardRect is null) await Task.Delay(800); // give the component a moment to report its bounds

        int w = _web.Width, h = _web.Height;

        // Pin the device pixel ratio for a deterministic, high-resolution PNG.
        await core.CallDevToolsProtocolMethodAsync("Emulation.setDeviceMetricsOverride",
            JsonSerializer.Serialize(new { width = w, height = h, deviceScaleFactor = _opts.Scale, mobile = false }));

        // Tight-crop to the award card (+ margin) if the component reported its bounds; else full frame.
        const float margin = 40f;
        double clipX = 0, clipY = 0, clipW = w, clipH = h;
        if (_cardRect is { } r && r is { Width: > 0, Height: > 0 })
        {
            clipX = Math.Max(0, r.Left - margin);
            clipY = Math.Max(0, r.Top - margin);
            clipW = Math.Min(w - clipX, r.Width + 2 * margin);
            clipH = Math.Min(h - clipY, r.Height + 2 * margin);
        }

        var paramsJson = JsonSerializer.Serialize(new
        {
            format = "png",
            captureBeyondViewport = true,
            clip = new { x = clipX, y = clipY, width = clipW, height = clipH, scale = 1 },
        });
        var resultJson = await core.CallDevToolsProtocolMethodAsync("Page.captureScreenshot", paramsJson);
        await core.CallDevToolsProtocolMethodAsync("Emulation.clearDeviceMetricsOverride", "{}");

        var base64 = JsonDocument.Parse(resultJson).RootElement.GetProperty("data").GetString()!;
        var bytes = Convert.FromBase64String(base64);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await File.WriteAllBytesAsync(path, bytes);

        Log($"✔ Saved PNG ({bytes.Length:N0} bytes, {(int)clipW * _opts.Scale}×{(int)clipH * _opts.Scale}): {path}");
    }

    // --- small helpers ---

    private void Log(string msg)
    {
        Console.WriteLine(msg);
        SetStatus(msg.Length > 60 ? msg[..60] + "…" : msg);
    }

    private void SetStatus(string msg)
    {
        if (_status is null) return;
        if (_status.IsHandleCreated && _status.InvokeRequired) _status.BeginInvoke(() => _status.Text = msg);
        else _status.Text = msg;
    }

    private static string SafeFileName(string s) =>
        string.Concat(s.Split(Path.GetInvalidFileNameChars())).Replace(' ', '-');

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // The msedgewebview2 browser process holds a lock on the user-data dir and exits
            // asynchronously, so grab its PID, start the shutdown, then wait for it to actually
            // exit before deleting — otherwise the delete races the lock and the temp dir leaks.
            int? browserPid = null;
            try { browserPid = (int?)_web.CoreWebView2?.BrowserProcessId; } catch { }
            _web.Dispose();
            if (browserPid is int pid)
                try { using var p = System.Diagnostics.Process.GetProcessById(pid); p.WaitForExit(3000); } catch { }
            CleanupTemp();
        }
        base.Dispose(disposing);
    }

    private void CleanupTemp()
    {
        foreach (var dir in new[] { _tempContentDir, _tempUserDataDir })
            try { if (dir is not null && Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
    }
}
