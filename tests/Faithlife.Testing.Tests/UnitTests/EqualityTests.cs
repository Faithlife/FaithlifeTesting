using System.Collections.Generic;
using NUnit.Framework;

namespace Faithlife.Testing.Tests.UnitTests
{
	[TestFixture, Ignore("Not yet implemented")]
	public sealed class EqualityTests
	{
		[Test, ExpectedMessage(
			@"Expected:
	foo == bar

Actual:
	foo = [1, 2, 3]
	bar = [1, 3, 2]")]
		public void TestArrayInequality()
		{
			int[] foo = { 1, 2, 3 };
			int[] bar = { 1, 3, 2 };

			AssertEx.IsTrue(() => foo == bar);
		}

		[Test]
		public void TestReferenceEquality()
		{
			int[] foo = { 1, 2, 3 };

			// ReSharper disable once EqualExpressionComparison
#pragma warning disable CS1718 // Comparison made to same variable
			AssertEx.IsTrue(() => foo == foo);
#pragma warning restore CS1718 // Comparison made to same variable
		}

		[Test]
		public void TestArrayEquality()
		{
			int[] foo = { 1, 2, 3 };
			int[] bar = { 1, 2, 3 };

			AssertEx.IsTrue(() => foo == bar);
		}

		[Test]
		public void TestJaggedArrayEquality()
		{
			int[][] foo = { new[] { 1, 2, 3 }, new[] { 4, 5 } };
			int[][] bar = { new[] { 1, 2, 3 }, new[] { 4, 5 } };

			AssertEx.IsTrue(() => foo == bar);
		}
		[Test]
		public void TestMultidimentionalArrayEquality()
		{
#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
			var foo = new [,] { { 1, 2, 3 }, { 4, 5, 6 } };
			var bar = new [,] { { 1, 2, 3 }, { 4, 5, 6 } };
#pragma warning restore CA1814 // Prefer jagged arrays over multidimensional

			AssertEx.IsTrue(() => foo == bar);
		}

		[Test]
		public void TestArrayAndListEqualityOperator()
		{
			IEnumerable<int> foo = new [] { 1, 2, 3 };
			IEnumerable<int> bar = new List<int> { 1, 2, 3 };

			// ReSharper disable once PossibleUnintendedReferenceComparison
			AssertEx.IsTrue(() => foo == bar);
		}

		[Test]
		public void TestArrayAndListEquality()
		{
			IEnumerable<int> foo = new[] { 1, 2, 3 };
			IEnumerable<int> bar = new List<int> { 1, 2, 3 };

			AssertEx.IsTrue(() => Equals(foo, bar));
		}

		[Test, ExpectedMessage(@"Expected:
	ReferenceEquals(foo, bar)

Actual:
	foo = [1, 2, 3]
	bar = [1, 2, 3]")]
		public void TestArrayAndListReferenceInequality()
		{
			IEnumerable<int> foo = new[] { 1, 2, 3 };
			IEnumerable<int> bar = new List<int> { 1, 2, 3 };

			AssertEx.IsTrue(() => ReferenceEquals(foo, bar));
		}
	}
}
