using AppKit;

NSApplication.Init();
var app = NSApplication.SharedApplication;
app.Delegate = new ProSlideRelay.Mac.AppDelegate();
app.Run();
