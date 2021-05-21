using NUnit.Framework;

namespace Faithlife.Testing.Tests.UnitTests
{
	[TestFixture]
	public sealed class AssertableTests
	{
		[Test]
		public void IsTrueSuccess()
		{
			AssertEx.HasValue("foo")
				.IsTrue(v => v == "foo");
		}

		[Test]
		public void IsTrueExpressionSuccess()
		{
			AssertEx.HasValue(() => "foo")
				.IsTrue(v => v == "foo");
		}

		[Test]
		public void DoesNotThrowSuccess()
		{
			AssertEx.HasValue("foo")
				.DoesNotThrow(v => NoOp());
		}

		[Test]
		public void HasValueEnumSuccess()
		{
			AssertEx.HasValue("foo")
				.HasValue(v => (int?) v.Length);
		}

		private static void NoOp()
		{
		}

		[Test, ExpectedMessage(@"Expected:
	foo

Actual:
	foo = null")]
		public void AssertMultipleNoop()
		{
			Assert.Multiple(() =>
			{
				string foo = null;
				AssertEx.HasValue(() => foo)
					.IsTrue(a => a.Length == 5);
			});
		}

		[Test, ExpectedMessage(@"Multiple failures or warnings in test:
  1) Expected:
	foo.Length == 5

Actual:
	foo.Length = 3
  2) Expected:
	foo.Length == 4

Actual:
	foo.Length = 3
")]
		public void AssertMultiple()
		{
			Assert.Multiple(() =>
			{
				var foo = "bar";
				AssertEx.HasValue(() => foo)
					.IsTrue(a => a.Length == 5)
					.IsTrue(a => a.Length == 4);
			});
		}

		[Test]
		public void TestReuseOverCapturedVariable()
		{
			var fooBar = "foo";

			var assertion = AssertEx.HasValue(() => fooBar)
				.IsTrue(a => a == "foo");

			// NOTE: the `.IsTrue(a => a == "foo")` expression above is **not** re-evaluated.
			fooBar = "bar";

			assertion.IsTrue(a => a == "bar");
		}

		[Test, ExpectedMessage(@"Expected:
	foo != null

Actual:
	foo = null")]
		public void TestHasValue()
		{
			AssertEx.HasValue<string>(null, "foo");
		}
	}
}
