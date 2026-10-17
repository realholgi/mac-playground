using System;
using System.Reflection;
using AppKit;
using CoreGraphics;
using Foundation;
using NUnit.Framework;
using System.Windows.Forms;

namespace FormsTest.Test;

[TestFixture]
[NonParallelizable]
[SingleThreaded]
public sealed class NativeMultilineEditBoundaryTests
{
    [TestCase(4, 0, 1, false)]
    [TestCase(3, 2, 2, false)]
    [TestCase(0, 5, 0, false)]
    [TestCase(1, 3, 1, true)]
    [TestCase(4, 0, 0, true)]
    public void EditRangesAreCheckedBeforeCallingCustomCallback(
        int start, int length, int extra, bool valid)
    {
        using var textBox = new TextBox { Multiline = true, MaxLength = 0 };
        using var native = Native("abcd");
        int invoked = 0;
        SetCallback(textBox, (_, _) => { invoked++; return true; });
        var ranges = new[] { NSValue.FromRange(new NSRange(start, length + extra)) };

        bool accepted = AllowChange(textBox, native, ranges, new[] { "x" });
        Assert.That(accepted, Is.EqualTo(valid));
        Assert.That(invoked, Is.EqualTo(valid ? 1 : 0));
        Assert.That(native.TextStorage.Length, Is.EqualTo((nuint)4));
    }

    [Test]
    public void CallbackReceivesAllRangesAndCanRejectOtherwiseValidEdit()
    {
        using var textBox = new TextBox { Multiline = true, MaxLength = 8 };
        using var native = Native("abcdef");
        int[] seen = null;
        string[] text = null;
        SetCallback(textBox, (ranges, replacement) =>
        {
            seen = ranges;
            text = replacement;
            return false;
        });
        var ranges = new[] { NSValue.FromRange(new NSRange(0, 1)), NSValue.FromRange(new NSRange(3, 2)) };

        Assert.That(AllowChange(textBox, native, ranges, new[] { "xx", "y" }), Is.False);
        Assert.That(seen, Is.EqualTo(new[] { 0, 1, 3, 2 }));
        Assert.That(text, Is.EqualTo(new[] { "xx", "y" }));
        Assert.That(native.TextStorage.Length, Is.EqualTo((nuint)6));
    }

    [Test]
    public void LengthLimitRejectsGrowthBeforeCallingCallback()
    {
        using var textBox = new TextBox { Multiline = true, MaxLength = 4 };
        using var native = Native("abcd");
        int invoked = 0;
        SetCallback(textBox, (_, _) => { invoked++; return true; });
        Assert.That(AllowChange(textBox, native,
            new[] { NSValue.FromRange(new NSRange(4, 0)) }, new[] { "x" }), Is.False);
        Assert.That(invoked, Is.Zero);
    }

    private static NSTextView Native(string value)
    {
        var view = new NSTextView(new CGRect(0, 0, 100, 40));
        view.TextStorage.SetString(new NSAttributedString(value));
        return view;
    }

    private static bool AllowChange(TextBox box, NSTextView view, NSValue[] ranges, string[] replacements)
    {
        var method = typeof(TextBoxBase).GetMethod("TextViewShouldChangeTextInRanges",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (bool)method.Invoke(box, new object[] { view, ranges, replacements });
    }

    private static void SetCallback(TextBox box, Func<int[], string[], bool> callback)
    {
        var prop = typeof(TextBoxBase).GetProperty("ShouldChangeTextsInRanges",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(prop, Is.Not.Null);
        prop.SetValue(box, callback);
    }
}
