# Release Notes

## 2.1.7

* Patch: Fix NRE when displaying a tuple.

## 2.1.6

* Patch: Fix NRE when MessageProcessedAwaiter is disposed with an outstanding message.

## 2.1.5

* Patch: Gracefully handle exceptions thrown by message-awaiter predicates.

## 2.1.4

* Patch: Fix regression in `MessagePublishedAwaiter`, added tests.

## 2.1.3

* Patch: Fix another duplicate-ack bug in re-used `MessageProcessedAwaiter`s, improve tests and code-clarity.

## 2.1.2

* Patch: Fix duplicate-ack bug in re-used `MessageProcessedAwaiter`s.

## 2.1.1

* Patch: Upgrade RabbitMQ.Client to 6.2.2.

## 2.1.0

* Minor: Add a `MessageProcessedAwaiter` for waiting on existing-queue messages.
* Patch: Fix bug in `MessagePublishedAwaiter` which would allow messages to be lost if they were published before the consumer started.

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
