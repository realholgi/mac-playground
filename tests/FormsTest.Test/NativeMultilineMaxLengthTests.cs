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
public sealed class NativeMultilineMaxLengthTests
{
    [TestCase("abcd", 4, 4, 0, "x", false)]
    [TestCase("abc", 4, 3, 0, "x", true)]
    [TestCase("abcd", 4, 2, 2, "xy", true)]
    [TestCase("abcd", 4, 2, 2, "xyz", false)]
    [TestCase("abcd", 4, 2, 2, "", true)]
    [TestCase("abcdef", 4, 0, 2, "", true)]
    [TestCase("abcdef", 4, 0, 0, "z", false)]
    [TestCase("abcd", 0, 4, 0, "unlimited", true)]
    public void NativeEditRespectsResultingLength(
        string initial, int limit, int start, int length, string insertion, bool expected)
    {
        using var textBox = new TextBox { Multiline = true, MaxLength = limit };
        using var native = new NSTextView(new CGRect(0, 0, 100, 40));
        native.TextStorage.SetString(new NSAttributedString(initial));
        var range = NSValue.FromRange(new NSRange(start, length));

        Assert.That(AllowChange(textBox, native, new[] { range }, new[] { insertion }), Is.EqualTo(expected));
    }

    [Test]
    public void MultipleRangesAreCountedTogether()
    {
        using var textBox = new TextBox { Multiline = true, MaxLength = 6 };
        using var native = new NSTextView(new CGRect(0, 0, 100, 40));
        native.TextStorage.SetString(new NSAttributedString("abcdef"));
        var ranges = new[] { NSValue.FromRange(new NSRange(1, 2)), NSValue.FromRange(new NSRange(4, 1)) };

        Assert.That(AllowChange(textBox, native, ranges, new[] { "Z", "XY" }), Is.True);
        Assert.That(AllowChange(textBox, native, ranges, new[] { "ZZ", "XYZ" }), Is.False);
    }

    private static bool AllowChange(TextBox textBox, NSTextView native, NSValue[] ranges, string[] replacements)
    {
        var callback = typeof(TextBoxBase).GetMethod(
            "TextViewShouldChangeTextInRanges", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(callback, Is.Not.Null);
        return (bool)callback.Invoke(textBox, new object[] { native, ranges, replacements });
    }
}
