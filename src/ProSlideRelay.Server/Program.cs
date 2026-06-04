using ProSlideRelay.Server.ProPresenter;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ProPresenterOptions>(
    builder.Configuration.GetSection(ProPresenterOptions.SectionName));

builder.Services.AddHttpClient<IProPresenterClient, ProPresenterClient>((sp, client) =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ProPresenterOptions>>().Value;
    client.BaseAddress = new Uri($"http://{opts.Host}:{opts.Port}");
    client.Timeout = TimeSpan.FromSeconds(3);
});

var app = builder.Build();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/current", async (IProPresenterClient client) =>
{
    var status = await client.GetCurrentSlideAsync();
    return Results.Ok(status);
});

app.Run();
