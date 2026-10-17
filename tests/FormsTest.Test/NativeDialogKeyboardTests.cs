using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using AppKit;
using CoreGraphics;
using NUnit.Framework;

namespace FormsTest.Test;

[TestFixture]
[NonParallelizable]
[SingleThreaded]
public sealed class NativeDialogKeyboardTests
{
    [TestCase(false, "\r")]
    [TestCase(false, "\u001b")]
    [TestCase(true, "\r")]
    [TestCase(true, "\u001b")]
    public void NativeKeyEquivalentActivatesTheIntendedButtonExactlyOnce(bool shared, string key)
    {
        using var dialog = new Dialog(shared);
        dialog.Show();
        var target = shared || key == "\r" ? dialog.Accept : dialog.Cancel;
        Assert.That(Dispatch(target, key), Is.True);
        Application.DoEvents();
        Assert.That(dialog.AcceptClicks, Is.EqualTo(shared || key == "\r" ? 1 : 0));
        Assert.That(dialog.CancelClicks, Is.EqualTo(!shared && key == "\u001b" ? 1 : 0));
    }

    [TestCase("\r")]
    [TestCase("\u001b")]
    public void DisabledSharedButtonIgnoresNativeKeyAndWorksAfterReenabling(string key)
    {
        using var dialog = new Dialog(true);
        dialog.Show();
        dialog.Accept.Enabled = false;
        Assert.That(Dispatch(dialog.Accept, key), Is.False);
        Application.DoEvents();
        Assert.That(dialog.AcceptClicks, Is.Zero);
        dialog.Accept.Enabled = true;
        Assert.That(Dispatch(dialog.Accept, key), Is.True);
        Application.DoEvents();
        Assert.That(dialog.AcceptClicks, Is.EqualTo(1));
    }

    [TestCase(NSEventModifierMask.CommandKeyMask)]
    [TestCase(NSEventModifierMask.ControlKeyMask)]
    [TestCase(NSEventModifierMask.AlternateKeyMask)]
    public void ModifiedReturnDoesNotUseSharedButtonsUnmodifiedReturnShortcut(NSEventModifierMask modifier)
    {
        using var dialog = new Dialog(true);
        dialog.Show();
        Assert.That(Dispatch(dialog.Accept, "\r", modifier), Is.False);
        Application.DoEvents();
        Assert.That(dialog.AcceptClicks, Is.Zero);
    }

    [Test]
    public void RemovingSharedRolesUpdatesActualNativeKeyboardActivation()
    {
        using var dialog = new Dialog(true);
        dialog.Show();
        dialog.Form.CancelButton = null;
        Assert.That(Dispatch(dialog.Accept, "\r"), Is.True);
        Assert.That(Dispatch(dialog.Accept, "\u001b"), Is.False);
        Assert.That(dialog.AcceptClicks, Is.EqualTo(1));

        dialog.Form.CancelButton = dialog.Accept;
        dialog.Accept.DialogResult = DialogResult.None;
        dialog.Form.AcceptButton = null;
        Assert.That(Dispatch(dialog.Accept, "\r"), Is.False);
        Assert.That(Dispatch(dialog.Accept, "\u001b"), Is.True);
        Assert.That(dialog.AcceptClicks, Is.EqualTo(2));

        dialog.Form.CancelButton = null;
        Assert.That(Dispatch(dialog.Accept, "\r"), Is.False);
        Assert.That(Dispatch(dialog.Accept, "\u001b"), Is.False);
        Application.DoEvents();
        Assert.That(dialog.AcceptClicks, Is.EqualTo(2));
    }

    private static bool Dispatch(Button button, string key, NSEventModifierMask modifiers = 0)
    {
        var property = typeof(Button).GetProperty("NativeButton", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(property, Is.Not.Null);
        var native = (NSButton)property.GetValue(button);
        Assert.That(native.Window, Is.Not.Null, "The native button must be attached to the displayed window.");
        using var keyEvent = NSEvent.KeyEvent(NSEventType.KeyDown, CGPoint.Empty, modifiers,
            0, native.Window.WindowNumber, null, key, key, false, key == "\r" ? (ushort)36 : (ushort)53);
        // Deliberately use AppKit's key-equivalent path, not managed Button.PerformClick.
        return native.PerformKeyEquivalent(keyEvent);
    }

    private sealed class Dialog : IDisposable
    {
        public Form Form { get; } = new Form { ClientSize = new Size(320, 140) };
        public TextBox Editor { get; } = new TextBox { Text = "Focus stays in the editor", Location = new Point(10, 10), Width = 270 };
        public Button Accept { get; } = new Button { Text = "Accept", Location = new Point(10, 65), Size = new Size(110, 30) };
        public Button Cancel { get; } = new Button { Text = "Cancel", Location = new Point(155, 65), Size = new Size(110, 30) };
        public int AcceptClicks { get; private set; }
        public int CancelClicks { get; private set; }

        public Dialog(bool shared)
        {
            Form.Controls.Add(Editor);
            Form.Controls.Add(Accept);
            Form.Controls.Add(Cancel);
            Form.AcceptButton = Accept;
            Form.CancelButton = shared ? Accept : Cancel;
            // Observe repeated activations without closing the modeless test window.
            Accept.DialogResult = DialogResult.None;
            Cancel.DialogResult = DialogResult.None;
            Accept.Click += (_, _) => AcceptClicks++;
            Cancel.Click += (_, _) => CancelClicks++;
        }

        public void Show()
        {
            Form.Show();
            NSApplication.SharedApplication.Activate();
            Form.Activate();
            Editor.Focus();
            Assert.That(() =>
            {
                Application.DoEvents();
                return Form.Visible && Editor.Focused;
            }, Is.True.After(2000, 10));
        }

        public void Dispose()
        {
            Form.Close();
            Form.Dispose();
            Application.DoEvents();
        }
    }
}
