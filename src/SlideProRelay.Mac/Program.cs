using AppKit;

NSApplication.Init();
var app = NSApplication.SharedApplication;
app.Delegate = new SlideProRelay.Mac.AppDelegate();
app.Run();
