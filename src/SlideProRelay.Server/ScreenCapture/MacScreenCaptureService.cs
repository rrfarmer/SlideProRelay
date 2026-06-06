using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SlideProRelay.Server.ScreenCapture;

/// <summary>
/// Captures a macOS display by shelling out to the built-in <c>screencapture</c>
/// tool (no extra dependencies; it handles JPEG encoding and the TCC permission
/// flow). The first capture triggers the system "Screen Recording" permission
/// prompt — until it's granted, macOS returns a desktop-only/black image.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacScreenCaptureService : IScreenCaptureService
{
    private readonly IOptionsMonitor<ScreenCaptureOptions> _opts;
    private readonly ILogger<MacScreenCaptureService> _logger;

    public MacScreenCaptureService(
        IOptionsMonitor<ScreenCaptureOptions> opts,
        ILogger<MacScreenCaptureService> logger)
    {
        _opts = opts;
        _logger = logger;
    }

    public bool IsSupported => true;

    public async Task<byte[]?> CaptureAsync(CancellationToken ct = default)
    {
        var display = ResolveDisplayIndex(_opts.CurrentValue.DisplayIndex);
        var tmp = Path.Combine(Path.GetTempPath(), $"spr-capture-{Guid.NewGuid():N}.jpg");

        try
        {
            // -x: silent (no shutter sound). -t jpg: JPEG. -D <n>: whole display
            // n (1-based). Writes to a file, which we read back and then delete.
            var psi = new ProcessStartInfo("screencapture")
            {
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-x");
            psi.ArgumentList.Add("-t");
            psi.ArgumentList.Add("jpg");
            psi.ArgumentList.Add("-D");
            psi.ArgumentList.Add(display.ToString());
            psi.ArgumentList.Add(tmp);

            using var proc = Process.Start(psi);
            if (proc is null)
                return null;

            await proc.WaitForExitAsync(ct);

            if (proc.ExitCode != 0 || !File.Exists(tmp))
            {
                _logger.LogDebug(
                    "screencapture exited {Code} for display {Display}", proc.ExitCode, display);
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(tmp, ct);
            return bytes.Length > 0 ? bytes : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug("Screen capture failed: {Message}", ex.Message);
            return null;
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* best effort */ }
        }
    }

    // 0 = auto: with more than one display attached, grab the first non-primary
    // (index 2 — usually the ProPresenter output); otherwise the main display.
    // Any explicit value is used verbatim.
    private int ResolveDisplayIndex(int configured)
    {
        if (configured > 0)
            return configured;

        try
        {
            return ActiveDisplayCount() >= 2 ? 2 : 1;
        }
        catch (DllNotFoundException)
        {
            return 1;
        }
    }

    private static uint ActiveDisplayCount()
    {
        // Passing a null list with maxDisplays 0 returns just the count.
        _ = CGGetActiveDisplayList(0, null, out var count);
        return count;
    }

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern int CGGetActiveDisplayList(
        uint maxDisplays, uint[]? activeDisplays, out uint displayCount);
}
