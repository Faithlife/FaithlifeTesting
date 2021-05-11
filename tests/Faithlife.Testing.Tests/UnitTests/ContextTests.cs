using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Faithlife.Testing.Tests.UnitTests
{
	[TestFixture]
	public sealed class ContextTests
	{
		[Test]
		public void ObjectContext()
		{
			AssertHasNoContext();

			using (AssertEx.Context(new { foo = "bar" }))
				AssertHasContext("foo = \"bar\"");

			AssertHasNoContext();
		}

		[Test]
		public void NameValueContext()
		{
			AssertHasNoContext();

			using (AssertEx.Context("foo", "bar"))
				AssertHasContext("foo = \"bar\"");

			AssertHasNoContext();
		}

		[Test]
		public void ExpressionContext()
		{
			AssertHasNoContext();

			var foo = "bar";
			using (AssertEx.Context(() => foo))
				AssertHasContext("foo = \"bar\"");

			AssertHasNoContext();
		}

		[Test]
		public void TupleContext()
		{
			AssertHasNoContext();

			using (AssertEx.Context(("foo", "bar")))
				AssertHasContext("foo = \"bar\"");

			AssertHasNoContext();
		}

		[Test]
		public void NoContext()
		{
			using (AssertEx.Context(Enumerable.Empty<(string, object)>()))
				AssertHasNoContext();
		}

		[Test]
		public void BuilderObjectContext() => AssertHasContext(b => b.Context(new { foo = "bar" }), "foo = \"bar\"");

		[Test]
		public void BuilderNameValueContext() => AssertHasContext(b => b.Context("foo", "bar"), "foo = \"bar\"");

		[Test]
		public void BuilderTupleContext() => AssertHasContext(b => b.Context(("foo", "bar")), "foo = \"bar\"");

		[Test]
		public void BuilderExpressionContext()
		{
			var foo = "bar";
			AssertHasContext(b => b.Context(() => foo), "foo = \"bar\"");
		}

		[Test]
		public void BuilderNoContext()
		{
			var builder = AssertEx.Select(new object())
				.Context(Enumerable.Empty<(string, object)>());

			var assertion = Assert.Throws<AssertionException>(() => builder.Assert(o => false));
			new ExpectedMessageAttribute(@"Expected:
	false", expectStackTrace: false)
				.AssertMessageIsExpected(assertion.Message);
		}


		[Test]
		public void TestContextCapturedVariable()
		{
			var value = 1;
			using var d = AssertEx.Context(() => value);
			AssertHasContext(@"value = 1");
		}

		[Test, ExpectedMessage(@"Expected:
	value == 2

Actual:
	value = 1")]
		public void TestContextDuplicateCapturedActualVariable()
		{
			var value = 1;
			using var d = AssertEx.Context(() => value);
			AssertEx.Assert(() => value == 2);
		}

		[Test]
		public void TestContextDuplicateCapturedContextVariable()
		{
			var value = 1;
			using var d = AssertEx.Context(() => value);
			using var e = AssertEx.Context(() => value);

			AssertHasContext(@"value = 1");
		}

		[Test]
		public void TestContextConstant()
		{
			// This is a bit silly; test is more to document the silly rather than preserve it.
			const int value = 1;
			using var d = AssertEx.Context(() => value);

			AssertHasContext(@"1 = 1");
		}

		[Test]
		public async Task TestContextAsyncLocal()
		{
			var value = 1;
			using var d = AssertEx.Context(() => value);

			await Task.WhenAll(
				Task.Run(
					() =>
					{
						var firstTask = 1;
						using var e = AssertEx.Context(() => firstTask);
						AssertHasContext(@"firstTask = 1
	value = 1");
					}),
				Task.Run(
					() =>
					{
						var secondTask = 1;
						using var e = AssertEx.Context(() => secondTask);
						AssertHasContext(@"secondTask = 1
	value = 1");
					}));

			AssertHasContext(@"value = 1");
		}

		private static void AssertHasNoContext()
		{
			var assertion = Assert.Throws<AssertionException>(() => AssertEx.Assert(() => false));
			var expectedMessage = @"Expected:
	false";
			Assert.AreEqual(expectedMessage, assertion.Message, assertion.Message);
		}

		private static void AssertHasContext(string expectedContext)
		{
			var assertion = Assert.Throws<AssertionException>(() => AssertEx.Assert(() => false));
			new ExpectedMessageAttribute(@$"Expected:
	false

Context:
	{expectedContext}", expectStackTrace: false)
				.AssertMessageIsExpected(assertion.Message);
		}

		private static void AssertHasContext(Func<AssertEx.Builder<object>, AssertEx.Builder<object>> addContext, string expectedContext)
		{
			var builder = AssertEx.Select(new object());
			builder = addContext(builder);

			var assertion = Assert.Throws<AssertionException>(() => builder.Assert(o => false));
			new ExpectedMessageAttribute(@$"Expected:
	false

Context:
	{expectedContext}", expectStackTrace: false)
				.AssertMessageIsExpected(assertion.Message);
		}
	}
}
