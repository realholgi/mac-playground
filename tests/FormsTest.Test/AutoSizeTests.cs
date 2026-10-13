using System.Linq;
using System.Windows.Forms;
using NUnit.Framework;

namespace FormsTest.Test;

[TestFixture]
[NonParallelizable]
[SingleThreaded]
public class AutoSizeTests
{
    [Test]
    public void DirectionButtonReflowsControlsAndRestoresTheOriginalLayout()
    {
        using var form = new AutoSizeForm();
        try
        {
            form.Show();
            Application.DoEvents();
            var panel = form.Controls.OfType<FlowLayoutPanel>().Single();
            panel.WrapContents = false;
            Application.DoEvents();
            var label = (Label)panel.Controls.Find("label", false).Single();
            var textBox = (TextBox)panel.Controls.Find("textBox", false).Single();
            var button = (Button)panel.Controls.Find("button", false).Single();
            var initialBounds = new[] { label.Bounds, textBox.Bounds, button.Bounds };
            var initialPanelSize = panel.Size;

            button.PerformClick();
            Application.DoEvents();

            Assert.That(panel.FlowDirection, Is.EqualTo(FlowDirection.TopDown));
            Assert.That(label.Bottom, Is.LessThanOrEqualTo(textBox.Top));
            Assert.That(textBox.Bottom, Is.LessThanOrEqualTo(button.Top));
            Assert.That(panel.Height, Is.GreaterThan(initialPanelSize.Height));
            Assert.That(panel.Width, Is.LessThan(initialPanelSize.Width));
            foreach (Control control in panel.Controls)
                Assert.That(panel.ClientRectangle.Contains(control.Bounds), Is.True);

            button.PerformClick();
            Application.DoEvents();

            Assert.That(panel.FlowDirection, Is.EqualTo(FlowDirection.LeftToRight));
            Assert.That(new[] { label.Bounds, textBox.Bounds, button.Bounds }, Is.EqualTo(initialBounds));
            Assert.That(panel.Size, Is.EqualTo(initialPanelSize));
        }
        finally
        {
            form.Close();
        }
    }
}
