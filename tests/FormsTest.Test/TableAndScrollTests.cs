using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using NUnit.Framework;

namespace FormsTest.Test;

[TestFixture]
[NonParallelizable]
[SingleThreaded]
public sealed class TableAndScrollTests
{
    [Test]
    public void TwoRowPanelRemainsContainedWhenItsHeightChanges()
    {
        using var form = new TablesForm();
        try
        {
            form.Show();
            Application.DoEvents();

            var main = form.Controls.OfType<Panel>().Single();
            var left = main.Controls.OfType<FlowLayoutPanel>().Single(panel => panel.Name == "leftPanel");
            var table = left.Controls.OfType<TableLayoutPanel>().Single();
            var square = table.Controls.OfType<Panel>().Single();
            var initialSize = square.Size;
            var initialTableHeight = table.Height;

            AssertSpanningLayout(table, square);
            square.Height = initialSize.Height * 2;
            Application.DoEvents();

            Assert.That(square.Height, Is.EqualTo(initialSize.Height * 2));
            Assert.That(table.Height, Is.GreaterThan(initialTableHeight), "The table must accommodate the taller spanning panel.");
            AssertSpanningLayout(table, square);
            Assert.That(left.ClientRectangle.Contains(table.Bounds), Is.True, "The expanded table must remain inside its flow panel.");

            square.Size = initialSize;
            Application.DoEvents();

            Assert.That(table.Height, Is.EqualTo(initialTableHeight), "GrowAndShrink must release the extra height.");
            AssertSpanningLayout(table, square);
        }
        finally
        {
            form.Close();
        }
    }

