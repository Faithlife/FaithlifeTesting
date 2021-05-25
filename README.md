# Faithlife.Testing

[![Build](https://github.com/Faithlife/FaithlifeTesting/workflows/Build/badge.svg)](https://github.com/Faithlife/FaithlifeTesting/actions?query=workflow%3ABuild) [![NuGet](https://img.shields.io/nuget/v/Faithlife.Testing.svg)](https://www.nuget.org/packages/Faithlife.Testing)

A collection of libraries for better testing in C#.

* [Documentation](https://faithlife.github.io/FaithlifeTesting/)
* [Release Notes](ReleaseNotes.md)

## Table of Contents

* [Why use `Faithlife.Testing`?](#why-use-faithlife.testing?)
* [Advanced Examples](#advanced-examples)
* [Limitations of Expressions](#limitations-of-expressions)

## Why use `Faithlife.Testing`?

`Faithlife.Testing` uses the power of C# expression trees to help pinpoint exactly why your assertions are failing.

Some of the main problems that `Faithlife.Testing` is attempting to solve is:

* The requirement to learn a new "syntax" when using a new test runner or assertion library.
	```csharp
	string actual = "test";
	Assert.That(actual, Is.Not.Null.And.Length.AtLeast(3));
	```
* Many assertion libraries have common pits of failure, like using something like `Assert.True`. You're never going to get a helpful failure message out of an assertion like that.
	```csharp
	bool isFrobbable = false;
	Assert.True(isFrobbable);
	...
	Expected: True
  	But was: False
	```
* Needing to write "guard" assertions to get helpful failure messages. If you have a deeply nested object you want to assert on, you'd normally want to assert every property along the way isn't null, which can become quite tedious:
	```
	Assert.NotNull(foo);
	Assert.NotNull(foo.Bar);
	Assert.NotNull(foo.Bar.Baz);
	```

With Faithlife.Testing, those problems go away!

* No learning a new syntax beyond calling `AssertEx.IsTrue` and `AssertEx.HasValue`, simply use C#!
	```csharp
	string actual = "test";
	AssertEx.IsTrue(() => actual.Length >= 3);
	...
	Expected:
		actual.Length >= 3

	Actual:
		actual.Length = 4
	```	
* No more checking boolean expression against `IsTrue`! Just run the boolean expression.
	```csharp
	bool isFrobbable = false;
	AssertEx.IsTrue(() => isFrobbable);
	...
	Expected:
		isFrobbable

	Actual:
		isFrobbable = false
	```
* There's no longe a need to use guard assertions so that you can know which property in a chain was the one that null.
	```csharp
	var foo = new Foo
	{
		Bar = null
	};
	AssertEx.HasValue(() => foo.Bar.Baz);
	...
	Expected:
		foo.Bar.Baz != null

	Actual:
		foo.Bar = null
	```

## Advanced Examples

A common pattern in testing is to have methods for fetching data whose callers then assertions on it. `Faithlife.Testing` exposes tools for preserving helpful context that a single method alone often lack.

### `Assertable<T>`

`Assertable<T>` allows methods to return values on which further assertions can be made.

### `Context`

Helpful context -- in the form of name/value pairs displayed in assertion messages -- can be attached to an `Assertable<T>`, and can also be attached to all assertions made inside a `using` block.

### `WaitUntil`

`AssertEx.WaitUntil` allows you to retry a function which fetches a value until all assertions chained on it pass.

## Limitations of Expressions

Because `Faithlife.Testing` is based around expressions, some newer C# language features cannot be used inside assertions. These features can be used inside functions called by your expression -- just not inside the expression itself.

These features include:

* `async/await` - instead of using `async/await` in an expression, you can call a separate function to get an asyncrhonous value then make synchronous assertions on it.
* The `null`-coalescing operator -- instead of using the null-coalescing `?.` operator, just let the `NullReferenceException`s fly! `Faithlife.Testing` will tell you exactly what was `null`.