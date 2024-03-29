using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using Faithlife.Testing.RabbitMq;
using FakeItEasy;
using NUnit.Framework;

namespace Faithlife.Testing.Tests.RabbitMq
{
	[TestFixture]
	public sealed class MessagePublishedAwaiterTests
	{
		[Test]
		public async Task TestMessage()
		{
			var (awaiter, messages) = GetAwaiter();

			var messagePublished = awaiter.WaitForMessage(m => m.Id == 1);

			messages.TryWrite("{ id: 1, bar: \"baz\" }");

			(await messagePublished).IsTrue(m => m.Bar == "baz");
		}

		[Test, Timeout(10000), ExpectedMessage(@"Expected:
	messages.First(m => m.Id == 1)

Actual:
	messages = []

Context:
	timeout = ""50 milliseconds""
	messageCount = 0
	context = ""present""

System.InvalidOperationException: Sequence contains no matching element", expectStackTrace: true)]
		public async Task TestNoMessages()
		{
			var (awaiter, _) = GetAwaiter(shortTimeout: true);

			await awaiter.WaitForMessage(m => m.Id == 1);
		}

		[Test, Timeout(10000), ExpectedMessage(@"Expected:
	messages.First(m => Throw())

Actual:
	messages = [{ ""id"": 1, ""bar"": ""baz"" }]

Context:
	timeout = ""50 milliseconds""
	messageCount = 1
	context = ""present""

System.InvalidOperationException: This is a test.", expectStackTrace: true)]
		public async Task TestPredicateFailure()
		{
			var (awaiter, messages) = GetAwaiter(shortTimeout: true);

			var messagePublished = awaiter.WaitForMessage(m => Throw());

			messages.TryWrite("{ id: 1, bar: \"baz\" }");

			await messagePublished;
		}

		private static bool Throw() => throw new InvalidOperationException("This is a test.");

		[Test, Timeout(10000), ExpectedMessage(@"Expected:
	messages.First(m => m.Id == 1)

Actual:
	messages = []

Context:
	malformedMessages = [""garbage""]
	timeout = ""50 milliseconds""
	messageCount = 1
	context = ""present""

System.InvalidOperationException: Sequence contains no matching element", expectStackTrace: true)]
		public async Task TestMalformedMessage()
		{
			var (awaiter, messages) = GetAwaiter(shortTimeout: true);

			var messagePublished = awaiter.WaitForMessage(m => m.Id == 1);

			messages.TryWrite("garbage");

			await messagePublished;
		}

		[Test, Timeout(10000), ExpectedMessage(@"Expected:
	messages.First(m => m.Id == 1)

Actual:
	messages = [{ ""id"": 2, ""bar"": ""baz"" }]

Context:
	timeout = ""50 milliseconds""
	messageCount = 1
	context = ""present""

System.InvalidOperationException: Sequence contains no matching element", expectStackTrace: true)]
		public async Task TestUnmatchedMessage()
		{
			var (awaiter, messages) = GetAwaiter(shortTimeout: true);

			var messagePublished = awaiter.WaitForMessage(m => m.Id == 1);

			messages.TryWrite("{ id: 2, bar: \"baz\" }");

			(await messagePublished).IsTrue(m => m.Bar == "baz");
		}

		[Test]
		public async Task TestMultipleIdenticalMessages()
		{
			var (awaiter, messages) = GetAwaiter();

			var messagePublished = awaiter.WaitForMessage(m => m.Id == 1);

			messages.TryWrite("{ id: 1, bar: \"baz\" }");
			messages.TryWrite("{ id: 1, bar: \"baz\" }");

			(await messagePublished).IsTrue(m => m.Bar == "baz");
		}

		[Test]
		public async Task TestOneMessageMultipleAwaiters()
		{
			var (awaiter, messages) = GetAwaiter();

			var firstMessagePublished = awaiter.WaitForMessage(m => m.Id == 1);
			var secondMessagePublished = awaiter.WaitForMessage(m => m.Id == 1);

			messages.TryWrite("{ id: 1, bar: \"baz\" }");

			(await firstMessagePublished).IsTrue(m => m.Bar == "baz");
			(await secondMessagePublished).IsTrue(m => m.Bar == "baz");
		}

		[Test]
		public async Task TestMultipleAwaiters()
		{
			var (awaiter, messages) = GetAwaiter();

			var firstMessagePublished = awaiter.WaitForMessage(m => m.Id == 1);
			var secondMessagePublished = awaiter.WaitForMessage(m => m.Id == 2);

			messages.TryWrite("{ id: 1, bar: \"baz\" }");
			messages.TryWrite("{ id: 2, bar: \"zab\" }");

			(await firstMessagePublished).IsTrue(m => m.Bar == "baz");
			(await secondMessagePublished).IsTrue(m => m.Bar == "zab");
		}

		[Test]
		public async Task TestDisposeBeforeAwait()
		{
			var mock = A.Fake<IRabbitMqWrapper>();
			var (awaiter, _) = GetAwaiter(rabbitMq: mock);

			var messagePublished = awaiter.WaitForMessage(_ => true);

			awaiter.Dispose();

			Assert.ThrowsAsync<TaskCanceledException>(async () => await messagePublished);

			A.CallTo(() => mock.Dispose()).MustHaveHappenedOnceExactly();
			A.CallTo(mock).MustHaveHappenedOnceExactly();
		}

		private sealed class FooDto
		{
			public int Id { get; set; }
			public string Bar { get; set; }
		}

		private static (MessagePublishedAwaiter<FooDto> Awaiter, ChannelWriter<string> Messages) GetAwaiter(bool shortTimeout = false, IRabbitMqWrapper rabbitMq = null)
		{
			var messages = Channel.CreateUnbounded<string>();
			return (
				new MessagePublishedAwaiter<FooDto>(TimeSpan.FromMilliseconds(shortTimeout ? 50 : 10_000), new { context = "present" }, rabbitMq, messages.Reader),
				messages.Writer);
		}
	}
}
