using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SlideProRelay.Server.ScreenCapture;

/// <summary>
/// Captures a macOS display by shelling out to the built-in <c>screencapture</c>
/// tool (no extra dependencies; it handles JPEG encoding and the TCC permission
/// flow). The first capture triggers the system "Screen Recording" permission
/// prompt — until it's granted, macOS returns a desktop-only/black image.
/// Displays are enumerated via CoreGraphics.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacScreenCaptureService : IScreenCaptureService
{
    private const string CoreGraphics =
        "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    private readonly ILogger<MacScreenCaptureService> _logger;

    public MacScreenCaptureService(ILogger<MacScreenCaptureService> logger) => _logger = logger;

    public bool IsSupported => true;

    public IReadOnlyList<CaptureDisplay> GetDisplays()
    {
        try
        {
            if (CGGetActiveDisplayList(0, null, out var count) != 0 || count == 0)
                return [];

            var ids = new uint[count];
            if (CGGetActiveDisplayList(count, ids, out count) != 0)
                return [];

            var main = CGMainDisplayID();
            var displays = new List<CaptureDisplay>((int)count);
            for (var i = 0; i < count; i++)
            {
                var b = CGDisplayBounds(ids[i]);
                // screencapture -D is 1-based and ordered like the active display
                // list (main first), so index i+1 maps to `-D (i+1)`.
                displays.Add(new CaptureDisplay(
                    i + 1, (int)b.Size.Width, (int)b.Size.Height, (int)b.Origin.X, (int)b.Origin.Y,
                    IsPrimary: ids[i] == main));
            }

            return displays;
        }
        catch (DllNotFoundException ex)
        {
            _logger.LogDebug("Display enumeration unavailable: {Message}", ex.Message);
            return [];
        }
    }

    public async Task<byte[]?> CaptureAsync(int displayIndex, CancellationToken ct = default)
    {
        var display = ResolveDisplayIndex(displayIndex);
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

            var optimized = Path.Combine(Path.GetTempPath(), $"spr-opt-{Guid.NewGuid():N}.jpg");
            try
            {
                // sips: re-encode at 85% quality and cap width at 1920px (only downscales).
                // --resampleHeightWidthMax with a huge height value acts as a width-only cap.
                var sipsPsi = new ProcessStartInfo("sips")
                {
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                sipsPsi.ArgumentList.Add("-s"); sipsPsi.ArgumentList.Add("formatOptions"); sipsPsi.ArgumentList.Add("85");
                sipsPsi.ArgumentList.Add("--resampleHeightWidthMax"); sipsPsi.ArgumentList.Add("99999"); sipsPsi.ArgumentList.Add("1920");
                sipsPsi.ArgumentList.Add(tmp);
                sipsPsi.ArgumentList.Add("--out"); sipsPsi.ArgumentList.Add(optimized);

                using var sipsProc = Process.Start(sipsPsi);
                if (sipsProc is not null)
                {
                    await sipsProc.WaitForExitAsync(ct);
                    if (sipsProc.ExitCode == 0 && File.Exists(optimized))
                    {
                        var optimizedBytes = await File.ReadAllBytesAsync(optimized, ct);
                        if (optimizedBytes.Length > 0) return optimizedBytes;
                    }
                }
            }
            finally
            {
                try { if (File.Exists(optimized)) File.Delete(optimized); } catch { /* best effort */ }
            }

            // sips unavailable or failed — fall back to the raw screencapture output
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

    // <= 0 means "service default": the first non-primary display, else display 1.
    private int ResolveDisplayIndex(int requested)
    {
        if (requested >= 1)
            return requested;

        var displays = GetDisplays();
        var firstNonPrimary = displays.FirstOrDefault(d => !d.IsPrimary);
        return firstNonPrimary?.Index ?? 1;
    }

    // ── CoreGraphics interop ────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    private struct CGPoint { public double X; public double Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct CGSize { public double Width; public double Height; }

    [StructLayout(LayoutKind.Sequential)]
    private struct CGRect { public CGPoint Origin; public CGSize Size; }

    [DllImport(CoreGraphics)]
    private static extern int CGGetActiveDisplayList(uint maxDisplays, uint[]? activeDisplays, out uint displayCount);

    [DllImport(CoreGraphics)]
    private static extern uint CGMainDisplayID();

    [DllImport(CoreGraphics)]
    private static extern CGRect CGDisplayBounds(uint display);
}