    [Test]
    public void DockedPanelsFollowFormGrowthAndShrinkageWithoutOverlappingTheTable()
    {
        using var form = new TablesForm();
        try
        {
            form.Show();
            Application.DoEvents();

            var main = form.Controls.OfType<Panel>().Single();
            var left = main.Controls.OfType<FlowLayoutPanel>().Single(panel => panel.Name == "leftPanel");
            var right = main.Controls.OfType<FlowLayoutPanel>().Single(panel => panel.Name == "rightPanel");
            var table = left.Controls.OfType<TableLayoutPanel>().Single();
            var square = table.Controls.OfType<Panel>().Single();
            var initialClientSize = form.ClientSize;
            var initialMainSize = main.Size;
            var initialLeftHeight = left.Height;
            var initialRightHeight = right.Height;
            var initialTableSize = table.Size;

            foreach (var targetSize in new[]
                     {
                         new Size(initialClientSize.Width + 200, initialClientSize.Height + 120),
                         initialClientSize
                     })
            {
                form.ClientSize = targetSize;
                Application.DoEvents();

                var heightChange = targetSize.Height - initialClientSize.Height;
                Assert.That(main.Width, Is.EqualTo(initialMainSize.Width + targetSize.Width - initialClientSize.Width));
                Assert.That(main.Height, Is.EqualTo(initialMainSize.Height + heightChange));
                Assert.That(left.Height, Is.EqualTo(initialLeftHeight + heightChange));
                Assert.That(right.Height, Is.EqualTo(initialRightHeight + heightChange));
                Assert.That(main.ClientRectangle.Contains(left.Bounds), Is.True);
                Assert.That(main.ClientRectangle.Contains(right.Bounds), Is.True);
                Assert.That(left.Bounds.IntersectsWith(right.Bounds), Is.False);
                Assert.That(left.ClientRectangle.Contains(table.Bounds), Is.True);
                Assert.That(table.Size, Is.EqualTo(initialTableSize), "Absolute columns and autosized rows should not stretch with the form.");
                AssertSpanningLayout(table, square);
            }
        }
        finally
        {
            form.Close();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TextInputChangesOnlyTheSelectedScrollbarAndNotifiesOnlyForNewValues(bool horizontal)
    {
        using var form = new ScrollBarsForm();
        try
        {
            form.Show();
            Application.DoEvents();

            var main = form.Controls.OfType<Panel>().Single();
            var verticalBar = main.Controls.OfType<VScrollBar>().Single();
            var horizontalBar = main.Controls.OfType<HScrollBar>().Single();
            var textBoxes = main.Controls.OfType<TextBox>().ToArray();
            var labels = main.Controls.OfType<Label>().ToArray();
            // The demo adds the vertical input/label before the horizontal input/label.
            var input = textBoxes[horizontal ? 1 : 0];
            var label = labels[horizontal ? 1 : 0];
            var otherLabel = labels[horizontal ? 0 : 1];
            ScrollBar bar = horizontal ? horizontalBar : verticalBar;
            ScrollBar otherBar = horizontal ? verticalBar : horizontalBar;
            var otherValue = otherBar.Value;
            var otherLabelText = otherLabel.Text;
            var notifications = new List<int>();
            bar.ValueChanged += (_, _) => notifications.Add(bar.Value);

            var midpoint = bar.Minimum + (bar.Maximum - bar.Minimum) / 2;
            foreach (var value in new[] { midpoint, bar.Minimum, bar.Maximum, midpoint })
            {
                var oldCount = notifications.Count;
                input.Text = value.ToString(CultureInfo.InvariantCulture);
                Application.DoEvents();

                Assert.That(bar.Value, Is.EqualTo(value));
                Assert.That(notifications.Count, Is.EqualTo(oldCount + 1));
                Assert.That(notifications.Last(), Is.EqualTo(value));
                Assert.That(otherBar.Value, Is.EqualTo(otherValue));
                Assert.That(otherLabel.Text, Is.EqualTo(otherLabelText));
            }

            var retainedValue = bar.Value;
            var retainedLabel = label.Text;
            var retainedCount = notifications.Count;
            foreach (var text in new[]
                     {
                         "", "not-a-number", "2147483648",
                         (bar.Minimum - 1).ToString(CultureInfo.InvariantCulture),
                         (bar.Maximum + 1).ToString(CultureInfo.InvariantCulture),
                         " +" + retainedValue.ToString(CultureInfo.InvariantCulture)
                     })
            {
                input.Text = text;
                Application.DoEvents();

                Assert.That(bar.Value, Is.EqualTo(retainedValue), $"Input '{text}' must retain the current value.");
                Assert.That(notifications.Count, Is.EqualTo(retainedCount), "Rejected input and equivalent values must not notify.");
                Assert.That(label.Text, Is.EqualTo(retainedLabel));
                Assert.That(otherBar.Value, Is.EqualTo(otherValue));
                Assert.That(otherLabel.Text, Is.EqualTo(otherLabelText));
            }

            input.Text = bar.Minimum.ToString(CultureInfo.InvariantCulture);
            Application.DoEvents();
            Assert.That(bar.Value, Is.EqualTo(bar.Minimum), "Valid input must still work after rejected input.");
            Assert.That(notifications.Count, Is.EqualTo(retainedCount + 1));
            Assert.That(notifications.Last(), Is.EqualTo(bar.Minimum));
        }
        finally
        {
            form.Close();
        }
    }

    private static void AssertSpanningLayout(TableLayoutPanel table, Panel square)
    {
        var firstLabel = (Label)table.GetControlFromPosition(0, 0)!;
        var secondLabel = (Label)table.GetControlFromPosition(0, 1)!;

        Assert.That(table.ClientRectangle.Contains(square.Bounds), Is.True);
        Assert.That(table.ClientRectangle.Contains(firstLabel.Bounds), Is.True);
        Assert.That(table.ClientRectangle.Contains(secondLabel.Bounds), Is.True);
        Assert.That(firstLabel.Bottom, Is.LessThanOrEqualTo(secondLabel.Top), "Labels in successive rows must not overlap.");
        Assert.That(square.Bounds.IntersectsWith(firstLabel.Bounds), Is.False);
        Assert.That(square.Bounds.IntersectsWith(secondLabel.Bounds), Is.False);
        Assert.That(square.Left, Is.GreaterThanOrEqualTo(Math.Max(firstLabel.Right, secondLabel.Right)));
        Assert.That(square.Top, Is.LessThan(secondLabel.Top), "The panel must begin above the second row.");
        Assert.That(square.Bottom, Is.GreaterThan(secondLabel.Top), "The panel must physically extend into the second row.");
    }
}
