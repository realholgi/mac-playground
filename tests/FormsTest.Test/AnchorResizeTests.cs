using System.Drawing;
using System.Windows.Forms;
using NUnit.Framework;

namespace FormsTest.Test;

[TestFixture]
[NonParallelizable]
[SingleThreaded]
public class AnchorResizeTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void AnchoredLabelFollowsPanelGrowthAndShrinkage(bool suspendLayout)
    {
        using var container = new SplitContainer { Size = new Size(400, 240), SplitterDistance = 200 };
        using var label = new Label
        {
            Location = new Point(8, 20),
            Size = new Size(180, 60),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        container.Panel1.Controls.Add(label);
        var initialPanelSize = container.Panel1.Size;
        var initialLabelBounds = label.Bounds;

        foreach (var size in new[] { new Size(800, 340), new Size(400, 240) })
        {
            if (suspendLayout)
                container.Panel1.SuspendLayout();
            try
            {
                container.Size = size;
            }
            finally
            {
                if (suspendLayout)
                    container.Panel1.ResumeLayout(true);
            }

            Assert.That(label.Bounds, Is.EqualTo(new Rectangle(
                initialLabelBounds.Location,
                new Size(
                    initialLabelBounds.Width + container.Panel1.Width - initialPanelSize.Width,
                    initialLabelBounds.Height + container.Panel1.Height - initialPanelSize.Height))),
                $"Container resized to {size}");
        }
    }
}
