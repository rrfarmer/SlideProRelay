using ProSlideRelay.Server.ProPresenter;
using ProSlideRelay.Server.ProPresenter.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ProPresenterOptions>(
    builder.Configuration.GetSection(ProPresenterOptions.SectionName));

builder.Services.AddHttpClient<IProPresenterClient, ProPresenterClient>((sp, client) =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ProPresenterOptions>>().Value;
    client.BaseAddress = new Uri($"http://{opts.Host}:{opts.Port}");
    client.Timeout = TimeSpan.FromSeconds(3);
});

builder.Services.AddSingleton<SlideCache>();
builder.Services.AddHostedService<SlidePollingService>();

var app = builder.Build();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/current", (SlideCache cache) =>
{
    var status = cache.Latest;
    if (status is null)
        return Results.Ok(new { connection = "starting", current = (object?)null, next = (object?)null });

    return Results.Ok(new
    {
        connection = status.Connection.ToString().ToLowerInvariant(),
        current = status.Current is null ? null : new { status.Current.Uuid, status.Current.Text, status.Current.Notes },
        next = status.Next is null ? null : new { status.Next.Uuid, status.Next.Text, status.Next.Notes },
        updatedAt = status.UpdatedAt,
    });
});

app.Run();
