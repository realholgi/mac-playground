using System.Linq;
using System.Windows.Forms;
using NUnitLite;

namespace FormsTest.Test;

internal static class Program
{
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        // There is no Application.Run loop to promote the test process to a foreground app.
        var application = AppKit.NSApplication.SharedApplication;
        application.ActivationPolicy = AppKit.NSApplicationActivationPolicy.Regular;
        application.Activate();
        if (!application.Active)
        {
            AppKit.NSRunningApplication.CurrentApplication.Activate(
                AppKit.NSWorkspace.SharedWorkspace.FrontmostApplication,
                AppKit.NSApplicationActivationOptions.ActivateAllWindows);
        }

        // AppKit controls must stay on the process main thread, not NUnit workers.
        return new AutoRun().Execute(new[] { "--mainthread", "--workers=0" }.Concat(args).ToArray());
    }
}
