# Forms regression tests

Build and run on macOS with the matching .NET/macOS workload and Xcode described in the repository README. AppKit tests require an interactive, logged-in graphical session. The NUnitLite entry point already selects `--mainthread --workers=0`.

```sh
dotnet build tests/FormsTest.Test/FormsTest.Test.csproj --configuration Debug --runtime osx-arm64
mkdir -p TestResults

tests/FormsTest.Test/bin/Debug/net10.0-macos27.0/osx-arm64/FormsTest.Test.app/Contents/MacOS/FormsTest.Test \
  --result="$PWD/TestResults/forms.xml"
```

Omitting `--where` runs the **complete FormsTest.Test assembly**, including the original accessibility, layout, scrolling, text and date fixtures. CI executes that full assembly twice in fresh processes. The separate `MacBridge.Test` job still selects `MatrixTests`; it does not claim to execute all bridge tests.

## Lifecycle and previously missing coverage

- `FormatterLifecycleTests` retains real native fields/formatters across actual TextBox disposal. Late formatting/conversion must be safe and must not invoke a disposed control. Active preprocessing and conversion errors remain tested. The unchanged production code failed four of these seven cases in the reproduction run for PR #12.
- `NativeDialogButtonTests` shows real windows and pumps AppKit events. Do not remove the event pump to hide late callbacks from other controls.
- `NativeDialogKeyboardTests` sends constructed `NSEvent` values through the native button's `PerformKeyEquivalent` method and observes managed Click events. It covers separate/shared roles, disabled/re-enabled buttons, modifier keys and role removal. These are native dispatch integration tests, not OS-level keyboard automation.
- `GraphicsStateAndWritableBitmapTests` checks `ReadWrite`/`WriteOnly`, read-only snapshots including translucent images, and three nested saves/restores with actual native pixels and transforms.

Pixel checks inspect the bitmap's borrowed CoreGraphics context directly, so they do not use `GetPixel` or `LockBits` as the oracle for the feature being tested. Every writable lock is paired with `UnlockBits` in `finally`.

CI retains XML/TRX reports only on failure, for three days. Successful runs do not upload artifacts.
