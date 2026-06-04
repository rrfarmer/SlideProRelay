using ProSlideRelay.Server;
using ProSlideRelay.Server.Startup;

var app = ServerHost.Create(args);

app.Lifetime.ApplicationStarted.Register(() =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    var addresses = app.Urls.Count > 0 ? app.Urls : ["http://localhost:5174"];
    NetworkUrlPrinter.Print(logger, addresses);
});

await app.RunAsync();
