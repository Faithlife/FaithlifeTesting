using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Faithlife.Testing.Tests.UnitTests
{
	[TestFixture]
	public sealed class AssertExTests
	{
		[Test]
		public void TestIsTrueConstant()
		{
			AssertEx.Assert(() => true);
		}

		[Test]
		public void TestIsTrueCapturedConstant()
		{
			var value = true;
			AssertEx.Assert(() => value);
		}

		[Test]
		public void TestIsTrueEquals()
		{
			var value = 1;
			AssertEx.Assert(() => value == 1);
		}

		[Test]
		public void TestIsTrueAny()
		{
			var value = new[] { 1 };
			AssertEx.Assert(() => value.Any());
		}

		[Test]
		public void TestIsTrueNestedPredicate()
		{
			var value = new[] { 1 };
			AssertEx.Assert(() => value.Any(x => x == 1));
		}

		[Test]
		public void TestIsTrueBooleanLogic()
		{
			var a = 1;
			var b = 3;
			AssertEx.Assert(() => a == 1 && b == 3);
		}

		[Test]
		public void TestFalseConstant()
		{
			// This is silly, but not worth preventing.
			AssertThrowsAssertion(() => false, @"Expected:
	false");
		}

		[Test]
		public void TestBooleanVariable()
		{
			var value = false;
			AssertThrowsAssertion(() => value, @"Expected:
	value

Actual:
	value = false");
		}

		[Test]
		public void TestVariableEquals()
		{
			var value = 1;
			AssertThrowsAssertion(() => value == 2, @"Expected:
	value == 2

Actual:
	value = 1");
		}

		[Test]
		public void TestNullableConvert()
		{
			int? value = 1;
			AssertThrowsAssertion(() => value == 2, @"Expected:
	value == 2

Actual:
	value = 1");
		}

		[Test]
		public void TestNot()
		{
			var value = true;
			AssertThrowsAssertion(() => !value, @"Expected:
	!value

Actual:
	value = true");
		}

		[Test]
		public void TestMember()
		{
			var value = 2;
			AssertThrowsAssertion(() => value == m_member, @"Expected:
	value == m_member

Actual:
	value = 2
	m_member = 1");
		}

		[Test]
		public void TestStaticMember()
		{
			var value = 2;
			AssertThrowsAssertion(() => value == s_member, @"Expected:
	value == s_member

Actual:
	value = 2
	s_member = 1");
		}

		[Test]
		public void TestConstant()
		{
			var value = 2;
			AssertThrowsAssertion(() => value == c_member, @"Expected:
	value == 1

Actual:
	value = 2");
		}

		[Test]
		public void TestAny()
		{
			var value = Array.Empty<int>();
			AssertThrowsAssertion(() => value.Any(), @"Expected:
	value.Any()

Actual:
	value = []");
		}

		[Test]
		public void TestSingle()
		{
			var value = Array.Empty<FooDto>();
			AssertThrowsAssertionWithStackTrace(() => value.Single(), @"Expected:
	value.Single()

Actual:
	value = []

System.InvalidOperationException: Sequence contains no elements");
		}

		[Test]
		public void TestArrayIndex()
		{
			var value = new[] { 2 };
			AssertThrowsAssertion(() => value[0] == 1, @"Expected:
	value[0] == 1

Actual:
	value = [2]");
		}

		[Test]
		public void TestListIndex()
		{
			var value = new List<int> { 2 };
			AssertThrowsAssertion(() => value[0] == 1, @"Expected:
	value[0] == 1

Actual:
	value = [2]");
		}

		[Test]
		public void TestDictionaryIndex()
		{
			var value = new Dictionary<string, int>();
			AssertThrowsAssertionWithStackTrace(() => value["foo"] == 1, @"Expected:
	value[""foo""] == 1

Actual:
	value = {}

System.Collections.Generic.KeyNotFoundException: The given key 'foo' was not present in the dictionary.");
		}

		[Test]
		public void TestNestedPredicate()
		{
			var value = new[] { 1 };
			AssertThrowsAssertion(() => value.Any(x => x == 2), @"Expected:
	value.Any(x => x == 2)

Actual:
	value = [1]");
		}

		[Test]
		public void TestLinqMethodNull()
		{
			int[] value = null;
			AssertThrowsAssertionWithStackTrace(() => value.Any(), @"Expected:
	value.Any()

Actual:
	value = null

System.ArgumentNullException: Value cannot be null. (Parameter 'source')");
		}

		[Test]
		public void TestBooleanLogic()
		{
			var a = 2;
			var b = 4;
			AssertThrowsAssertion(() => a == 1 || b == 3, @"Expected:
	a == 1 || b == 3

Actual:
	a = 2
	b = 4");
		}

		[Test]
		public void TestAndChainIncludesAllFalse()
		{
			var a = 2;
			var b = 4;
			AssertThrowsAssertion(() => a == 1 && b == 3, @"Expected:
	a == 1
	&& b == 3

Actual:
	a = 2
	b = 4");
		}

		[Test]
		public void TestAndChainIncludesOnlyFalse()
		{
			var a = 1;
			var b = 4;
			AssertThrowsAssertion(() => a == 1 && b == 3, @"Expected:
	b == 3

Actual:
	b = 4");
		}

		[Test]
		public void TestPrecedence()
		{
			var a = 1;
			var b = 4;
			var c = 5;
			AssertThrowsAssertion(() => (a + b) * c == 0, @"Expected:
	(a + b) * c == 0

Actual:
	a = 1
	b = 4
	c = 5");
		}

		[Test]
		public void TestAssociativityMinus()
		{
			var a = 1;
			var b = 4;
			var c = 5;
			AssertThrowsAssertion(() => a - (b - c) == -8, @"Expected:
	a - (b - c) == -8

Actual:
	a = 1
	b = 4
	c = 5");
		}

		[Test]
		public void TestAssociativityPlus()
		{
			var a = 1;
			var b = 4;
			var c = 5;
			AssertThrowsAssertion(() => a - (b + c) == 2, @"Expected:
	a - (b + c) == 2

Actual:
	a = 1
	b = 4
	c = 5");
		}

		[Test]
		public void TestBooleanLogicWrapping()
		{
			var a = Array.Empty<string>();
			var b = Array.Empty<string>();
			var c = Array.Empty<string>();
			var d = Array.Empty<string>();
			AssertThrowsAssertion(() => a.Any(x => x.Length == 5) && b.Any(x => x.Length == 5) && c.Any(x => x.Length == 5) && d.Any(x => x.Length == 5), @"Expected:
	a.Any(x => x.Length == 5)
	&& b.Any(x => x.Length == 5)
	&& c.Any(x => x.Length == 5)
	&& d.Any(x => x.Length == 5)

Actual:
	a = []
	b = []
	c = []
	d = []");
		}

		[Test]
		public void TestDtoProperty()
		{
			var foo = new FooDto { Id = "1", Bar = "Fizz" };

			AssertThrowsAssertion(() => foo.Bar == "Buzz", @"Expected:
	foo.Bar == ""Buzz""

Actual:
	foo.Bar = ""Fizz""");
		}

		[Test]
		public void TestDtoPropertyNull()
		{
			FooDto foo = null;

			AssertThrowsAssertionWithStackTrace(() => foo.Bar == "Buzz", @"Expected:
	foo.Bar == ""Buzz""

Actual:
	foo = null

System.NullReferenceException: Object reference not set to an instance of an object.");
		}

		[Test]
		public void TestDtoPropertyNullWithCheck()
		{
			FooDto foo = null;

			AssertThrowsAssertion(() => foo != null && foo.Bar == "Buzz", @"Expected:
	foo != null

Actual:
	foo = null");
		}

		[Test]
		public void TestContextCapturedVariable()
		{
			var value = 1;
			using var d = AssertEx.Context(() => value);
			AssertThrowsAssertion(() => false, @"Expected:
	false

Context:
	value = 1");
		}

		[Test]
		public void TestContextDuplicateCapturedActualVariable()
		{
			var value = 1;
			using var d = AssertEx.Context(() => value);
			AssertThrowsAssertion(() => value == 2, @"Expected:
	value == 2

Actual:
	value = 1");
		}

		[Test]
		public void TestContextDuplicateCapturedContextVariable()
		{
			var value = 1;
			using var d = AssertEx.Context(() => value);
			using var e = AssertEx.Context(() => value);
			AssertThrowsAssertion(() => false, @"Expected:
	false

Context:
	value = 1");
		}

		[Test]
		public void TestContextConstant()
		{
			// This is a bit silly; test is more to document the silly rather than preserve it.
			const int value = 1;
			using var d = AssertEx.Context(() => value);
			AssertThrowsAssertion(() => false, @"Expected:
	false

Context:
	1 = 1");
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
						AssertThrowsAssertion(() => false, @"Expected:
	false

Context:
	firstTask = 1
	value = 1");
					}),
				Task.Run(
					() =>
					{
						var secondTask = 1;
						using var e = AssertEx.Context(() => secondTask);
						AssertThrowsAssertion(() => false, @"Expected:
	false

Context:
	secondTask = 1
	value = 1");
					}));

			AssertThrowsAssertion(() => false, @"Expected:
	false

Context:
	value = 1");
		}

		[Test]
		public void TestSelect()
		{
			var foos = new[] { new FooDto { Baz = 1, Bar = "Buzz" }, };

			AssertThrowsAssertion(() => foos.Select(f => f.Baz).Contains(2), @"Expected:
	foos.Select(f => f.Baz).Contains(2)

Actual:
	foos.Select(f => f.Baz) = [1]");
		}

		[Test]
		public void TestSelectWithCapture()
		{
			var foos = new[] { new FooDto { Baz = 1, Bar = "Buzz" }, };
			var value = 1;

			AssertThrowsAssertion(() => foos.Select(f => f.Baz + value).Contains(1), @"Expected:
	foos.Select(f => f.Baz + value).Contains(1)

Actual:
	value = 1
	foos.Select(f => f.Baz + value) = [2]");
		}

		[Test]
		public void TestAnyWithNullElements()
		{
			var foos = new[] { null, new FooDto { Baz = 1, Bar = "Buzz" } };

			AssertThrowsAssertionWithStackTrace(() => foos.Any(f => f.Bar.Length == 4), @"Expected:
	foos.Any(f => f.Bar.Length == 4)

Actual:
	foos = [null, { ""bar"": ""Buzz"", ""baz"": 1 }]

System.NullReferenceException: Object reference not set to an instance of an object.");
		}

		[Test]
		public void TestAnyWithNullProperties()
		{
			var foos = new[] { new FooDto { Baz = 2 }, new FooDto { Baz = 1, Bar = "Buzz" } };

			AssertThrowsAssertionWithStackTrace(() => foos.Any(f => f.Bar.Length == 4), @"Expected:
	foos.Any(f => f.Bar.Length == 4)

Actual:
	foos = [{ ""baz"": 2 }, { ""bar"": ""Buzz"", ""baz"": 1 }]

System.NullReferenceException: Object reference not set to an instance of an object.");
		}

		[Test]
		public void TestAll()
		{
			var foos = new[] { new FooDto { Baz = 2 }, new FooDto { Baz = 1, Bar = "Buzz" } };

			AssertThrowsAssertion(() => foos.All(f => f.Baz == 1), @"Expected:
	foos.All(f => f.Baz == 1)

Actual:
	foos[0] = { ""baz"": 2 }");
		}

		[Test]
		public void TestNotAny()
		{
			var foos = new[] { new FooDto { Baz = 2 }, new FooDto { Baz = 1, Bar = "Buzz" } };

			AssertThrowsAssertion(() => !foos.Any(f => f.Baz == 2), @"Expected:
	!foos.Any(f => f.Baz == 2)

Actual:
	foos[0] = { ""baz"": 2 }");
		}

		[Test]
		public void TestLargeObjectFormatting()
		{
			var bar = new BarDto { Text = "Oogity Boogity Boo, The Krampus comes for you.", Foo = new FooDto { Bar = "hashtag yolo swag", Baz = 1 } };

			AssertThrowsAssertion(() => bar == null, @"Expected:
	bar == null

Actual:
	bar = {
		""text"": ""Oogity Boogity Boo, The Krampus comes for you."",
		""foo"": { ""bar"": ""hashtag yolo swag"", ""baz"": 1 }
	}");
		}

		[Test]
		public void TestLargeObjectWithSmallArrayFormatting()
		{
			var bar = new BarDto { Text = "Oogity Boogity Boo, The Krampus comes for you.", Foo = new FooDto { Bar = "hashtag yolo swag", Baz = 1 }, Ints = new[] { 1, 2, 3 } };

			AssertThrowsAssertion(() => bar == null, @"Expected:
	bar == null

Actual:
	bar = {
		""text"": ""Oogity Boogity Boo, The Krampus comes for you."",
		""ints"": [1, 2, 3],
		""foo"": { ""bar"": ""hashtag yolo swag"", ""baz"": 1 }
	}");
		}

		[Test]
		public void TestLargeObjectWithLargeArrayFormatting()
		{
			var bar = new BarDto { Text = "Oogity Boogity Boo, The Krampus comes for you.", Ints = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26 } };

			AssertThrowsAssertion(() => bar == null, @"Expected:
	bar == null

Actual:
	bar = {
		""text"": ""Oogity Boogity Boo, The Krampus comes for you."",
		""ints"": [
			1,
			2,
			3,
			4,
			5,
			6,
			7,
			8,
			9,
			10,
			11,
			12,
			13,
			14,
			15,
			16,
			17,
			18,
			19,
			20,
			21,
			22,
			23,
			24,
			25,
			26
		]
	}");
		}

		[Test, Explicit("TODO: Fix this")]
		public void TestStaticStringMethod()
		{
			var foo = "foo";

#pragma warning disable CA1309 // Use ordinal string comparison
			AssertThrowsAssertion(() => string.Equals("bar", foo, StringComparison.InvariantCultureIgnoreCase), @"Expected:
	string.Equals(""bar"", foo, StringComparison.InvariantCultureIgnoreCase)

Actual:
	foo = ""foo""");
#pragma warning restore CA1309 // Use ordinal string comparison
		}

		[Test, Explicit("TODO: Fix this")]
		public void TestEnumCast()
		{
			var foo = 1;

			AssertThrowsAssertion(() => ((FooEnum) foo).ToString() == "Bar", @"Expected:
	((FooEnum) foo).ToString() == ""Bar""

Actual:
	((FooEnum) foo).ToString() = ""Foo""");
		}

		[Test, Explicit("TODO: Fix this")]
		public void TestStaticMemberOfBaseClass()
		{
			AssertThrowsAssertion(() => new FooImpl().DoFalseAssert(), @"Expected:
	Bar == ""Buzz""

Actual:
	Bar = ""Baz""", expectStackTrace: false);
		}

		private static void AssertThrowsAssertionWithStackTrace<T>(Expression<Func<T>> expression, string expectedMessage)
			where T : class
			=> AssertThrowsAssertion(() => AssertEx.Select(expression), expectedMessage, expectStackTrace: true);

		private static void AssertThrowsAssertion(Expression<Func<bool>> expression, string expectedMessage)
			=> AssertThrowsAssertion(() => AssertEx.Assert(expression), expectedMessage, expectStackTrace: false);

		private static void AssertThrowsAssertionWithStackTrace(Expression<Func<bool>> expression, string expectedMessage)
			=> AssertThrowsAssertion(() => AssertEx.Assert(expression), expectedMessage, expectStackTrace: true);

		private static void AssertThrowsAssertion(TestDelegate code, string expectedMessage, bool expectStackTrace)
		{
			var assertion = Assert.Throws<AssertionException>(code);

			if (expectStackTrace)
			{
				Assert.AreEqual(expectedMessage, assertion.Message[..expectedMessage.Length], assertion.Message);
				Assert.AreNotEqual(expectedMessage.Length, assertion.Message.Length, "Expected stack trace, got: " + expectedMessage);
				foreach (var line in assertion.Message[expectedMessage.Length..].Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0))
					Assert.AreEqual("at ", line[..3], line);
			}
			else
			{
				Assert.AreEqual(expectedMessage, assertion.Message, assertion.Message);
			}
		}

		private sealed class FooDto
		{
			public string Id { get; set; }
			public string Bar { get; set; }
			public int? Baz { get; set; }
		}

		private enum FooEnum
		{
			Foo = 1,
		}

		private abstract class FooBase
		{
#pragma warning disable CA1802 // Use literals where appropriate
			protected static readonly string Bar = "Baz";
#pragma warning restore CA1802 // Use literals where appropriate
		}

		private sealed class FooImpl : FooBase
		{
#pragma warning disable CA1822 // Mark members as static
			public void DoFalseAssert()
#pragma warning restore CA1822 // Mark members as static
			{
				AssertEx.Assert(() => Bar == "Buzz");
			}
		}

		private sealed class BarDto
		{
			public string Text { get; set; }
			public int[] Ints { get; set; }
			public FooDto Foo { get; set; }
			public FooDto[] Foos { get; set; }
		}

		private const int c_member = 1;
#pragma warning disable CA1802 // Use literals where appropriate
		private static readonly int s_member = 1;
#pragma warning restore CA1802 // Use literals where appropriate
		private readonly int m_member = 1;
	}
}
