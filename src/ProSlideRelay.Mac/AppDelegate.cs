using AppKit;
using Foundation;

namespace ProSlideRelay.Mac;

[Register("AppDelegate")]
public sealed class AppDelegate : NSApplicationDelegate
{
    private StatusBarController? _controller;

    public override void DidFinishLaunching(NSNotification notification)
    {
        // Hide from the Dock — appear only in the menu bar
        NSApplication.SharedApplication.ActivationPolicy = NSApplicationActivationPolicy.Accessory;

        _controller = new StatusBarController();
    }

    public override void WillTerminate(NSNotification notification)
    {
        _controller?.Dispose();
    }

    // Don't quit when the last settings window is closed
    public override bool ApplicationShouldTerminateAfterLastWindowClosed(NSApplication sender) => false;
}
