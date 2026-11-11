using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AppKit;
using CoreGraphics;
using Foundation;
using NUnit.Framework;
using ObjCRuntime;

namespace FormsTest.Test;

[TestFixture]
[NonParallelizable]
[SingleThreaded]
public sealed class TextAccessibilityTests
{
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern CGRect FrameForRange(IntPtr receiver, IntPtr selector, NSRange range);

    [Test]
    public void CustomEditNativeValueAndSelectedTextWritesReachOwner()
    {
        using var form = new Form();
        using var edit = new MessageEdit { Text = "before", Size = new Size(220, 100) };
        form.Controls.Add(edit);
        try
        {
            form.Show();
            Application.DoEvents();
            var view = Runtime.GetNSObject<NSView>(edit.Handle);
            using var value = new NSString("A😀BCD");
            view.AccessibilityValue = value;
            Assert.That(edit.Text, Is.EqualTo("A😀BCD"));
            Assert.That(view.AccessibilityNumberOfCharacters, Is.EqualTo((nint)6));
            view.AccessibilitySelectedTextRange = new NSRange(1, 2);
            Assert.That(view.AccessibilitySelectedText, Is.EqualTo("😀"));
            view.AccessibilitySelectedText = "xy";
            Assert.That(edit.Text, Is.EqualTo("AxyBCD"));
            Assert.That(view.AccessibilityValue.ToString(), Is.EqualTo("AxyBCD"));
            Assert.That(view.AccessibilitySelectedTextRange.Location, Is.EqualTo((nint)3));
            Assert.That(view.AccessibilitySelectedTextRange.Length, Is.EqualTo((nint)0));
        }
        finally { form.Close(); }
    }

    [TestCase("readonly")]
    [TestCase("disabled")]
    [TestCase("hidden")]
    [TestCase("disposed")]
    public void UnavailableCustomEditRejectsBothNativeWritePaths(string state)
    {
        using var form = new Form();
        using var edit = new MessageEdit { Text = "unchanged" };
        form.Controls.Add(edit);
        try
        {
            form.Show();
            Application.DoEvents();
            var view = Runtime.GetNSObject<NSView>(edit.Handle);
            view.AccessibilitySelectedTextRange = new NSRange(0, 3);
            switch (state)
            {
                case "readonly": edit.ReadOnly = true; break;
                case "disabled": edit.Enabled = false; break;
                case "hidden": edit.Visible = false; break;
                case "disposed": edit.Dispose(); break;
            }
            Assert.That(view.IsAccessibilitySelectorAllowed(new Selector("setAccessibilityValue:")), Is.False);
            Assert.That(view.IsAccessibilitySelectorAllowed(new Selector("setAccessibilitySelectedText:")), Is.False);
            using var value = new NSString("corrupted");
            view.AccessibilityValue = value;
            view.AccessibilitySelectedText = "corrupted";
            Assert.That(edit.Text, Is.EqualTo("unchanged"));
        }
        finally { form.Close(); }
    }

    [Test]
    public void CustomEditClampsInvalidAndOversizedUtf16SelectionsWithoutOverflow()
    {
        using var form = new Form();
        using var edit = new MessageEdit { Text = "a😀z" };
        form.Controls.Add(edit);
        try
        {
            form.Show();
            Application.DoEvents();
            var view = Runtime.GetNSObject<NSView>(edit.Handle);
            view.AccessibilitySelectedTextRange = new NSRange(1, nint.MaxValue);
            Assert.That(view.AccessibilitySelectedText, Is.EqualTo("😀z"));
            view.AccessibilitySelectedTextRange = new NSRange(nint.MaxValue, nint.MaxValue);
            Assert.That(view.AccessibilitySelectedTextRange.Location, Is.EqualTo((nint)4));
            Assert.That(view.AccessibilitySelectedText, Is.Empty);
            view.AccessibilitySelectedTextRange = new NSRange(-1, 2);
            Assert.That(view.AccessibilitySelectedTextRange.Location, Is.EqualTo((nint)0));
            Assert.That(view.AccessibilitySelectedTextRange.Length, Is.EqualTo((nint)0));
        }
        finally { form.Close(); }
    }

