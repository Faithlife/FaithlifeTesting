using System;
using System.Collections.Generic;
using System.Linq;
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

		[Test, ExpectedMessage(@"Expected:
	false")]
		public void TestFalseConstant()
		{
			// This is silly, but not worth preventing.
			AssertEx.Assert(() => false);
		}

		[Test, ExpectedMessage(@"Expected:
	value

Actual:
	value = false")]
		public void TestBooleanVariable()
		{
			var value = false;
			AssertEx.Assert(() => value);
		}

		[Test, ExpectedMessage(@"Expected:
	value == 2

Actual:
	value = 1")]
		public void TestVariableEquals()
		{
			var value = 1;
			AssertEx.Assert(() => value == 2);
		}

		[Test, ExpectedMessage(@"Expected:
	value == 2

Actual:
	value = 1")]
		public void TestNullableConvert()
		{
			int? value = 1;
			AssertEx.Assert(() => value == 2);
		}

		[Test, ExpectedMessage(@"Expected:
	!value

Actual:
	value = true")]
		public void TestNot()
		{
			var value = true;
			AssertEx.Assert(() => !value);
		}

		[Test, ExpectedMessage(@"Expected:
	value == m_member

Actual:
	value = 2
	m_member = 1")]
		public void TestMember()
		{
			var value = 2;
			AssertEx.Assert(() => value == m_member);
		}

		[Test, ExpectedMessage(@"Expected:
	value == s_member

Actual:
	value = 2
	s_member = 1")]
		public void TestStaticMember()
		{
			var value = 2;
			AssertEx.Assert(() => value == s_member);
		}

		[Test, ExpectedMessage(@"Expected:
	value == 1

Actual:
	value = 2")]
		public void TestConstant()
		{
			var value = 2;
			AssertEx.Assert(() => value == c_member);
		}

		[Test, ExpectedMessage(@"Expected:
	value.Any()

Actual:
	value = []")]
		public void TestAny()
		{
			var value = Array.Empty<int>();
			AssertEx.Assert(() => value.Any());
		}

		[Test, ExpectedMessage(@"Expected:
	value.Single()

Actual:
	value = []

System.InvalidOperationException: Sequence contains no elements",
			expectStackTrace:true)]
		public void TestSingle()
		{
			var value = Array.Empty<FooDto>();
			AssertEx.Select(() => value.Single());
		}

		[Test, ExpectedMessage(@"Expected:
	value[0] == 1

Actual:
	value = [2]")]
		public void TestArrayIndex()
		{
			var value = new[] { 2 };
			AssertEx.Assert(() => value[0] == 1);
		}

		[Test, ExpectedMessage(@"Expected:
	value[0] == 1

Actual:
	value = [2]")]
		public void TestListIndex()
		{
			var value = new List<int> { 2 };
			AssertEx.Assert(() => value[0] == 1);
		}

		[Test, ExpectedMessage(@"Expected:
	value[""foo""] == 1

Actual:
	value = {}

System.Collections.Generic.KeyNotFoundException: The given key 'foo' was not present in the dictionary.",
			expectStackTrace:true)]
		public void TestDictionaryIndex()
		{
			var value = new Dictionary<string, int>();
			AssertEx.Assert(() => value["foo"] == 1);
		}

		[Test, ExpectedMessage(@"Expected:
	value.Any(x => x == 2)

Actual:
	value = [1]")]
		public void TestNestedPredicate()
		{
			var value = new[] { 1 };
			AssertEx.Assert(() => value.Any(x => x == 2));
		}

		[Test, ExpectedMessage(@"Expected:
	value.Any()

Actual:
	value = null

System.ArgumentNullException: Value cannot be null. (Parameter 'source')", expectStackTrace:true)]
		public void TestLinqMethodNull()
		{
			int[] value = null;
			AssertEx.Assert(() => value.Any());
		}

		[Test, ExpectedMessage(@"Expected:
	a == 1 || b == 3

Actual:
	a = 2
	b = 4")]
		public void TestBooleanLogic()
		{
			var a = 2;
			var b = 4;
			AssertEx.Assert(() => a == 1 || b == 3);
		}

		[Test, ExpectedMessage(@"Expected:
	a == 1
	&& b == 3

Actual:
	a = 2
	b = 4")]
		public void TestAndChainIncludesAllFalse()
		{
			var a = 2;
			var b = 4;
			AssertEx.Assert(() => a == 1 && b == 3);
		}

		[Test, ExpectedMessage(@"Expected:
	b == 3

Actual:
	b = 4")]
		public void TestAndChainIncludesOnlyFalse()
		{
			var a = 1;
			var b = 4;
			AssertEx.Assert(() => a == 1 && b == 3);
		}

		[Test, ExpectedMessage(@"Expected:
	(a + b) * c == 0

Actual:
	a = 1
	b = 4
	c = 5")]
		public void TestPrecedence()
		{
			var a = 1;
			var b = 4;
			var c = 5;
			AssertEx.Assert(() => (a + b) * c == 0);
		}

		[Test, ExpectedMessage(@"Expected:
	a - (b - c) == -8

Actual:
	a = 1
	b = 4
	c = 5")]
		public void TestAssociativityMinus()
		{
			var a = 1;
			var b = 4;
			var c = 5;
			AssertEx.Assert(() => a - (b - c) == -8);
		}

		[Test, ExpectedMessage(@"Expected:
	a - (b + c) == 2

Actual:
	a = 1
	b = 4
	c = 5")]
		public void TestAssociativityPlus()
		{
			var a = 1;
			var b = 4;
			var c = 5;
			AssertEx.Assert(() => a - (b + c) == 2);
		}

		[Test, ExpectedMessage(@"Expected:
	a.Any(x => x.Length == 5)
	&& b.Any(x => x.Length == 5)
	&& c.Any(x => x.Length == 5)
	&& d.Any(x => x.Length == 5)

Actual:
	a = []
	b = []
	c = []
	d = []")]
		public void TestBooleanLogicWrapping()
		{
			var a = Array.Empty<string>();
			var b = Array.Empty<string>();
			var c = Array.Empty<string>();
			var d = Array.Empty<string>();
			AssertEx.Assert(() => a.Any(x => x.Length == 5) && b.Any(x => x.Length == 5) && c.Any(x => x.Length == 5) && d.Any(x => x.Length == 5));
		}

		[Test, ExpectedMessage(@"Expected:
	foo.Bar == ""Buzz""

Actual:
	foo.Bar = ""Fizz""")]
		public void TestDtoProperty()
		{
			var foo = new FooDto { Id = "1", Bar = "Fizz" };
			AssertEx.Assert(() => foo.Bar == "Buzz");
		}

		[Test, ExpectedMessage(@"Expected:
	foo.Bar == ""Buzz""

Actual:
	foo = null

System.NullReferenceException: Object reference not set to an instance of an object.",
			expectStackTrace:true)]
		public void TestDtoPropertyNull()
		{
			FooDto foo = null;

			AssertEx.Assert(() => foo.Bar == "Buzz");
		}

		[Test, ExpectedMessage(@"Expected:
	foo != null

Actual:
	foo = null")]
		public void TestDtoPropertyNullWithCheck()
		{
			FooDto foo = null;

			AssertEx.Assert(() => foo != null && foo.Bar == "Buzz");
		}

		[Test, ExpectedMessage(@"Expected:
	false

Context:
	value = 1")]
		public void TestContextCapturedVariable()
		{
			var value = 1;
			using var d = AssertEx.Context(() => value);
			AssertEx.Assert(() => false);
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

		[Test, ExpectedMessage(@"Expected:
	false

Context:
	value = 1")]
		public void TestContextDuplicateCapturedContextVariable()
		{
			var value = 1;
			using var d = AssertEx.Context(() => value);
			using var e = AssertEx.Context(() => value);
			AssertEx.Assert(() => false);
		}

		[Test, ExpectedMessage(@"Expected:
	false

Context:
	1 = 1")]
		public void TestContextConstant()
		{
			// This is a bit silly; test is more to document the silly rather than preserve it.
			const int value = 1;
			using var d = AssertEx.Context(() => value);
			AssertEx.Assert(() => false);
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
						AssertHasContext(@"
	firstTask = 1
	value = 1");
					}),
				Task.Run(
					() =>
					{
						var secondTask = 1;
						using var e = AssertEx.Context(() => secondTask);
						AssertHasContext(@"
	secondTask = 1
	value = 1");
					}));

			AssertHasContext(@"
	value = 1");

			void AssertHasContext(string expectedContext)
			{
				var assertion = Assert.Throws<AssertionException>(() => AssertEx.Assert(() => false));
				new ExpectedMessageAttribute(@$"Expected:
	false

Context:{expectedContext}", expectStackTrace:false)
					.AssertMessageIsExpected(assertion.Message);
			}
		}

		[Test, ExpectedMessage(@"Expected:
	foos.Select(f => f.Baz).Contains(2)

Actual:
	foos.Select(f => f.Baz) = [1]")]
		public void TestSelect()
		{
			var foos = new[] { new FooDto { Baz = 1, Bar = "Buzz" } };
			AssertEx.Assert(() => foos.Select(f => f.Baz).Contains(2));
		}

		[Test, ExpectedMessage(@"Expected:
	foos.Select(f => f.Baz + value).Contains(1)

Actual:
	value = 1
	foos.Select(f => f.Baz + value) = [2]")]
		public void TestSelectWithCapture()
		{
			var foos = new[] { new FooDto { Baz = 1, Bar = "Buzz" } };
			var value = 1;
			AssertEx.Assert(() => foos.Select(f => f.Baz + value).Contains(1));
		}

		[Test, ExpectedMessage(@"Expected:
	foos.Any(f => f.Bar.Length == 4)

Actual:
	foos = [null, { ""bar"": ""Buzz"", ""baz"": 1 }]

System.NullReferenceException: Object reference not set to an instance of an object.", expectStackTrace:true)]
		public void TestAnyWithNullElements()
		{
			var foos = new[] { null, new FooDto { Baz = 1, Bar = "Buzz" } };

			AssertEx.Assert(() => foos.Any(f => f.Bar.Length == 4));
		}

		[Test, ExpectedMessage(@"Expected:
	foos.Any(f => f.Bar.Length == 4)

Actual:
	foos = [{ ""baz"": 2 }, { ""bar"": ""Buzz"", ""baz"": 1 }]

System.NullReferenceException: Object reference not set to an instance of an object.", expectStackTrace:true)]
		public void TestAnyWithNullProperties()
		{
			var foos = new[] { new FooDto { Baz = 2 }, new FooDto { Baz = 1, Bar = "Buzz" } };

			AssertEx.Assert(() => foos.Any(f => f.Bar.Length == 4));
		}

		[Test, ExpectedMessage(@"Expected:
	foos.All(f => f.Baz == 1)

Actual:
	foos[0] = { ""baz"": 2 }")]
		public void TestAll()
		{
			var foos = new[] { new FooDto { Baz = 2 }, new FooDto { Baz = 1, Bar = "Buzz" } };
			AssertEx.Assert(() => foos.All(f => f.Baz == 1));
		}

		[Test, ExpectedMessage(@"Expected:
	!foos.Any(f => f.Baz == 2)

Actual:
	foos[0] = { ""baz"": 2 }")]
		public void TestNotAny()
		{
			var foos = new[] { new FooDto { Baz = 2 }, new FooDto { Baz = 1, Bar = "Buzz" } };
			// ReSharper disable once SimplifyLinqExpressionUseAll
			AssertEx.Assert(() => !foos.Any(f => f.Baz == 2));
		}

		[Test, ExpectedMessage(@"Expected:
	bar == null

Actual:
	bar = {
		""text"": ""Oogity Boogity Boo, The Krampus comes for you."",
		""foo"": { ""bar"": ""hashtag yolo swag"", ""baz"": 1 }
	}")]
		public void TestLargeObjectFormatting()
		{
			var bar = new BarDto { Text = "Oogity Boogity Boo, The Krampus comes for you.", Foo = new FooDto { Bar = "hashtag yolo swag", Baz = 1 } };
			AssertEx.Assert(() => bar == null);
		}

		[Test, ExpectedMessage(@"Expected:
	bar == null

Actual:
	bar = {
		""text"": ""Oogity Boogity Boo, The Krampus comes for you."",
		""ints"": [1, 2, 3],
		""foo"": { ""bar"": ""hashtag yolo swag"", ""baz"": 1 }
	}")]
		public void TestLargeObjectWithSmallArrayFormatting()
		{
			var bar = new BarDto { Text = "Oogity Boogity Boo, The Krampus comes for you.", Foo = new FooDto { Bar = "hashtag yolo swag", Baz = 1 }, Ints = new[] { 1, 2, 3 } };
			AssertEx.Assert(() => bar == null);
		}

		[Test, ExpectedMessage(@"Expected:
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
	}")]
		public void TestLargeObjectWithLargeArrayFormatting()
		{
			var bar = new BarDto { Text = "Oogity Boogity Boo, The Krampus comes for you.", Ints = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26 } };
			AssertEx.Assert(() => bar == null);
		}

		[Test, Explicit("TODO: Fix this"), ExpectedMessage(@"Expected:
	string.Equals(""bar"", foo, StringComparison.OrdinalIgnoreCase)

Actual:
	foo = ""foo""")]
		public void TestStaticStringMethod()
		{
			var foo = "foo";

			AssertEx.Assert(() => string.Equals("bar", foo, StringComparison.OrdinalIgnoreCase));
		}

		[Test, Explicit("TODO: Fix this"), ExpectedMessage(@"Expected:
	((FooEnum) foo).ToString() == ""Bar""

Actual:
	((FooEnum) foo).ToString() = ""Foo""")]
		public void TestEnumCast()
		{
			var foo = 1;

			AssertEx.Assert(() => ((FooEnum) foo).ToString() == "Bar");
		}

		[Test, Explicit("TODO: Fix this"), ExpectedMessage(@"Expected:
	Bar == ""Buzz""

Actual:
	Bar = ""Baz""")]
		public void TestStaticMemberOfBaseClass()
		{
			new FooImpl().DoFalseAssert();
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
				AssertEx.Select(() => foo)
					.Assert(a => a.Length == 5);
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
				AssertEx.Select(() => foo)
					.Assert(a => a.Length == 5)
					.Assert(a => a.Length == 4);
			});
		}

		[Test, ExpectedMessage(@"Expected:
	actual[i] == 1

Actual:
	actual = [1, 2, 3]
	i = 1")]
		public void TestVariableIndex()
		{
			var actual = new[] { 1, 2, 3 };

			for (var i = 0; i < actual.Length; i++)
				AssertEx.Assert(() => actual[i] == 1);
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
