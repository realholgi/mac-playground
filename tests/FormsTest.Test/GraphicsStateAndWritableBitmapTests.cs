using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Mac;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace FormsTest.Test;

[TestFixture]
[NonParallelizable]
[SingleThreaded]
public sealed class GraphicsStateAndWritableBitmapTests
{
    [TestCase(ImageLockMode.ReadWrite)]
    [TestCase(ImageLockMode.WriteOnly)]
    public void WritableLockCommitsEveryBgraPixelAndSupportsReadOnlyRoundTrip(ImageLockMode mode)
    {
        using var bitmap = new Bitmap(5, 3);
        using (var graphics = Graphics.FromImage(bitmap))
            graphics.Clear(Color.Black);
        var bounds = new Rectangle(Point.Empty, bitmap.Size);
        var data = bitmap.LockBits(bounds, mode, bitmap.PixelFormat);
        try
        {
            Assert.That(data.Width, Is.EqualTo(bitmap.Width));
            Assert.That(data.Height, Is.EqualTo(bitmap.Height));
            Assert.That(data.Stride, Is.GreaterThanOrEqualTo(bitmap.Width * 4));
            for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var color = Pattern(x, y);
                var bgra = new byte[] { color.B, color.G, color.R, color.A };
                Marshal.Copy(bgra, 0, IntPtr.Add(data.Scan0, y * data.Stride + x * 4), 4);
            }
        }
        finally { bitmap.UnlockBits(data); }

