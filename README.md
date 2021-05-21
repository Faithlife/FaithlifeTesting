# Faithlife.Testing

[![Build](https://github.com/Faithlife/FaithlifeTesting/workflows/Build/badge.svg)](https://github.com/Faithlife/FaithlifeTesting/actions?query=workflow%3ABuild) [![NuGet](https://img.shields.io/nuget/v/Faithlife.Testing.svg)](https://www.nuget.org/packages/Faithlife.Testing)

A collection of libraries for better testing in C#.

* [Documentation](https://faithlife.github.io/FaithlifeTesting/)
* [Release Notes](ReleaseNotes.md)

## Table of Contents

* [Why use Faithlife.Testing?](#why-use-faithlife.testing?)
* [Using AssertEx.Select](#using-assertex.select)
* [Using AssertEx.Context](#using-assertex.context)
* [Advanced Examples](#advanced-examples)

## Why use `Faithlife.Testing`?

Some of the main problems that `Faithlife.Testing` is attempting to solve is:

* The requirement to learn a new "syntax" when using a new test runner or assertion library.
	```csharp
	string actual = "test";
	Assert.That(actual, Is.Not.Null.And.Length.AtLeast(3));
	```
* Many assertion libraries have common pits of failure, like using something like `Assert.IsTrue`. You're never going to get a helpful failure message out of an assertion like that.
	```csharp
	Assert.IsTrue(actual != null);
	...
	Expected: True
  	But was:  False
	```
* Needing to write "guard" assertions to get helpful failure messages. If you have a deeply nested object you want to assert on, you'd normally want to assert every property along the way isn't null, which can become quite tedious:
	```
	Assert.NotNull(foo);
	Assert.NotNull(foo.bar);
	Assert.NotNull(foo.bar.baz);
	```

With Faithlife.Testing, those problems go away!

* No learning a new syntax beyond calling `AssertEx.Assert` and `AssertEx.Select`, simply use C#!
	```csharp
	string actual = "test";
	AssertEx.Assert (() => actual != null && actual.Length >= 3)
	```	
* No more checking boolean expression against `IsTrue`! Just run the boolean expression.
	```csharp
	AssertEx.Assert (() => actual != null)
	...
	Expected:
		actual != null

	Actual:
		actual = null
	```
* There's no longe a need to use guard assertions so that you can know which property in a chain was the one that null.
	```csharp
	var foo = new Foo
	{
		Bar = null
	};
	AssertEx.Assert (() => foo.Bar.Baz != null);
	...
	Expected:
		foo.Bar.Baz != null

	Actual:
		foo.Bar = null
	```

## Using `AssertEx.Select`

TODO

## Using `AssertEx.Context`

TODO

## Advanced Examples