# mac-playground

This is a mix of code that was developed during our effort to port eM Client to macOS. It is a hodgepodge of various projects and experiments. The notable ones are listed below. The code is released as-is.

To test the experiments:
* Make sure .NET 10 SDK and the macOS workload for Xcode 27 are installed (macOS 14 or newer)
* Run `dotnet workload restore` from the Terminal
* Open solution folder in Visual Studio Code or Visual Studio
* Run the test application

Debug macOS builds omit the hardened runtime so ad-hoc signed native libraries can load. Release builds retain the hardened runtime and require appropriate code signing for distribution.

Trim analysis is disabled for normal, untrimmed builds. Set `PublishTrimmed=true` when preparing a trimmed build to enable the analyzer and review its warnings before shipping.

Some build warnings remain for the WinForms drawing backend and formatter-based ResX/Cursor serialization. Their modern replacements do not always provide equivalent behavior; these warnings are left visible rather than suppressed. The `WebForm` example uses `WKWebView` to display its page.

The managed POSIX limit struct was renamed from `LibC.rlimit` to `LibC.ResourceLimit`; callers must update the type name. The native `getrlimit`/`setrlimit` entry points and struct layout are unchanged.

The `Core Data Test` example uses a main-queue managed object context, matching the UI-thread code that creates and saves its in-memory objects.

The `CancellationForm` example checks TCP connectivity to `www.google.com:443` on macOS, both after network address changes and every 30 seconds. `NetworkUtility.IsNetworkAvailable` reflects the last completed check (initially `false`); it is not a synchronous network probe.

## System.Drawing

The `System.Drawing` directory contains a fork of the https://github.com/mono/sysdrawing-coregraphics project. We have enhanced the API surface to be compatible enough with System.Drawing to run System.Windows.Forms on top of it. In addition we have implemented some missing APIs and fixed compatibility issues with pixel rounding.

## System.Windows.Forms

The `System.Windows.Forms` directory contains a fork of the Mono System.Windows.Forms implementation. It contains a Cocoa backend to allow applications run on 64-bit macOS systems. Layout code was heavily overhauled and debugged on both a test application and a full UI of eM Client. Further experiments were made with replacing some controls with their native counterparts (akin to https://github.com/Clancey/MonoMac.Windows.Form), which can be done on per-control basis.

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