        AssertPattern(bitmap);
        var read = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, bitmap.PixelFormat);
        try
        {
            for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var color = Pattern(x, y);
                var actual = new byte[4];
                Marshal.Copy(IntPtr.Add(read.Scan0, y * read.Stride + x * 4), actual, 0, 4);
                Assert.That(actual, Is.EqualTo(new byte[] { color.B, color.G, color.R, color.A }),
                    $"Readback at ({x}, {y}) after {mode}");
            }
        }
        finally { bitmap.UnlockBits(read); }
        AssertPattern(bitmap);
    }

    [Test]
    public void ReadWriteChangesOnePixelWithoutChangingAnyOtherPixel()
    {
        using var bitmap = new Bitmap(7, 5);
        using (var graphics = Graphics.FromImage(bitmap))
            graphics.Clear(Color.FromArgb(255, 210, 60, 20));
        var expected = NativeBytes(bitmap, out int stride);
        const int x = 3;
        const int y = 2;
        var data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadWrite, bitmap.PixelFormat);
        try
        {
            Marshal.Copy(new byte[] { 160, 120, 80, 255 }, 0,
                IntPtr.Add(data.Scan0, y * data.Stride + x * 4), 4);
        }
        finally { bitmap.UnlockBits(data); }

        new byte[] { 80, 120, 160, 255 }.CopyTo(expected, y * stride + x * 4);
        Assert.That(NativeBytes(bitmap, out _), Is.EqualTo(expected));
    }

    [TestCase(255)]
    [TestCase(128)]
    public void ReadOnlySnapshotsDoNotChangeNativeBytesEvenWhileLocked(int alpha)
    {
        using var bitmap = new Bitmap(7, 5);
        using (var graphics = Graphics.FromImage(bitmap))
            graphics.Clear(Color.FromArgb(alpha, 210, 60, 20));
        var before = NativeBytes(bitmap, out _);
        byte[] previousSnapshot = null;
        for (int iteration = 0; iteration < 2; iteration++)
        {
            var data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadOnly, bitmap.PixelFormat);
            try
            {
                var snapshot = new byte[data.Stride * data.Height];
                Marshal.Copy(data.Scan0, snapshot, 0, snapshot.Length);
                if (previousSnapshot != null)
                    Assert.That(snapshot, Is.EqualTo(previousSnapshot));
                previousSnapshot = snapshot;
                Assert.That(NativeBytes(bitmap, out _), Is.EqualTo(before), "Reading must not alter the live RGBA buffer.");
                int offset = data.Stride * 2 + 3 * 4;
                Marshal.Copy(new byte[] { 0, 0, 0, 0 }, 0, IntPtr.Add(data.Scan0, offset), 4);
                Assert.That(NativeBytes(bitmap, out _), Is.EqualTo(before), "The read-only snapshot must not alias the source.");
            }
            finally { bitmap.UnlockBits(data); }
            Assert.That(NativeBytes(bitmap, out _), Is.EqualTo(before));
        }
    }

    [Test]
    public void ThreeNestedSavesRestoreEachNativeClipInOrder()
    {
        using var bitmap = new Bitmap(32, 16);
        using var graphics = Graphics.FromImage(bitmap);
        using var red = new SolidBrush(Color.Red);
        using var blue = new SolidBrush(Color.Blue);
        using var green = new SolidBrush(Color.Lime);
        graphics.Clear(Color.White);
        var outer = graphics.Save();
        graphics.SetClip(new Rectangle(0, 0, 24, 16));
        var middle = graphics.Save();
        graphics.SetClip(new Rectangle(4, 0, 8, 16));
        var inner = graphics.Save();
        graphics.SetClip(new Rectangle(6, 0, 2, 16));

        graphics.Restore(inner);
        graphics.FillRectangle(red, 0, 0, 32, 16);
        Assert.That(graphics.ClipBounds, Is.EqualTo(new RectangleF(4, 0, 8, 16)));
        AssertPixel(bitmap, 5, Color.Red);
        AssertPixel(bitmap, 10, Color.Red);
        AssertPixel(bitmap, 20, Color.White);

        graphics.Restore(middle);
        graphics.FillRectangle(blue, 0, 0, 32, 16);
        Assert.That(graphics.ClipBounds, Is.EqualTo(new RectangleF(0, 0, 24, 16)));
        AssertPixel(bitmap, 20, Color.Blue);
        AssertPixel(bitmap, 28, Color.White);

        graphics.Restore(outer);
        graphics.FillRectangle(green, 26, 0, 6, 16);
        AssertPixel(bitmap, 28, Color.Lime);
        AssertPixel(bitmap, 20, Color.Blue);
    }

    [Test]
    public void ThreeNestedSavesRestoreWorldTransformAndNativeDrawing()
    {
        using var bitmap = new Bitmap(32, 16);
        using var graphics = Graphics.FromImage(bitmap);
        using var red = new SolidBrush(Color.Red);
        using var blue = new SolidBrush(Color.Blue);
        using var green = new SolidBrush(Color.Lime);
        graphics.Clear(Color.White);
        graphics.TranslateTransform(3, 0);
        var outer = graphics.Save();
        graphics.TranslateTransform(6, 0);
        var middle = graphics.Save();
        graphics.TranslateTransform(6, 0);
        var inner = graphics.Save();
        graphics.TranslateTransform(3, 0);

        graphics.Restore(inner);
        graphics.FillRectangle(red, 0, 0, 4, 16);
        AssertPixel(bitmap, 17, Color.Red);
        graphics.Restore(middle);
        graphics.FillRectangle(blue, 0, 0, 4, 16);
        AssertPixel(bitmap, 11, Color.Blue);
        graphics.Restore(outer);
        graphics.FillRectangle(green, 0, 0, 4, 16);
        AssertPixel(bitmap, 5, Color.Lime);
        AssertPixel(bitmap, 11, Color.Blue);
        AssertPixel(bitmap, 17, Color.Red);
        using var transform = graphics.Transform;
        Assert.That(transform.Elements, Is.EqualTo(new[] { 1f, 0f, 0f, 1f, 3f, 0f }));
    }

    private static Color Pattern(int x, int y) => Color.FromArgb(255, 40 + x * 20, 90 + y * 20, 200 - x * 15);

    private static void AssertPattern(Bitmap bitmap)
    {
        var bytes = NativeBytes(bitmap, out int stride);
        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
        {
            var color = Pattern(x, y);
            var actual = new byte[4];
            Array.Copy(bytes, y * stride + x * 4, actual, 0, 4);
            Assert.That(actual, Is.EqualTo(new byte[] { color.R, color.G, color.B, color.A }), $"Native pixel ({x}, {y})");
        }
    }

    private static byte[] NativeBytes(Bitmap bitmap, out int stride)
    {
        var context = bitmap.ToCGBitmapContext();
        context.Flush();
        stride = checked((int)context.BytesPerRow);
        var bytes = new byte[checked(stride * bitmap.Height)];
        Marshal.Copy(context.Data, bytes, 0, bytes.Length);
        return bytes;
    }

    private static void AssertPixel(Bitmap bitmap, int x, Color color)
    {
        var bytes = NativeBytes(bitmap, out int stride);
        var actual = new byte[4];
        Array.Copy(bytes, (bitmap.Height / 2) * stride + x * 4, actual, 0, 4);
        Assert.That(actual, Is.EqualTo(new byte[] { color.R, color.G, color.B, color.A }));
    }
}
