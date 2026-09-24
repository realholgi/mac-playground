using System;
using System.Drawing;
using System.Windows.Forms;

using var container = new SplitContainer { Size = new Size(400, 240), SplitterDistance = 200 };
using var label = new Label
{
    Location = new Point(8, 20),
    Size = new Size(180, 60),
    Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
};
container.Panel1.Controls.Add(label);
var initialPanelWidth = container.Panel1.Width;
var initialLabelWidth = label.Width;
var initialPanelHeight = container.Panel1.Height;
var initialLabelHeight = label.Height;

container.Size = new Size(800, 340);
AssertResize(800, 340);
container.Size = new Size(400, 240);
AssertResize(400, 240);
Console.WriteLine("Anchored label follows SplitContainer panel resizing in both directions.");

void AssertResize(int width, int height)
{
    var expectedWidth = initialLabelWidth + container.Panel1.Width - initialPanelWidth;
    if (label.Width != expectedWidth)
        throw new Exception($"At {width}x{height}, label width was {label.Width}; expected {expectedWidth}.");
    var expectedHeight = initialLabelHeight + container.Panel1.Height - initialPanelHeight;
    if (label.Height != expectedHeight)
        throw new Exception($"At {width}x{height}, label height was {label.Height}; expected {expectedHeight}.");
}
