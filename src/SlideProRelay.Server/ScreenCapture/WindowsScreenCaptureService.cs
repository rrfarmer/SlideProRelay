using Microsoft.Extensions.Options;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace SlideProRelay.Server.ScreenCapture;

/// <summary>
/// Captures a Windows display via the DXGI Desktop Duplication API — it reads
/// the GPU's actual output frame buffer, so unlike GDI <c>CopyFromScreen</c> it
/// works for hardware-accelerated content like ProPresenter's DirectX output.
/// The captured BGRA frame is encoded to JPEG with GDI+.
/// </summary>
/// <remarks>
/// A device + duplication is created and torn down per capture. That's heavier
/// than holding them open, but capture only fires on slide changes (seconds
/// apart) and recreating each time sidesteps the DXGI_ERROR_ACCESS_LOST /
/// topology-change bookkeeping a long-lived duplication would require.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsScreenCaptureService : IScreenCaptureService
{
    private const int AcquireTimeoutMs = 200;       // per-AcquireNextFrame wait
    private const int AcquireBudgetMs = 2000;       // overall budget to land a real frame
    private const long JpegQuality = 85L;

    private readonly IOptionsMonitor<ScreenCaptureOptions> _opts;
    private readonly ILogger<WindowsScreenCaptureService> _logger;

    public WindowsScreenCaptureService(
        IOptionsMonitor<ScreenCaptureOptions> opts,
        ILogger<WindowsScreenCaptureService> logger)
    {
        _opts = opts;
        _logger = logger;
    }

    public bool IsSupported => true;

    public Task<byte[]?> CaptureAsync(CancellationToken ct = default) =>
        // Desktop Duplication is entirely synchronous COM; keep it off the caller.
        Task.Run(() => Capture(), ct);

    private byte[]? Capture()
    {
        try
        {
            using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();

            var (adapter, output) = SelectOutput(factory, _opts.CurrentValue.DisplayIndex);
            if (adapter is null || output is null)
            {
                _logger.LogDebug("No capturable display found");
                return null;
            }

            using (adapter)
            using (output)
            {
                return CaptureOutput(adapter, output);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Screen capture failed: {Message}", ex.Message);
            return null;
        }
    }

    private byte[]? CaptureOutput(IDXGIAdapter1 adapter, IDXGIOutput output)
    {
        // Device must live on the same adapter that owns the output.
        var result = D3D11.D3D11CreateDevice(
            adapter,
            DriverType.Unknown,
            DeviceCreationFlags.BgraSupport,
            null,
            out var device);
        if (result.Failure || device is null)
        {
            _logger.LogDebug("D3D11CreateDevice failed: {Result}", result);
            return null;
        }

        using (device)
        using (var context = device.ImmediateContext)
        using (var output1 = output.QueryInterface<IDXGIOutput1>())
        using (var duplication = output1.DuplicateOutput(device))
        {
            if (!TryAcquireFrame(duplication, out var desktopResource))
                return null;

            try
            {
                using var desktopTexture = desktopResource!.QueryInterface<ID3D11Texture2D>();
                return EncodeFrame(device, context, desktopTexture);
            }
            finally
            {
                desktopResource!.Dispose();
                duplication.ReleaseFrame();
            }
        }
    }

    // Returns a frame that actually contains desktop pixels. The very first frame
    // after DuplicateOutput is a blank, never-presented surface (LastPresentTime
    // == 0) — accepting it yields an all-black capture. So skip un-presented
    // frames and wait (within the budget) for the first real presented frame.
    private bool TryAcquireFrame(IDXGIOutputDuplication duplication, out IDXGIResource? desktopResource)
    {
        desktopResource = null;
        var deadline = Environment.TickCount64 + AcquireBudgetMs;

        while (Environment.TickCount64 < deadline)
        {
            var result = duplication.AcquireNextFrame(AcquireTimeoutMs, out var info, out var resource);

            if (result.Code == Vortice.DXGI.ResultCode.WaitTimeout.Code)
                continue; // no frame this interval — nothing acquired to release

            if (result.Failure)
            {
                _logger.LogDebug("AcquireNextFrame failed: {Result}", result);
                resource?.Dispose();
                return false;
            }

            if (info.LastPresentTime > 0)
            {
                desktopResource = resource; // real pixels — caller releases it
                return true;
            }

            // Blank initial/no-present frame: release it and wait for a real one.
            resource.Dispose();
            duplication.ReleaseFrame();
        }

        _logger.LogDebug("No presented frame within {Ms}ms (display may be static)", AcquireBudgetMs);
        return false;
    }

    private static byte[] EncodeFrame(
        ID3D11Device device, ID3D11DeviceContext context, ID3D11Texture2D desktopTexture)
    {
        var desc = desktopTexture.Description;

        // The duplicated texture lives in GPU memory; copy into a CPU-readable
        // staging texture so we can map and read its pixels.
        var stagingDesc = desc with
        {
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CPUAccessFlags = CpuAccessFlags.Read,
            MiscFlags = ResourceOptionFlags.None,
        };

        using var staging = device.CreateTexture2D(stagingDesc);
        context.CopyResource(staging, desktopTexture);

        var map = context.Map(staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        try
        {
            return BgraToJpeg(map.DataPointer, (int)map.RowPitch, (int)desc.Width, (int)desc.Height);
        }
        finally
        {
            context.Unmap(staging, 0);
        }
    }

    private static byte[] BgraToJpeg(IntPtr src, int srcRowPitch, int width, int height)
    {
        // DXGI desktop format is B8G8R8A8_UNORM, which matches GDI+'s
        // 32bppArgb byte order (B,G,R,A) — copy row by row honoring the pitch.
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var bmpData = bitmap.LockBits(
            new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            var rowBytes = width * 4;
            var row = new byte[rowBytes];
            for (var y = 0; y < height; y++)
            {
                Marshal.Copy(src + (y * srcRowPitch), row, 0, rowBytes);
                Marshal.Copy(row, 0, bmpData.Scan0 + (y * bmpData.Stride), rowBytes);
            }
        }
        finally
        {
            bitmap.UnlockBits(bmpData);
        }

        using var ms = new MemoryStream();
        bitmap.Save(ms, JpegEncoder.Value, JpegParams.Value);
        return ms.ToArray();
    }

    // 0 = auto: pick the first non-primary attached output (usually the
    // ProPresenter projector), else the primary. A positive value selects the
    // Nth attached output (1-based) in enumeration order.
    private (IDXGIAdapter1?, IDXGIOutput?) SelectOutput(IDXGIFactory1 factory, int displayIndex)
    {
        var attached = new List<(IDXGIAdapter1 Adapter, IDXGIOutput Output, bool IsPrimary)>();

        for (uint a = 0; factory.EnumAdapters1(a, out var adapter).Success; a++)
        {
            var keepAdapter = false;
            for (uint o = 0; adapter.EnumOutputs(o, out var output).Success; o++)
            {
                var od = output.Description;
                if (od.AttachedToDesktop)
                {
                    var rect = od.DesktopCoordinates;
                    var isPrimary = rect.Left == 0 && rect.Top == 0;
                    attached.Add((adapter, output, isPrimary));
                    keepAdapter = true;
                }
                else
                {
                    output.Dispose();
                }
            }

            if (!keepAdapter)
                adapter.Dispose();
        }

        if (attached.Count == 0)
            return (null, null);

        int chosen;
        if (displayIndex > 0)
            chosen = Math.Min(displayIndex - 1, attached.Count - 1);
        else
        {
            var firstNonPrimary = attached.FindIndex(x => !x.IsPrimary);
            chosen = firstNonPrimary >= 0 ? firstNonPrimary : 0;
        }

        // Dispose the outputs/adapters we aren't using.
        for (var i = 0; i < attached.Count; i++)
        {
            if (i == chosen)
                continue;
            attached[i].Output.Dispose();
            if (!ReferenceEquals(attached[i].Adapter, attached[chosen].Adapter))
                attached[i].Adapter.Dispose();
        }

        return (attached[chosen].Adapter, attached[chosen].Output);
    }

    private static readonly Lazy<ImageCodecInfo> JpegEncoder = new(() =>
        ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid));

    private static readonly Lazy<EncoderParameters> JpegParams = new(() =>
    {
        var p = new EncoderParameters(1);
        p.Param[0] = new EncoderParameter(Encoder.Quality, JpegQuality);
        return p;
    });
}
