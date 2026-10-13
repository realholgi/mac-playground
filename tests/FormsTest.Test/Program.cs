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

        // AppKit controls must stay on the process main thread, not NUnit workers.
        return new AutoRun().Execute(new[] { "--mainthread", "--workers=0" }.Concat(args).ToArray());
    }
}
