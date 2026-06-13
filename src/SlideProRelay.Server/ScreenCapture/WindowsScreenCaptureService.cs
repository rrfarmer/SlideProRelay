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
    private const int MaxUploadWidth = 1920;        // matches SlidePro server-side cap

    private readonly ILogger<WindowsScreenCaptureService> _logger;

    public WindowsScreenCaptureService(ILogger<WindowsScreenCaptureService> logger) => _logger = logger;

    public bool IsSupported => true;

    public IReadOnlyList<CaptureDisplay> GetDisplays()
    {
        try
        {
            using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
            var displays = new List<CaptureDisplay>();
            var index = 1;

            for (uint a = 0; factory.EnumAdapters1(a, out var adapter).Success; a++)
            {
                using (adapter)
                {
                    for (uint o = 0; adapter.EnumOutputs(o, out var output).Success; o++)
                    {
                        using (output)
                        {
                            var d = output.Description;
                            if (!d.AttachedToDesktop)
                                continue;

                            var r = d.DesktopCoordinates;
                            displays.Add(new CaptureDisplay(
                                index++, r.Right - r.Left, r.Bottom - r.Top, r.Left, r.Top,
                                IsPrimary: r.Left == 0 && r.Top == 0));
                        }
                    }
                }
            }

            return displays;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Display enumeration failed: {Message}", ex.Message);
            return [];
        }
    }

    public Task<byte[]?> CaptureAsync(int displayIndex, CancellationToken ct = default) =>
        // Desktop Duplication is entirely synchronous COM; keep it off the caller.
        Task.Run(() => Capture(displayIndex), ct);

    private byte[]? Capture(int displayIndex)
    {
        // Enumerate with live COM handles so we can keep the chosen one and capture it.
        var attached = new List<(IDXGIAdapter1 Adapter, IDXGIOutput Output, bool IsPrimary)>();
        try
        {
            using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
            for (uint a = 0; factory.EnumAdapters1(a, out var adapter).Success; a++)
            {
                var keepAdapter = false;
                for (uint o = 0; adapter.EnumOutputs(o, out var output).Success; o++)
                {
                    var d = output.Description;
                    if (d.AttachedToDesktop)
                    {
                        var r = d.DesktopCoordinates;
                        attached.Add((adapter, output, r.Left == 0 && r.Top == 0));
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
            {
                _logger.LogDebug("No attached displays to capture");
                return null;
            }

            int chosen;
            if (displayIndex >= 1 && displayIndex <= attached.Count)
            {
                chosen = displayIndex - 1;
            }
            else
            {
                var firstNonPrimary = attached.FindIndex(x => !x.IsPrimary);
                chosen = firstNonPrimary >= 0 ? firstNonPrimary : 0;
            }

            return CaptureOutput(attached[chosen].Adapter, attached[chosen].Output);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Screen capture failed: {Message}", ex.Message);
            return null;
        }
        finally
        {
            // Dispose every output, and each distinct adapter exactly once.
            var adapters = new HashSet<IDXGIAdapter1>();
            foreach (var (adapter, output, _) in attached)
            {
                output.Dispose();
                adapters.Add(adapter);
            }
            foreach (var adapter in adapters)
                adapter.Dispose();
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

        Bitmap toEncode = bitmap;
        if (width > MaxUploadWidth)
        {
            var scale = (double)MaxUploadWidth / width;
            toEncode = new Bitmap(bitmap, MaxUploadWidth, (int)(height * scale));
        }

        try
        {
            using var ms = new MemoryStream();
            toEncode.Save(ms, JpegEncoder.Value, JpegParams.Value);
            return ms.ToArray();
        }
        finally
        {
            if (!ReferenceEquals(toEncode, bitmap))
                toEncode.Dispose();
        }
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
