using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace SlideProRelay.Tray;

internal sealed class UpdateChecker
{
    private const string ReleaseUrl =
        "https://slidepro.io/api/public/v1/releases/latest?platform=windows";

    internal sealed record UpdateInfo(Version Version, string DownloadUrl, string ReleaseNotes);

    /// <summary>
    /// Returns update info if the server has a newer version, null otherwise.
    /// Swallows all exceptions — a failed check is treated as "no update."
    /// </summary>
    public static async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            var current = Assembly.GetEntryAssembly()?.GetName().Version;
            if (current is null) return null;

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var json = await client.GetStringAsync(ReleaseUrl);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("version", out var vEl) ||
                !Version.TryParse(vEl.GetString(), out var server))
                return null;

            // Compare Major.Minor.Patch only (assembly version has a 4th Revision component).
            if (CompareMmp(server, current) <= 0) return null;

            var url = root.TryGetProperty("downloadUrl", out var uEl) ? uEl.GetString() ?? "" : "";
            var notes = root.TryGetProperty("releaseNotes", out var nEl) ? nEl.GetString() ?? "" : "";

            if (string.IsNullOrEmpty(url)) return null;

            return new UpdateInfo(server, url, notes);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Downloads the installer to %TEMP%, launches it with /SILENT, then exits the app.
    /// Reports download progress (0–100) via <paramref name="onProgress"/>.
    /// </summary>
    public static async Task DownloadAndInstallAsync(UpdateInfo update, Action<int> onProgress)
    {
        var tempFile = Path.Combine(
            Path.GetTempPath(),
            $"SlideProRelay-{update.Version}-Setup.exe");

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        using var response = await client.GetAsync(
            update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1L;
        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var file = File.Create(tempFile);

        var buffer = new byte[81_920];
        long downloaded = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read));
            downloaded += read;
            if (total > 0)
                onProgress((int)(downloaded * 100 / total));
        }

        // Inno Setup's /SILENT flag handles kill-running-instance + restart via
        // CloseApplications=yes and RestartApplications=yes in the .iss script.
        Process.Start(new ProcessStartInfo(tempFile, "/SILENT") { UseShellExecute = true });
        Application.Exit();
    }

    private static int CompareMmp(Version a, Version b)
    {
        if (a.Major != b.Major) return a.Major.CompareTo(b.Major);
        if (a.Minor != b.Minor) return a.Minor.CompareTo(b.Minor);
        return Math.Max(0, a.Build).CompareTo(Math.Max(0, b.Build));
    }
}
