using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Mac;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace FormsTest.Test;

[TestFixture]
[NonParallelizable]
[SingleThreaded]
public sealed class GraphicsClipAndBitmapLockTests
{
    [Test]
    public void RestoreReenablesPaintingOutsideTemporaryClip()
    {
        using var bitmap = new Bitmap(32, 16);
        using var graphics = Graphics.FromImage(bitmap);
        using var red = new SolidBrush(Color.Red);
        graphics.Clear(Color.White);
        var state = graphics.Save();
        graphics.SetClip(new Rectangle(0, 0, 8, 16));
        graphics.FillRectangle(red, 0, 0, 32, 16);
        AssertPixel(bitmap, 4, Color.Red);
        AssertPixel(bitmap, 24, Color.White);
        graphics.Restore(state);
        graphics.FillRectangle(red, 20, 0, 8, 16);
        AssertPixel(bitmap, 24, Color.Red);
    }

    [Test]
    public void RestoreReappliesEarlierClipAfterNestedState()
    {
        using var bitmap = new Bitmap(32, 16);
        using var graphics = Graphics.FromImage(bitmap);
        using var red = new SolidBrush(Color.Red);
        using var blue = new SolidBrush(Color.Blue);
        graphics.Clear(Color.White);
        graphics.SetClip(new Rectangle(0, 0, 16, 16));
        var state = graphics.Save();
        graphics.SetClip(new Rectangle(0, 0, 8, 16));
        graphics.Restore(state);
        graphics.FillRectangle(red, 12, 0, 20, 16);
        AssertPixel(bitmap, 13, Color.Red);
        AssertPixel(bitmap, 24, Color.White);
        graphics.ResetClip();
        graphics.FillRectangle(blue, 20, 0, 8, 16);
        AssertPixel(bitmap, 24, Color.Blue);
    }

    [Test]
    public void ReadOnlyLockExposesBGRAWithoutMutatingNativePixels()
    {
        using var bitmap = new Bitmap(8, 4);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.FromArgb(255, 210, 60, 20));
        var before = Pixel(bitmap, 3);
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        for (int i = 0; i < 2; i++)
        {
            var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, bitmap.PixelFormat);
            try
            {
                var bytes = new byte[4];
                Marshal.Copy(data.Scan0, bytes, 0, 4);
                Assert.That(bytes, Is.EqualTo(new byte[] { 20, 60, 210, 255 }));
                // A read-only caller must not be able to corrupt the native source.
                Marshal.WriteByte(data.Scan0, 0, 0);
            }
            finally { bitmap.UnlockBits(data); }
            Assert.That(Pixel(bitmap, 3), Is.EqualTo(before));
        }
    }

    private static byte[] Pixel(Bitmap bitmap, int x)
    {
        var context = bitmap.ToCGBitmapContext();
        context.Flush();
        var bytes = new byte[4];
        int offset = checked((int)context.BytesPerRow * (bitmap.Height / 2) + x * 4);
        Marshal.Copy(IntPtr.Add(context.Data, offset), bytes, 0, bytes.Length);
        return bytes;
    }

    private static void AssertPixel(Bitmap bitmap, int x, Color color) =>
        Assert.That(Pixel(bitmap, x), Is.EqualTo(new byte[] { color.R, color.G, color.B, color.A }));
}
