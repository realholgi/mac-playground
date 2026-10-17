using System;
using System.Drawing;
using System.Reflection;
using AppKit;
using NUnit.Framework;
using System.Windows.Forms;

namespace FormsTest.Test;

[TestFixture]
[NonParallelizable]
[SingleThreaded]
public sealed class NativeDialogButtonTests
{
    [Test]
    public void SeparateAcceptAndCancelButtonsHaveCorrectNativeKeys()
    {
        using var form = new Form { ClientSize = new Size(300, 120) };
        using var accept = new Button { Text = "OK" };
        using var cancel = new Button { Text = "Cancel" };
        form.Controls.Add(accept);
        form.Controls.Add(cancel);
        form.AcceptButton = accept;
        form.CancelButton = cancel;
        try
        {
            form.Show();
            Application.DoEvents();
            Assert.That(Native(accept).KeyEquivalent, Is.EqualTo("\r"));
            Assert.That(Native(cancel).KeyEquivalent, Is.EqualTo("\u001b"));
        }
        finally { form.Close(); }
    }

    [Test]
    public void SharedAcceptAndCancelButtonPrefersEscapeKeyEquivalent()
    {
        using var form = new Form { ClientSize = new Size(300, 120) };
        using var shared = new Button { Text = "Shared" };
        form.Controls.Add(shared);
        form.AcceptButton = shared;
        form.CancelButton = shared;
        try
        {
            form.Show();
            Application.DoEvents();
            Assert.That(Native(shared).KeyEquivalent, Is.EqualTo("\u001b"));
        }
        finally { form.Close(); }
    }

    [Test]
    public void ReplacingAndRemovingAcceptButtonUpdatesNativeKeys()
    {
        using var form = new Form { ClientSize = new Size(300, 120) };
        using var first = new Button { Text = "First" };
        using var second = new Button { Text = "Second" };
        form.Controls.Add(first);
        form.Controls.Add(second);
        try
        {
            form.Show();
            Application.DoEvents();
            form.AcceptButton = first;
            Assert.That(Native(first).KeyEquivalent, Is.EqualTo("\r"));
            form.AcceptButton = second;
            Assert.That(Native(first).KeyEquivalent, Is.Empty);
            Assert.That(Native(second).KeyEquivalent, Is.EqualTo("\r"));
            form.AcceptButton = null;
            Assert.That(Native(second).KeyEquivalent, Is.Empty);
        }
        finally { form.Close(); }
    }

    [Test]
    public void ReplacingAndRemovingCancelButtonUpdatesNativeKeys()
    {
        using var form = new Form { ClientSize = new Size(300, 120) };
        using var first = new Button { Text = "First" };
        using var second = new Button { Text = "Second" };
        form.Controls.Add(first);
        form.Controls.Add(second);
        try
        {
            form.Show();
            Application.DoEvents();
            form.CancelButton = first;
            Assert.That(Native(first).KeyEquivalent, Is.EqualTo("\u001b"));
            form.CancelButton = second;
            Assert.That(Native(first).KeyEquivalent, Is.Empty);
            Assert.That(Native(second).KeyEquivalent, Is.EqualTo("\u001b"));
            form.CancelButton = null;
            Assert.That(Native(second).KeyEquivalent, Is.Empty);
        }
        finally { form.Close(); }
    }

    [Test]
    public void NativeEnabledStateTracksManagedEnabledState()
    {
        using var form = new Form { ClientSize = new Size(300, 120) };
        using var button = new Button { Text = "OK" };
        form.Controls.Add(button);
        form.AcceptButton = button;
        try
        {
            form.Show();
            Application.DoEvents();
            Assert.That(Native(button).Enabled, Is.True);
            button.Enabled = false;
            Assert.That(Native(button).Enabled, Is.False);
            button.Enabled = true;
            Assert.That(Native(button).Enabled, Is.True);
        }
        finally { form.Close(); }
    }

    private static NSButton Native(Button button)
    {
        var property = typeof(Button).GetProperty("NativeButton", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(property, Is.Not.Null);
        return (NSButton)property.GetValue(button);
    }
}
