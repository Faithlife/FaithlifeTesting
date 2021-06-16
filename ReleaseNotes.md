# Release Notes

## 2.0.1

* Patch: [#26](https://github.com/Faithlife/FaithlifeTesting/issues/26) Improve "actual" value display for `null` properties.

## 2.0.0

* BREAKING: [#11](https://github.com/Faithlife/FaithlifeTesting/issues/11)
  * Renamed `.Select([etc.])` to `.HasValue([etc.])`
  * Renamed `.Assert([etc.])` to `.IsTrue([etc.])`
  * Renamed `AssertEx.Builder<T>` to `Assertable<T>`
* Minor: [#18](https://github.com/Faithlife/FaithlifeTesting/issues/18) Add `.AssertResponse()` extension for `Faithlife.WebRequests.AutoWebServiceResponse`s.
* Minor: [#17](https://github.com/Faithlife/FaithlifeTesting/issues/17) Add `AssertEx.WaitUntil(Func<T>)` family of methods for retrying an action until it succeeds.
* Minor: [#10](https://github.com/Faithlife/FaithlifeTesting/issues/10) Allow `AssertEx.Value<T>(T value)` overload to specify an explicit string name.
* Patch: [#14](https://github.com/Faithlife/FaithlifeTesting/issues/14) Remove reference to specific NUnit version.
* Patch: [#13](https://github.com/Faithlife/FaithlifeTesting/issues/13) Improve output when AssertEx is used inside NUnit `Assert.Multiple` blocks.
* Patch: [#9](https://github.com/Faithlife/FaithlifeTesting/issues/9) Improve output when encountering indexers on captured lists.
* Patch: [#8](https://github.com/Faithlife/FaithlifeTesting/issues/8) Allow empty lists of context.
* Patch: Improve XML-doc comments

## 1.3.0

* Minor: Added `Context` overloads to `AssertEx.Builder` chains
* Patch: Remove dependencies on `Faithlife.Utility` and `System.Collections.Immutable`.

## 1.2.0

* Added overload for `WaitForMessage` in `MessagePublishedAwaiter` that lets you pass in a custom timeout

## 1.1.0

* Published Faithlife.Testing.RabbitMq package

## 1.0.0

* Initial release of Faithlife.Testing
