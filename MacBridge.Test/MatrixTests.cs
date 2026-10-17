using System.Drawing;
using System.Drawing.Drawing2D;
using NUnit.Framework;

namespace MacBridge.Test
{
	[TestFixture]
	public class MatrixTests
	{
		[TestCase(1f, 0f, 0f, 1f, 0f, 0f, true, TestName = "IsInvertible_Identity")]
		[TestCase(-1f, 0f, 0f, 1f, 0f, 0f, true, TestName = "IsInvertible_Reflection")]
		[TestCase(-1f, 0f, 0f, -1f, 10f, 20f, true, TestName = "IsInvertible_TranslatedSelfInverse")]
		[TestCase(1f, 2f, 2f, 4f, 10f, 20f, false, TestName = "IsInvertible_DependentRows")]
		[TestCase(0f, 0f, 0f, 1f, 10f, 20f, false, TestName = "IsInvertible_ZeroScale")]
		[TestCase(1e-30f, 0f, 0f, 1e-30f, 0f, 0f, true, TestName = "IsInvertible_SmallNonzeroScales")]
		[TestCase(float.NaN, 0f, 0f, 1f, 0f, 0f, false, TestName = "IsInvertible_NonFiniteDeterminant")]
		public void IsInvertible_ChecksDeterminant(float m11, float m12, float m21,
			float m22, float dx, float dy, bool expected)
		{
			using var matrix = new Matrix(m11, m12, m21, m22, dx, dy);
			var original = matrix.Elements;

			Assert.That(matrix.IsInvertible, Is.EqualTo(expected));
			Assert.That(matrix.Elements, Is.EqualTo(original), "Querying invertibility must not change the matrix.");
		}

		[Test]
		public void Invert_RestoresPointAfterAffineTransformation()
		{
			using var matrix = new Matrix(1.5f, 0.25f, -0.75f, 2f, 10f, -6f);
			var points = new[] { new PointF(3.2f, -4.7f) };

			matrix.TransformPoints(points);

			Assert.That(points[0].X, Is.EqualTo(18.325f).Within(0.00001f));
			Assert.That(points[0].Y, Is.EqualTo(-14.6f).Within(0.00001f));

			matrix.Invert();
			matrix.TransformPoints(points);

			Assert.That(points[0].X, Is.EqualTo(3.2f).Within(0.00001f));
			Assert.That(points[0].Y, Is.EqualTo(-4.7f).Within(0.00001f));
		}
	}
}