    [Test]
    public void CustomEditMultilineRangeFramesFollowLineLayoutAndScrolling()
    {
        using var form = new Form();
        using var edit = new MessageEdit { Text = "abcd\nefgh", Size = new Size(220, 100) };
        form.Controls.Add(edit);
        try
        {
            form.Show();
            Application.DoEvents();
            var view = Runtime.GetNSObject<NSView>(edit.Handle);
            view.AccessibilitySelectedTextRange = new NSRange(6, 0);
            Assert.That(view.AccessibilityInsertionPointLineNumber, Is.EqualTo((nint)1));
            var selector = new Selector("accessibilityFrameForRange:");
            var first = FrameForRange(view.Handle, selector.Handle, new NSRange(1, 2));
            var second = FrameForRange(view.Handle, selector.Handle, new NSRange(6, 2));
            var multiline = FrameForRange(view.Handle, selector.Handle, new NSRange(1, 6));
            Assert.That((double)first.Width, Is.EqualTo(20d));
            Assert.That((double)second.X, Is.EqualTo((double)first.X));
            Assert.That((double)second.Y, Is.EqualTo((double)first.Y - MessageEdit.LineHeight));
            Assert.That((double)multiline.Height, Is.EqualTo((double)edit.Font.Height + MessageEdit.LineHeight));
            Assert.That((double)multiline.Width, Is.GreaterThanOrEqualTo(30d));
            edit.ScrollOffset = new Point(7, 12);
            var scrolled = FrameForRange(view.Handle, selector.Handle, new NSRange(6, 2));
            Assert.That((double)scrolled.X, Is.EqualTo((double)second.X - 7));
            Assert.That((double)scrolled.Y, Is.EqualTo((double)second.Y + 12));
            var endCaret = FrameForRange(view.Handle, selector.Handle, new NSRange(nint.MaxValue, nint.MaxValue));
            Assert.That((double)endCaret.X, Is.EqualTo((double)second.X + 30 - 7));
            Assert.That((double)endCaret.Y, Is.EqualTo((double)second.Y + 12));
            Assert.That((double)endCaret.Width, Is.Zero);
            Assert.That((double)endCaret.Height, Is.EqualTo((double)edit.Font.Height));
        }
        finally { form.Close(); }
    }

    [Test]
    public void PasswordCustomEditDoesNotExposeContentsThroughNativeTextProperties()
    {
        using var form = new Form();
        using var edit = new MessageEdit { Text = "secret", Password = true };
        form.Controls.Add(edit);
        try
        {
            form.Show();
            Application.DoEvents();
            var view = Runtime.GetNSObject<NSView>(edit.Handle);
            view.AccessibilitySelectedTextRange = new NSRange(0, 6);
            Assert.That(view.AccessibilityValue.ToString(), Is.Empty);
            Assert.That(view.AccessibilitySelectedText, Is.Empty);
            Assert.That(view.AccessibilityNumberOfCharacters, Is.EqualTo((nint)0));
            Assert.That(view.AccessibilitySubrole, Is.EqualTo("AXSecureTextField"));
        }
        finally { form.Close(); }
    }

    // A genuine owner-drawn EDIT consumer: the Cocoa driver routes this class name
    // to MonoEditView. Standard TextBox instead implements IMacNativeControl and
    // bypasses MonoEditView, so it cannot reproduce these accessibility bugs.
    private sealed class MessageEdit : Control
    {
        internal const int LineHeight = 20;
        private int start, end;
        public bool ReadOnly { get; set; }
        public bool Password { get; set; }
        public Point ScrollOffset { get; set; }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassName = "EDIT";
                if (Password) cp.Style |= 0x20;
                return cp;
            }
        }

        protected override AccessibleObject CreateAccessibilityInstance() => new EditAccessibleObject(this);

        private sealed class EditAccessibleObject(MessageEdit owner) : ControlAccessibleObject(owner)
        {
            public override AccessibleStates State => base.State | (owner.ReadOnly ? AccessibleStates.ReadOnly : 0);
        }

        private Point Position(int index)
        {
            int line = 0, column = 0;
            for (int i = 0; i < Math.Min(index, Text.Length); i++)
            {
                if (Text[i] == '\n') { line++; column = 0; }
                else column++;
            }
            return new Point(column * 10 - ScrollOffset.X, line * LineHeight - ScrollOffset.Y);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            for (int i = 0; i < Text.Length; i++)
                if (Text[i] != '\n')
                    TextRenderer.DrawText(e.Graphics, Password ? "*" : Text.Substring(i, 1), Font,
                        Position(i), ForeColor, TextFormatFlags.NoPadding);
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case 0x000E: m.Result = (IntPtr)Text.Length; return; // WM_GETTEXTLENGTH
                case 0x000D: // WM_GETTEXT
                    int count = Math.Min(Text.Length, Math.Max(0, (int)m.WParam - 1));
                    Marshal.Copy(Text.ToCharArray(), 0, m.LParam, count);
                    Marshal.WriteInt16(m.LParam, count * 2, 0);
                    m.Result = (IntPtr)count;
                    return;
                case 0x000C: Text = Marshal.PtrToStringUni(m.LParam) ?? ""; start = end = 0; m.Result = (IntPtr)1; return;
                case 0x00B0: Marshal.WriteInt32(m.WParam, start); Marshal.WriteInt32(m.LParam, end); return;
                case 0x00B1:
                    start = Math.Clamp(Marshal.ReadInt32(m.WParam), 0, Text.Length);
                    end = Math.Clamp(Marshal.ReadInt32(m.LParam), start, Text.Length);
                    return;
                case 0x00C2:
                    string value = Marshal.PtrToStringUni(m.LParam) ?? "";
                    Text = Text.Remove(start, end - start).Insert(start, value);
                    start += value.Length;
                    end = start;
                    return;
                case 0x00C9:
                    int line = 0;
                    for (int i = 0; i < Math.Min((int)m.WParam, Text.Length); i++)
                        if (Text[i] == '\n') line++;
                    m.Result = (IntPtr)line;
                    return;
                case 0x00D6:
                    var point = Position((int)m.LParam);
                    Marshal.WriteInt32(m.WParam, point.X);
                    Marshal.WriteInt32(m.WParam, 4, point.Y);
                    return;
                default: base.WndProc(ref m); return;
            }
        }
    }
}
