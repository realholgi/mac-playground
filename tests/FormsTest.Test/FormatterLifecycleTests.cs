using System;
using System.Drawing;
using System.Windows.Forms;
using AppKit;
using Foundation;
using NUnit.Framework;

namespace FormsTest.Test;

[TestFixture]
[NonParallelizable]
[SingleThreaded]
public sealed class FormatterLifecycleTests
{
    [Test]
    public void DisposingTextBoxDetachesFormatterFromRetainedNativeField()
    {
        using var box = new TrackingTextBox { Text = "initial" };
        var field = CreateField(box);
        var formatter = field.Formatter;
        Assert.That(formatter, Is.Not.Null);

        box.Dispose();

        Assert.That(box.IsDisposed, Is.True);
        Assert.That(field.Formatter, Is.Null, "A retained native field must not call the released bridge.");
        Assert.DoesNotThrow(() => box.Dispose(), "Disposal must remain idempotent.");
        GC.KeepAlive(formatter);
        GC.KeepAlive(field);
    }

    [Test]
    public void LateStringForAfterDisposalDoesNotCallDisposedControl()
    {
        using var box = new TrackingTextBox { MaxLength = 3 };
        var field = CreateField(box);
        var formatter = field.Formatter;
        using var value = new NSString("abcdef");
        Assert.That(formatter.StringFor(value), Is.EqualTo("abc"), "Exercise the active formatter first.");

        box.Dispose();
        int callsAfterDispose = box.PreprocessCalls;
        string formatted = null;
        Assert.DoesNotThrow(() => formatted = formatter.StringFor(value));
        Assert.That(formatted, Is.EqualTo("abcdef"), "Detached formatting is pass-through, not disposed-control preprocessing.");
        Assert.That(formatter.StringFor(null), Is.Empty);
        Assert.That(box.PreprocessCalls, Is.EqualTo(callsAfterDispose));
        GC.KeepAlive(field);
    }

    [Test]
    public void LateGetObjectValueAfterDisposalIsSafeAndDoesNotApplyOldLimit()
    {
        using var box = new TrackingTextBox { MaxLength = 3 };
        var field = CreateField(box);
        var formatter = field.Formatter;
        box.Dispose();
        int callsAfterDispose = box.PreprocessCalls;
        NSObject result = null;
        NSString error = null;
        try
        {
            Assert.That(formatter.GetObjectValue(out result, "abcdef", out error), Is.True);
            Assert.That(error, Is.Null);
            Assert.That(result?.ToString(), Is.EqualTo("abcdef"));
            Assert.That(box.PreprocessCalls, Is.EqualTo(callsAfterDispose));
        }
        finally
        {
            result?.Dispose();
            error?.Dispose();
            GC.KeepAlive(field);
        }
    }

    [TestCase(0, "abcdef")]
    [TestCase(3, "abc")]
    public void ActiveFormatterPreservesPreprocessingAndMaxLength(int limit, string expected)
    {
        using var box = new TrackingTextBox { MaxLength = limit };
        var field = CreateField(box);
        var formatter = field.Formatter;
        using var value = new NSString("abcdef");
        Assert.That(formatter.StringFor(value), Is.EqualTo(expected));
        NSObject result = null;
        NSString error = null;
        try
        {
            Assert.That(formatter.GetObjectValue(out result, "abcdef", out error), Is.True);
            Assert.That(result?.ToString(), Is.EqualTo(expected));
            Assert.That(error, Is.Null);
            Assert.That(box.PreprocessCalls, Is.GreaterThan(0));
        }
        finally
        {
            result?.Dispose();
            error?.Dispose();
        }
    }

    [Test]
    public void ActiveConversionStillReportsPreprocessingFailure()
    {
        using var box = new TrackingTextBox();
        var field = CreateField(box);
        var formatter = field.Formatter;
        box.RejectInput = true;
        NSObject result = null;
        NSString error = null;
        try
        {
            Assert.That(formatter.GetObjectValue(out result, "invalid", out error), Is.False);
            Assert.That(error?.ToString(), Is.EqualTo("Rejected by test preprocessor"));
        }
        finally
        {
            box.RejectInput = false;
            result?.Dispose();
            error?.Dispose();
        }
    }

    [Test]
    public void ClosingEditedWindowAndPumpingNextWindowDoesNotInvokeReleasedFormatter()
    {
        for (int iteration = 0; iteration < 3; iteration++)
        {
            using var form = new Form { ClientSize = new Size(300, 120) };
            using var box = new TrackingTextBox { Text = "editable", Width = 200 };
            form.Controls.Add(box);
            try
            {
                form.Show();
                NSApplication.SharedApplication.Activate();
                form.Activate();
                box.Focus();
                Application.DoEvents();
                var formatter = CreateField(box).Formatter;
                form.Close();
                Assert.That(box.IsDisposed, Is.True);
                Assert.DoesNotThrow(() => Application.DoEvents());
                using var lateValue = new NSString("late");
                Assert.That(formatter.StringFor(lateValue), Is.EqualTo("late"));
            }
            finally { form.Close(); }
        }
    }

    private static NSTextField CreateField(TextBox box)
    {
        _ = box.Handle;
        var field = box.CreateView() as NSTextField;
        Assert.That(field, Is.Not.Null);
        Assert.That(field.Formatter, Is.Not.Null);
        return field;
    }

    private sealed class TrackingTextBox : TextBox
    {
        public int PreprocessCalls { get; private set; }
        public bool RejectInput { get; set; }

        protected override string PreprocessText(string value)
        {
            PreprocessCalls++;
            if (IsDisposed)
                throw new InvalidOperationException("Preprocessing reached a disposed control");
            if (RejectInput)
                throw new FormatException("Rejected by test preprocessor");
            return base.PreprocessText(value);
        }
    }
}
