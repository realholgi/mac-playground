# mac-playground

This is a mix of code that was developed during our effort to port eM Client to macOS. It is a hodgepodge of various projects and experiments. The notable ones are listed below. The code is released as-is.

To test the experiments:
* Make sure .NET 10 SDK and the macOS workload for Xcode 27 are installed (macOS 14 or newer)
* Run `dotnet workload restore` from the Terminal
* Open solution folder in Visual Studio Code or Visual Studio
* Run the test application

Debug macOS builds omit the hardened runtime so ad-hoc signed native libraries can load. Release builds retain the hardened runtime and require appropriate code signing for distribution.

Trim analysis is disabled for normal, untrimmed builds. Set `PublishTrimmed=true` when preparing a trimmed build to enable the analyzer and review its warnings before shipping.

Runtime packages track stable .NET 10 releases. Reference-only packages retain the existing .NET 8 API compatibility contracts and .NET 7 drawing facade contract, with their latest servicing updates. `Microsoft.DotNet.ApiCompat` and `Microsoft.DotNet.GenFacades` remain pinned because they have no stable releases on the configured feeds.

`MacBridge.Test` uses NUnit 5 and PackageReference dependencies only. Run it with `dotnet test MacBridge.Test/MacBridge.Test.csproj`. Its active image-conversion test references `System.Drawing.Common` directly and loads its copied JPEG fixture from the test output directory, so the test project does not require external WinApi sources. The separate `MacBridge` project still requires the external `../../../WinApi` source tree linked by `MacBridge.csproj`; it is not included in this repository.

The drawing backend retains synchronous `Graphics.FromHwnd` and generic WinForms system-color mappings: AppKit's supported drawing and color APIs are not drop-in replacements. Local `CA1422` exemptions at these call sites keep their behavior without hiding other warnings. Formatter-based ResX/Cursor serialization remains for compatibility; `ResXDataNode` follows Windows WinForms .NET 10's local `SYSLIB0050` exemption to retain rejection of non-serializable values. The `WebForm` example uses `WKWebView` to display its page.

The file-descriptor experiment no longer compares the CFNetwork HTTP handler, which is obsolete on all supported macOS versions; it still compares URL-session, sockets, and Mono web request handlers.

The managed POSIX limit struct was renamed from `LibC.rlimit` to `LibC.ResourceLimit`; callers must update the type name. The native `getrlimit`/`setrlimit` entry points and struct layout are unchanged.

The `Core Data Test` example uses a main-queue managed object context, matching the UI-thread code that creates and saves its in-memory objects.

The `CancellationForm` example checks TCP connectivity to `www.google.com:443` on macOS, both after network address changes and every 30 seconds. `NetworkUtility.IsNetworkAvailable` reflects the last completed check (initially `false`); it is not a synchronous network probe.

## System.Drawing

The `System.Drawing` directory contains a fork of the https://github.com/mono/sysdrawing-coregraphics project. We have enhanced the API surface to be compatible enough with System.Drawing to run System.Windows.Forms on top of it. In addition we have implemented some missing APIs and fixed compatibility issues with pixel rounding.

## System.Windows.Forms

The `System.Windows.Forms` directory contains a fork of the Mono System.Windows.Forms implementation. It contains a Cocoa backend to allow applications run on 64-bit macOS systems. Layout code was heavily overhauled and debugged on both a test application and a full UI of eM Client. Further experiments were made with replacing some controls with their native counterparts (akin to https://github.com/Clancey/MonoMac.Windows.Form), which can be done on per-control basis.

The automated control suite is in `tests/FormsTest.Test`. Run it on macOS from a
logged-in graphical session (.NET 10 SDK, macOS workload, and Xcode 27 required):

```sh
dotnet build tests/FormsTest.Test/FormsTest.Test.csproj
tests/FormsTest.Test/bin/Debug/net10.0-macos27.0/osx-arm64/FormsTest.Test.app/Contents/MacOS/FormsTest.Test --result=/tmp/mac-playground-forms-tests.xml
```

Use `osx-x64` instead of `osx-arm64` on Intel Macs. The NUnitLite runner forces
execution on the process main thread for AppKit; do not run this suite through
`dotnet test`. It exits nonzero on failures and writes NUnit XML results.
NUnitLite options such as `--where` can select individual tests.

The suite replaces the standalone `AnchorResize` executable and verifies
anchored growth/shrinkage with and without suspended layout, table row spans and
docking, text selection/replacement and multiline editing, scrollbar input
boundaries and change notifications, date/calendar selection, and unwrapped
autosize flow-direction changes. It links the existing `FormsTest` demo sources
instead of copying their implementations. Additional `DataGridView` tests verify
viewport/displayed-row updates, preserved current cell and selection, and rejection
of hidden or out-of-range scroll targets. These use the repository's grid directly;
the separate `DataGridForm` demo depends on external eM Client controls.
The manual application remains available for visual rendering, native dialogs,
printing, notifications, networking, and system-changing experiments; those are
not covered by this deterministic suite.

`DataGridView.FirstDisplayedScrollingRowIndex` scrolls through the vertical
scrollbar without changing the current cell or selection. Its getter follows
the actual viewport, and row `Displayed` flags are refreshed when painting so
rows scrolled out of view are no longer reported as visible.

## License

The code is released under the MIT X11 license unless noted otherwise in a specific source file. Any changes in this repository to the original code are published under the MIT X11 license.

### MIT X11 License

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the
"Software"), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
