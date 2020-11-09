using System;
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
			var expectedMessage = @"Expected:
	false

Context:
	";
			Assert.LessOrEqual(expectedMessage.Length, assertion.Message.Length, assertion.Message);
			Assert.AreEqual(expectedMessage, assertion.Message.Substring(0, expectedMessage.Length), assertion.Message);
			Assert.AreNotEqual(expectedMessage.Length, assertion.Message.Length, "Expected Context, got: " + expectedMessage);
			Assert.AreEqual(expectedContext, assertion.Message.Substring(expectedMessage.Length), assertion.Message);
		}

		private static void AssertHasContext(Func<AssertEx.Builder<object>, AssertEx.Builder<object>> addContext, string expectedContext)
		{
			var builder = AssertEx.Select(new object());
			builder = addContext(builder);

			var assertion = Assert.Throws<AssertionException>(() => builder.Assert(o => false));
			var expectedMessage = @"Expected:
	false

Context:
	";
			Assert.LessOrEqual(expectedMessage.Length, assertion.Message.Length, assertion.Message);
			Assert.AreEqual(expectedMessage, assertion.Message.Substring(0, expectedMessage.Length), assertion.Message);
			Assert.AreNotEqual(expectedMessage.Length, assertion.Message.Length, "Expected Context, got: " + expectedMessage);
			Assert.AreEqual(expectedContext, assertion.Message.Substring(expectedMessage.Length), assertion.Message);
		}
	}
}
