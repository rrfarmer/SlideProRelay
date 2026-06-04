using AppKit;
using Foundation;

namespace SlideProRelay.Mac;

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

    // Stop the embedded web server off the main thread before quitting.
    // Blocking the main (UI) thread on async shutdown deadlocks AppKit's run
    // loop — that was the "beach ball on quit" hang. Returning Later lets the
    // run loop keep spinning while shutdown runs in the background.
    public override NSApplicationTerminateReply ApplicationShouldTerminate(NSApplication sender)
    {
        if (_controller is null)
            return NSApplicationTerminateReply.Now;

        _controller.BeginShutdown(() => sender.ReplyToApplicationShouldTerminate(true));
        return NSApplicationTerminateReply.Later;
    }

    public override void WillTerminate(NSNotification notification)
    {
        _controller?.Dispose();
    }

    // Don't quit when the last settings window is closed
    public override bool ApplicationShouldTerminateAfterLastWindowClosed(NSApplication sender) => false;
}
