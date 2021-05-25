# Faithlife.Testing

[![Build](https://github.com/Faithlife/FaithlifeTesting/workflows/Build/badge.svg)](https://github.com/Faithlife/FaithlifeTesting/actions?query=workflow%3ABuild) [![NuGet](https://img.shields.io/nuget/v/Faithlife.Testing.svg)](https://www.nuget.org/packages/Faithlife.Testing)

`Faithlife.Testing` uses the power of C# expression trees to help pinpoint exactly why your assertions are failing.

* [Documentation](https://faithlife.github.io/FaithlifeTesting/)
* [Release Notes](ReleaseNotes.md)

## Table of Contents

* [Endoresements](#endoresements)
* [Why use `Faithlife.Testing`?](#why-use-faithlifetesting)
* [Advanced Examples](#advanced-examples)
  * [`Assertable<T>`](#assertablet)
  * [`Context`](#context)
  * [`WaitUntil`](#waituntil)
* [Limitations of Expressions](#limitations-of-expressions)

## Endoresements

"AssertEx is such a blessing, [...] it feels like a whole new world of possibilities has opened up to me."

  \- Joseph Stewart

"My mind was blown the first time I tried out a failure with AssertEx in linqpad."

  \- Ryan Johnson

"Oh, wow. That's amazing."

  \- Patrick Nausha, upon learning how AssertEx handles boolean logic

## Why use `Faithlife.Testing`?

Some of the main problems that `Faithlife.Testing` is attempting to solve is:

* The requirement to learn a new "syntax" when using a new test runner or assertion library.

 ```csharp
 string actual = "ab";
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

```csharp
Assert.NotNull(foo);
Assert.NotNull(foo.Bar);
Assert.NotNull(foo.Bar.Baz);
```

* Having many assertions fail, but only seeing one failure-message. When multiple things fail, you want to see all the failures -- but no more than that.

```csharp
var foo = new Foo
{
    Name = "name",
    Id = 1,
    Bar = null,
 }
 Assert.AreEqual("test", foo.Name);
 Assert.AreEqual(1, foo.Id);
 Assert.NotNull(foo.Bar.Baz);
 ```

With `Faithlife.Testing`, those problems go away!

* No learning a new syntax beyond calling `AssertEx.IsTrue` and `AssertEx.HasValue`, simply use C#!

 ```csharp
 string actual = "ab";
 AssertEx.IsTrue(() => actual.Length >= 3);
 ...
 Expected:
    actual.Length >= 3

 Actual:
    actual.Length = 2
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

* AssertEx will deconstruct boolean logic to show exactly why each assertion failed, and will elide the branches which did not fail.

 ```csharp
 var foo = new Foo
 {
    Name = "name",
    Id = 1,
    Bar = null,
 }
 AssertEx.IsTrue(() => foo.Name == "test" && foo.Id == 1 && foo.Bar.Baz != null);
 ...
 Expected:
    foo.Name == "test" && foo.Bar.Baz != null

 Actual:
    foo.Name = "name"
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
