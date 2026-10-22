# mac-playground

This is a mix of code that was developed during our effort to port eM Client to macOS. It is a hodgepodge of various projects and experiments. The notable ones are listed below. The code is released as-is.

To test the experiments:
* Make sure Xcode 27, .NET 10 SDK, and the matching macOS workload are installed (macOS 26.6 or newer for Xcode 27)
* Run `dotnet workload restore` from the Terminal
* Open solution folder in Visual Studio Code or Visual Studio
* Run the test application

Debug macOS builds omit the hardened runtime so ad-hoc signed native libraries can load. Release builds retain the hardened runtime and require appropriate code signing for distribution.

Trim analysis is disabled for normal, untrimmed builds. Set `PublishTrimmed=true` when preparing a trimmed build to enable the analyzer and review its warnings before shipping.

Runtime packages track stable .NET 10 releases, except for the serviced managed `Accessibility.dll` matching the existing .NET 8 WinForms contract. Reference-only packages pin the .NET 8 API compatibility contracts and .NET 7 drawing facade contract independently of runtime servicing. `Microsoft.DotNet.GenFacades` remains pinned because it has no stable release on the configured feeds.

### API compatibility checks

`System.Drawing.Common`, `System.Windows.Forms.Primitives`, and `System.Windows.Forms` run `Microsoft.DotNet.ApiCompat.Task` **10.0.401** during normal builds against the .NET 8.0.31 reference contracts. Existing API differences are recorded in `CompatibilitySuppressions.xml`; the obsolete text baselines have been removed. New public API differences fail the build rather than being automatically suppressed.

`eng/ApiCompat.targets` uses build-only `Mono.Cecil` 0.11.6 to prepare check-only reference copies under `obj`. It removes inaccessible interface declarations, including public interfaces nested in internal types and generic interfaces with inaccessible arguments. This avoids false positives for `ComboBox.ObjectCollection` and `WebBrowserSite` without disabling interface checks. Public interfaces, members, and attributes remain checked. The projected reference assemblies are not shipped.

The three assemblies now use version **8.0.0.0** and are public-signed with their respective Microsoft reference public keys. This matches contract identities; it is **not** a Microsoft signature or proof of authenticity. `MacBridge` uses the matching public key for friend access. Rebuild consumers after this identity change.

WinForms copies the real managed `Accessibility.dll` from the official `Microsoft.WindowsDesktop.App.Runtime.win-x64` 8.0.32 servicing package, including the [security fixes for affected 8.0.31 packages](https://github.com/advisories/GHSA-4j9v-h7pp-725h). The .NET 8.0.31 reference-only API contracts remain unchanged. Other Windows runtime components are excluded. Applications must bundle this DLL, not the reference-only assembly or an embedded interop substitute. Consumers that directly compile against `IAccessible` also need an explicit reference, as demonstrated in `tests/FormsTest.Test`. The adapter exposes the existing managed accessibility behavior; it does not add native Windows accessibility services or AppKit accessibility integration.

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
execution on the process main thread, uses regular AppKit application policy, and
requests [cooperative activation](https://developer.apple.com/documentation/appkit/nsrunningapplication/activate(from:options:))
without an `Application.Run` loop. Focus-sensitive tests wait for actual native
focus; keep the desktop unlocked and avoid switching applications during the run.
Do not run this suite through `dotnet test`. It exits nonzero on failures and
writes NUnit XML results.
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
Accessibility tests cover child IDs and bounds, metadata changes, checkbox actions
and state, focus selection, invalid child IDs, and ownerless objects.
The manual application remains available for visual rendering, native dialogs,
printing, notifications, networking, and system-changing experiments; those are
not covered by this deterministic suite.

`DataGridView.FirstDisplayedScrollingRowIndex` scrolls through the vertical
scrollbar without changing the current cell or selection. Its getter follows
the actual viewport, and row `Displayed` flags are refreshed when painting so
rows scrolled out of view are no longer reported as visible.

## Drawing and sorting regression tests

The focused regression fixtures cover:

* `MacBridge.Test.MatrixTests`: invertibility of identity, reflection, singular,
  and very small matrices, plus an inverse point round trip with an explicit
  numerical tolerance.
* `FormsTest.Test.GraphicsTransformTests`: independent `Transform` copies,
  `Append`/`Prepend` multiplication, coordinate conversion, and offscreen drawing
  after multiplication, translation, and reset.
* `FormsTest.Test.DataGridViewSortTests`: fractional, negative, large, and duplicate
  numeric values under `de-DE` and `en-US`, including repeated sorts and reversed
  direction.

The graphics fixture uses the Forms runner because `Graphics` initializes AppKit
screen state even when drawing to an offscreen bitmap. Run the focused checks on
an Apple Silicon Mac with the prerequisites above:

```sh
dotnet test MacBridge.Test/MacBridge.Test.csproj --configuration Debug --runtime osx-arm64 --filter 'FullyQualifiedName~MacBridge.Test.MatrixTests'
dotnet build tests/FormsTest.Test/FormsTest.Test.csproj --configuration Debug --runtime osx-arm64
tests/FormsTest.Test/bin/Debug/net10.0-macos27.0/osx-arm64/FormsTest.Test.app/Contents/MacOS/FormsTest.Test --where 'class == FormsTest.Test.GraphicsTransformTests or class == FormsTest.Test.DataGridViewSortTests' --result=/tmp/mac-playground-regression-tests.xml
```

[The regression workflow](.github/workflows/regression-tests.yml) runs these
fixtures for relevant pull requests and pushes to `main`, and supports manual
dispatch. It uses the `xcode-27` ARM64 runner with Xcode 27.0, .NET SDK 10.0.401,
and [macOS workload set 10.0.401.1](https://github.com/dotnet/macios/releases/tag/dotnet-10.0.1xx-xcode27.0-10722). It builds the two test projects directly and
retains only XML/TRX reports on failure, for three days.

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
