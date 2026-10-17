using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Mac;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace FormsTest.Test
{
	[TestFixture]
	[NonParallelizable]
	[SingleThreaded]
	public class GraphicsTransformTests
	{
		[Test]
		public void Transform_Get_ReturnsIndependentCopies()
		{
			using var bitmap = new Bitmap(32, 32);
			using var graphics = Graphics.FromImage(bitmap);
			graphics.TranslateTransform(10, 20);

			using var first = graphics.Transform;
			using var second = graphics.Transform;
			Assert.That(first, Is.Not.SameAs(second));
			first.Reset();
			Assert.That(second.Elements, Is.EqualTo(new[] { 1f, 0f, 0f, 1f, 10f, 20f }));
			AssertTransform(graphics, 1, 0, 0, 1, 10, 20);

			graphics.TranslateTransform(3, 4);
			Assert.That(second.Elements, Is.EqualTo(new[] { 1f, 0f, 0f, 1f, 10f, 20f }),
				"Later graphics changes must not modify an earlier snapshot.");
		}

		[Test]
		public void Transform_Set_CopiesInput()
		{
			using var bitmap = new Bitmap(32, 32);
			using var graphics = Graphics.FromImage(bitmap);
			using (var input = new Matrix(2, 0, 0, 3, 10, 20)) {
				graphics.Transform = input;
				input.Reset();
				AssertTransform(graphics, 2, 0, 0, 3, 10, 20);

				graphics.TranslateTransform(5, 7, MatrixOrder.Append);
				Assert.That(input.IsIdentity, Is.True, "Graphics must not retain the caller's matrix.");
			}

			AssertTransform(graphics, 2, 0, 0, 3, 15, 27);
		}

		[TestCase(MatrixOrder.Append, 20f, 60f, 22f, 63f)]
		[TestCase(MatrixOrder.Prepend, 10f, 20f, 12f, 23f)]
		public void MultiplyTransform_UpdatesTransformAndCoordinates(MatrixOrder order,
			float offsetX, float offsetY, float pointX, float pointY)
		{
			using var bitmap = new Bitmap(64, 96);
			using var graphics = Graphics.FromImage(bitmap);
			using var scale = new Matrix(2, 0, 0, 3, 0, 0);
			graphics.TranslateTransform(10, 20);

			graphics.MultiplyTransform(scale, order);

			// Append: translate then scale. Prepend: scale then translate.
			AssertTransform(graphics, 2, 0, 0, 3, offsetX, offsetY);
			var points = new[] { new PointF(1, 1) };
			graphics.TransformPoints(CoordinateSpace.Page, CoordinateSpace.World, points);
			Assert.That(points[0], Is.EqualTo(new PointF(pointX, pointY)));
			Assert.That(scale.Elements, Is.EqualTo(new[] { 2f, 0f, 0f, 3f, 0f, 0f }),
				"Multiplication must not change the supplied matrix.");
		}

		[Test]
		public void MultiplyTransform_DefaultOrderIsPrepend()
		{
			using var bitmap = new Bitmap(32, 32);
			using var graphics = Graphics.FromImage(bitmap);
			using var scale = new Matrix(2, 0, 0, 3, 0, 0);
			graphics.TranslateTransform(10, 20);

			graphics.MultiplyTransform(scale);

			AssertTransform(graphics, 2, 0, 0, 3, 10, 20);
		}

		[Test]
		public void TransformOperations_RejectNullWithoutChangingTransform()
		{
			using var bitmap = new Bitmap(32, 32);
			using var graphics = Graphics.FromImage(bitmap);
			graphics.TranslateTransform(10, 20);

			var setterError = Assert.Throws<ArgumentNullException>(() => graphics.Transform = null);
			Assert.That(setterError.ParamName, Is.EqualTo("value"));
			var defaultError = Assert.Throws<ArgumentNullException>(() => graphics.MultiplyTransform(null));
			Assert.That(defaultError.ParamName, Is.EqualTo("matrix"));
			var orderedError = Assert.Throws<ArgumentNullException>(() => graphics.MultiplyTransform(null, MatrixOrder.Append));
			Assert.That(orderedError.ParamName, Is.EqualTo("matrix"));
			AssertTransform(graphics, 1, 0, 0, 1, 10, 20);
		}

		[Test]
		public void TransformPoints_ReflectionRoundTrip_ReturnsOriginalWorldPoint()
		{
			using var bitmap = new Bitmap(32, 32);
			using var graphics = Graphics.FromImage(bitmap);
			using var reflection = new Matrix(-1, 0, 0, 1, 10, 0);
			graphics.Transform = reflection;
			var points = new[] { new PointF(3, 4) };

			graphics.TransformPoints(CoordinateSpace.Page, CoordinateSpace.World, points);
			Assert.That(points[0], Is.EqualTo(new PointF(7, 4)));
			graphics.TransformPoints(CoordinateSpace.World, CoordinateSpace.Page, points);
			Assert.That(points[0], Is.EqualTo(new PointF(3, 4)));
		}

		[TestCase(MatrixOrder.Append, 28, 18)]
		[TestCase(MatrixOrder.Prepend, 18, 28)]
		public void MultiplyTransform_DrawThenTranslateAndReset_UpdatesNativeContext(MatrixOrder order,
			int paintedX, int unpaintedX)
		{
			using var bitmap = new Bitmap(64, 32);
			using var graphics = Graphics.FromImage(bitmap);
			using var scale = new Matrix(2, 0, 0, 1, 0, 0);
			using var red = new SolidBrush(Color.Red);
			using var blue = new SolidBrush(Color.Blue);
			using var green = new SolidBrush(Color.Lime);
			graphics.SmoothingMode = SmoothingMode.None;
			graphics.Clear(Color.White);
			graphics.TranslateTransform(10, 0);
			graphics.MultiplyTransform(scale, order);

			// Full-height strips and interior pixels isolate transforms from bitmap Y orientation.
			graphics.FillRectangle(red, 2, 0, 4, bitmap.Height);
			AssertMiddlePixel(bitmap, paintedX, Color.Red);
			AssertMiddlePixel(bitmap, unpaintedX, Color.White);
			AssertMiddlePixel(bitmap, 4, Color.White);

			graphics.TranslateTransform(3, 0, MatrixOrder.Append);
			graphics.FillRectangle(blue, 2, 0, 4, bitmap.Height);
			AssertMiddlePixel(bitmap, paintedX + 3, Color.Blue);

			graphics.ResetTransform();
			graphics.FillRectangle(green, 2, 0, 4, bitmap.Height);
			AssertTransform(graphics, 1, 0, 0, 1, 0, 0);
			AssertMiddlePixel(bitmap, 4, Color.Lime);
		}

		private static void AssertTransform(Graphics graphics, params float[] expected)
		{
			using var transform = graphics.Transform;
			Assert.That(transform.Elements, Is.EqualTo(expected).Within(0.00001f));
		}

		private static void AssertMiddlePixel(Bitmap bitmap, int x, Color expected)
		{
			// The context is borrowed from the bitmap. Read its native RGBA buffer without
			// invoking Bitmap.GetPixel or LockBits, whose behavior is outside this test suite.
			var context = bitmap.ToCGBitmapContext();
			context.Flush();
			int offset = checked((int)context.BytesPerRow * (bitmap.Height / 2) + x * 4);
			var actual = new byte[4];
			Marshal.Copy(IntPtr.Add(context.Data, offset), actual, 0, actual.Length);
			Assert.That(actual, Is.EqualTo(new[] { expected.R, expected.G, expected.B, expected.A }),
				$"Native pixel at x={x}, middle row");
		}
	}
}
