using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Faithlife.Testing.RabbitMq;
using FakeItEasy;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace Faithlife.Testing.Tests.RabbitMq
{
	[TestFixture]
	public class MessageProcessedAwaiterTests
	{
		[Test]
		public async Task TestMessageSuccess()
		{
			var setup = GivenSetup();

			var messageProcessed = setup.Awaiter.WaitForMessage(m => m.Id == 1);

			setup.PublishMessage("{ id: 1, bar: \"baz\" }");

			var message = (await messageProcessed).IsTrue(m =>
				m.Id == 1
				&& m.Bar == "baz")
				.Value;

			setup.ProcessedMessages.IsTrue(messages => messages.Single() == message);

			await setup.VerifyConsumerCalls(mock => mock.BasicAck(1ul));
		}

		[Test, Timeout(10000), ExpectedMessage(@"Expected:
	messages.First(m => m.Id == 1)

Actual:
	messages = []

Context:
	timeout = ""50 milliseconds""
	messageCount = 0
	context = ""present""
	timeoutReason = ""after `await`""

System.InvalidOperationException: Sequence contains no matching element", expectStackTrace: true)]
		public async Task TestNoMessages([Values] bool? isPublishedBeforeWaitForMessage)
		{
			var setup = GivenSetup(shortTimeout: true);

			if (isPublishedBeforeWaitForMessage == true)
				setup.PublishMessage("{ id: 1, bar: \"baz\" }");

			var messageProcessed = setup.Awaiter.WaitForMessage(m => m.Id == 1);

			try
			{
				await messageProcessed;
			}
			finally
			{
				await setup.VerifyConsumerCalls();

				if (isPublishedBeforeWaitForMessage == false)
					setup.PublishMessage("{ id: 1, bar: \"baz\" }");

				await setup.VerifyCalls();

				setup.ProcessedMessages.IsTrue(m => !m.Any());
			}
		}

		[Test, Timeout(10000), ExpectedMessage(@"Expected:
	messages.First(m => m.Id == 2)

Actual:
	messages = [{ ""id"": 1, ""bar"": ""baz"" }]

Context:
	timeout = ""50 milliseconds""
	messageCount = 1
	context = ""present""
	timeoutReason = ""unacked message""

System.InvalidOperationException: Sequence contains no matching element", expectStackTrace: true)]
		public async Task TestUnackedMessageTimeout()
		{
			var setup = GivenSetup(shortTimeout: true);

			var messageProcessed = setup.Awaiter.WaitForMessage(m => m.Id == 2);

			setup.PublishMessage("{ id: 1, bar: \"baz\" }");

			await setup.VerifyConsumerCalls(mock => mock.BasicNack(1ul, A<bool>._));

			try
			{
				await messageProcessed;
			}
			finally
			{
				setup.ProcessedMessages.IsTrue(m => !m.Any());
			}
		}

		[Test, Timeout(10000), ExpectedMessage(@"Expected:
	messages.First(m => Throw())

Actual:
	messages = [{ ""id"": 1, ""bar"": ""baz"" }]

Context:
	timeout = ""50 milliseconds""
	messageCount = 1
	context = ""present""
	timeoutReason = ""unacked message""

System.InvalidOperationException: This is a test.", expectStackTrace: true)]
		public async Task TestPredicateFailure()
		{
			var setup = GivenSetup(shortTimeout: true);

			var messageProcessed = setup.Awaiter.WaitForMessage(m => Throw());

			setup.PublishMessage("{ id: 1, bar: \"baz\" }");

			await setup.VerifyConsumerCalls(mock => mock.BasicNack(1ul, A<bool>._));

			try
			{
				await messageProcessed;
			}
			finally
			{
				setup.ProcessedMessages.IsTrue(m => !m.Any());
			}
		}

		private static bool Throw() => throw new InvalidOperationException("This is a test.");

		[Test]
		public async Task TestProcessMessageFailure()
		{
			const string message = "Failure to obtain distributed lock";

			var setup = GivenSetup(processMessage: _ => throw new InvalidOperationException(message));

			var messageProcessed = setup.Awaiter.WaitForMessage(m => m.Id == 1);

			setup.PublishMessage("{ id: 1, bar: \"baz\" }");

			var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await messageProcessed);

			AssertEx.IsTrue(() => exception.Message.Contains(message, StringComparison.InvariantCulture));

			await setup.VerifyConsumerCalls(mock => mock.BasicNack(1ul, A<bool>._));
		}

		[Test]
		public async Task TestNacking()
		{
			var setup = GivenSetup();

			var messagePublished = setup.Awaiter.WaitForMessage(m => m.Id == 3);

			setup.PublishMessages(
				"{ id: 1, bar: \"baz\" }",
				"{ id: 2, bar: \"baz\" }",
				"{ id: 3, bar: \"baz\" }",
				"{ id: 4, bar: \"baz\" }");

			(await messagePublished).IsTrue(m => m.Id == 3);

			setup.ProcessedMessages.IsTrue(m => m.Single().Id == 3);

			await setup.VerifyConsumerCalls(
				mock => mock.BasicNack(2ul, A<bool>._),
				mock => mock.BasicAck(3ul),
				mock => mock.BasicNack(4ul, A<bool>._));

			setup.AssertAllMessagesWereAcked();
		}

		[Test]
		public async Task TestOverlappingMessages()
		{
			var setup = GivenSetup();

			var firstMessageProcessed = setup.Awaiter.WaitForMessage(m => m.Id == 1);

			var thirdMessageProcessed = setup.Awaiter.WaitForMessage(m => m.Id == 3);

			setup.PublishMessages(
				"{ id: 1, bar: \"baz\" }",
				"{ id: 2, bar: \"baz\" }",
				"{ id: 3, bar: \"baz\" }",
				"{ id: 4, bar: \"baz\" }");

			await firstMessageProcessed;
			await thirdMessageProcessed;

			setup.ProcessedMessages.IsTrue(messages =>
				messages.Count == 2
				&& messages.Any(m => m.Id == 1)
				&& messages.Any(m => m.Id == 3));

			await setup.VerifyConsumerCalls(
				mock => mock.BasicAck(1ul),
				mock => mock.BasicNack(2ul, A<bool>._),
				mock => mock.BasicAck(3ul),
				mock => mock.BasicNack(4ul, A<bool>._));

			setup.AssertAllMessagesWereAcked();
		}

		[Test]
		public async Task TestDuplicateMessages()
		{
			var setup = GivenSetup();

			var messageProcessed = setup.Awaiter.WaitForMessage(m => m.Id == 1);

			setup.PublishMessages(
				"{ id: 1, bar: \"baz\" }",
				"{ id: 1, bar: \"zab\" }");

			var message = (await messageProcessed).IsTrue(m =>
					m.Id == 1
					&& m.Bar == "baz")
				.Value;

			setup.ProcessedMessages.IsTrue(messages => messages.Single() == message);

			await setup.VerifyConsumerCalls(
				mock => mock.BasicAck(1ul),
				mock => mock.BasicNack(2ul, A<bool>._));
		}

		[Test]
		public async Task TestSequentialAwaits()
		{
			var setup = GivenSetup();

			var firstMessageProcessed = setup.Awaiter.WaitForMessage(m => m.Id == 1);

			setup.PublishMessage("{ id: 1, bar: \"baz\" }");

			await firstMessageProcessed;

			await setup.VerifyConsumerCalls(mock => mock.BasicAck(1ul));

			setup.PublishMessage("{ id: -1, bar: \"unseen\" }");

			var secondMessageProcessed = setup.Awaiter.WaitForMessage(m => m.Id == 2);

			setup.PublishMessage("{ id: 2, bar: \"baz\" }");

			await secondMessageProcessed;

			await setup.VerifyConsumerCalls(mock => mock.BasicAck(2ul));

			setup.ProcessedMessages.IsTrue(m =>
				m.Count == 2
				&& m[0].Id == 1
				&& m[1].Id == 2);

			setup.AssertAllMessagesWereAcked();
		}

		[Test, Timeout(10000)]
		public async Task TestSequentialFailures()
		{
			var setup = GivenSetup(shortTimeout: true);

			using (var a = new TestExecutionContext.IsolatedContext())
			{
				var firstMessageProcessed = setup.Awaiter.WaitForMessage(m => m.Id == 2);

				setup.PublishMessage("{ id: 1, bar: \"baz\" }");

				Assert.ThrowsAsync<AssertionException>(async () => await firstMessageProcessed);

				await setup.VerifyConsumerCalls(mock => mock.BasicNack(1ul, true));

				var secondMessageProcessed = setup.Awaiter.WaitForMessage(m => m.Id == 3);

				Assert.ThrowsAsync<AssertionException>(async () => await secondMessageProcessed);
			}

			await setup.VerifyConsumerCalls();

			setup.ProcessedMessages.IsTrue(m => m.Count == 0);
		}

		[Test, Timeout(10000)]
		public async Task TestSequentialLongProcessing()
		{
			var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			var setup = GivenSetup(processMessage: m => m.Id == 1 ? tcs.Task : Task.CompletedTask);

			var firstMessageProcessed = setup.Awaiter.WaitForMessage(m => m.Id == 1);

			setup.PublishMessages(
				"{ id: -1, bar: \"baz\" }",
				"{ id: 1, bar: \"baz\" }");

			await setup.VerifyConsumerCalls(mock => mock.BasicNack(1ul, A<bool>._));

			var secondMessageProcessed = setup.Awaiter.WaitForMessage(m => m.Id == 2);

			setup.PublishMessage("{ id: 2, bar: \"baz\" }");

			await secondMessageProcessed;

			await setup.VerifyConsumerCalls(mock => mock.BasicAck(3ul));

			tcs.SetResult();

			await firstMessageProcessed;

			await setup.VerifyCalls(mock => mock.BasicAck(2ul));
		}

		[Test, Timeout(10000), ExpectedMessage(@"Expected:
	messages.First(m => m.Id == 1)

Actual:
	messages = [{ ""id"": 2, ""bar"": ""baz"" }]

Context:
	timeout = ""50 milliseconds""
	messageCount = 1
	context = ""present""
	timeoutReason = ""unacked message""

System.InvalidOperationException: Sequence contains no matching element", expectStackTrace: true)]
		public async Task TestMismatchedMessage()
		{
			var setup = GivenSetup(shortTimeout: true);

			var messageProcessed = setup.Awaiter.WaitForMessage(m => m.Id == 1);

			setup.PublishMessage("{ id: 2, bar: \"baz\" }");

			// necessary to ensure we hit the "consumer timeout" message not the "awaiter timeout" message,
			// just so the error message is consistent.
			await setup.VerifyConsumerCalls(mock => mock.BasicNack(1ul, true));

			try
			{
				await messageProcessed;
			}
			finally
			{
				setup.ProcessedMessages.IsTrue(m => !m.Any());
			}
		}

		[Test, Timeout(10000), ExpectedMessage(@"Expected:
	messages.First(m => m.Id == 1)

Actual:
	messages = []

Context:
	malformedMessages = [""garbage""]
	timeout = ""50 milliseconds""
	messageCount = 1
	context = ""present""
	timeoutReason = ""unacked message""

System.InvalidOperationException: Sequence contains no matching element", expectStackTrace: true)]
		public async Task TestMalformedMessage()
		{
			var setup = GivenSetup(shortTimeout: true);

			var messageProcessed = setup.Awaiter.WaitForMessage(m => m.Id == 1);

			setup.PublishMessage("garbage");

			// necessary to ensure we hit the "consumer timeout" message not the "awaiter timeout" message,
			// just so the error message is consistent.
			await setup.VerifyConsumerCalls(mock => mock.BasicNack(1ul, true));

			try
			{
				await messageProcessed;
			}
			finally
			{
				setup.ProcessedMessages.IsTrue(m => !m.Any());
			}
		}

		[Test, Timeout(10000), ExpectedMessage(@"Expected:
	messages.First(m => m.Id == 2)

Actual:
	messages = [{ ""id"": 1, ""bar"": ""baz"" }]

Context:
	timeout = ""50 milliseconds""
	messageCount = 1
	context = ""present""
	timeoutReason = ""unacked message""

System.InvalidOperationException: Sequence contains no matching element", expectStackTrace: true)]
		public async Task TestConsumerTimeout()
		{
			var setup = GivenSetup(shortTimeout: true);

			var messageProcessed = setup.Awaiter.WaitForMessage(m => m.Id == 2);

			setup.PublishMessage("{ id: 1, bar: \"baz\" }");

			// Wait until after the consumer shuts down.
			await setup.VerifyConsumerCalls(mock => mock.BasicNack(1ul, true));

			// Does nothing.
			setup.PublishMessage("{ id: 2, bar: \"baz\" }");

			try
			{
				await messageProcessed;
			}
			finally
			{
				await setup.VerifyCalls();

				setup.ProcessedMessages.IsTrue(m => !m.Any());
			}
		}

		[Test]
		public void TestConstructorDoesNotCreateConsumer()
		{
			var mock = A.Fake<IRabbitMqWrapper>();

			var unused = new MessageProcessedAwaiter<FooDto>(
				new { },
				_ => Task.CompletedTask,
				new MessageProcessedSettings(),
				mock);

			A.CallTo(mock).MustNotHaveHappened();
		}

		[Test, Timeout(10000), ExpectedMessage(@"Expected:
	messages.First(m => true)

Actual:
	messages = []

Context:
	timeout = ""50 milliseconds""
	messageCount = 0
	context = ""present""
	timeoutReason = ""after `await`""

System.InvalidOperationException: Sequence contains no matching element", expectStackTrace: true)]
		public async Task TestMultipleAwaitersForSameMessage()
		{
			var setup = GivenSetup(shortTimeout: true);

			var firstMessageProcessed = setup.Awaiter.WaitForMessage(m => m.Id == 1);
			var secondMessageProcessed = setup.Awaiter.WaitForMessage(m => true);

			setup.PublishMessage("{ id: 1, bar: \"baz\" }");

			await firstMessageProcessed;

			try
			{
				await secondMessageProcessed;
			}
			finally
			{
				await setup.VerifyConsumerCalls(model => model.BasicAck(1ul));

				setup.ProcessedMessages.IsTrue(m => m.Single().Id == 1);
			}
		}

		[Test]
		public async Task TestDispose()
		{
			var setup = GivenSetup();

			setup.Awaiter.Dispose();

			await setup.VerifyCalls(model => model.Dispose());
		}

		[Test]
		public async Task TestDisposeAfterMessage()
		{
			var setup = GivenSetup();

			var messageProcessed = setup.Awaiter.WaitForMessage(m => true);

			setup.PublishMessage("{ id: 1, bar: \"baz\" }");

			await messageProcessed;

			setup.Awaiter.Dispose();

			await setup.VerifyConsumerCalls(
				model => model.BasicAck(1ul),
				model => model.Dispose());
		}

		[Test]
		public async Task TestDisposeBeforeAwait()
		{
			var setup = GivenSetup();

			var messageProcessed = setup.Awaiter.WaitForMessage(m => true);

			setup.Awaiter.Dispose();

			Assert.ThrowsAsync<TaskCanceledException>(async () => await messageProcessed);

			await setup.VerifyConsumerCalls(model => model.Dispose());
		}

		private static Setup GivenSetup(bool shortTimeout = false, Func<FooDto, Task> processMessage = null)
		{
			var mock = A.Fake<IRabbitMqWrapper>(x => x.Strict(StrictFakeOptions.AllowToString));

			var mutex = new object();
			var deliveryTag = 0ul;
			Action<ulong, string> onRecievedWrapper = null;
			var processedMessages = new List<FooDto>();
			var observedTags = new HashSet<ulong>();
			var isDisposed = false;
			var consumers = new List<(string Tag, Task Cancelled)>();

			A.CallTo(() => mock.StartConsumer(A<string>._, A<Action<ulong, string>>._, A<Action>._)).Invokes((string consumerTag, Action<ulong, string> onRecieved, Action onClose) =>
			{
				lock (mutex)
				{
					if (isDisposed)
						throw new ObjectDisposedException(nameof(IRabbitMqWrapper));

					var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
					onRecievedWrapper = onRecieved;
					consumers.Add((consumerTag, tcs.Task));

					A.CallTo(() => mock.BasicCancel(consumerTag)).Invokes(_ =>
					{
						lock (mutex)
						{
							if (isDisposed)
								throw new ObjectDisposedException(nameof(IRabbitMqWrapper));

							try
							{
								onClose();
								tcs.TrySetResult();
							}
							catch (Exception e)
							{
								tcs.TrySetException(e);
							}

							onRecievedWrapper = null;
						}
					});
				}
			})
				.Returns(Task.CompletedTask);

			A.CallTo(() => mock.BasicNack(A<ulong>._, A<bool>._)).Invokes((ulong dt, bool multiple) =>
			{
				lock (mutex)
				{
					if (isDisposed)
						throw new ObjectDisposedException(nameof(IRabbitMqWrapper));

					if (dt > deliveryTag)
						throw new InvalidOperationException("Unexpected large delivery tag");

					if (!observedTags.Add(dt))
						throw new InvalidOperationException($"Duplicate nack for delivery tag: {dt}");

					if (multiple)
					{
						for (; dt > 0ul; dt--)
							observedTags.Add(dt);
					}
				}
			});

			A.CallTo(() => mock.BasicAck(A<ulong>._)).Invokes((ulong dt) =>
			{
				lock (mutex)
				{
					if (isDisposed)
						throw new ObjectDisposedException(nameof(IRabbitMqWrapper));

					if (dt > deliveryTag)
						throw new InvalidOperationException("Unexpected large delivery tag");

					if (!observedTags.Add(dt))
						throw new InvalidOperationException($"Duplicate ack for delivery tag: {dt}");
				}
			});

			A.CallTo(() => mock.Dispose()).Invokes(() =>
			{
				lock (mutex)
				{
					if (isDisposed)
						throw new ObjectDisposedException(nameof(IRabbitMqWrapper));
					isDisposed = true;
				}
			});

			return new Setup(mock, consumers)
			{
				ProcessedMessages = AssertEx.HasValue(() => processedMessages),
				PublishMessagesCore = messages =>
				{
					lock (mutex)
					{
						foreach (var m in messages)
							onRecievedWrapper?.Invoke(++deliveryTag, m);
					}
				},
				Awaiter = new MessageProcessedAwaiter<FooDto>(
					new { context = "present" },
					m =>
					{
						lock (mutex)
							processedMessages.Add(m);

						return processMessage != null
							? processMessage(m)
							: Task.CompletedTask;
					},
					new MessageProcessedSettings { TimeoutMilliseconds = shortTimeout ? 50 : 5_000 },
					mock),
				AssertAllMessagesWereAcked = () => AssertEx.IsTrue(() => deliveryTag == (ulong) observedTags.Count),
			};
		}

		private sealed class Setup
		{
			public Setup(IRabbitMqWrapper mock, List<(string Tag, Task Cancelled)> consumers)
			{
				m_mock = mock;
				m_consumers = consumers;
			}

			public void PublishMessage(string message) => PublishMessagesCore(new[] { message });
			public void PublishMessages(params string[] messages) => PublishMessagesCore(messages );

			public Action<IEnumerable<string>> PublishMessagesCore { get; set; }
			public MessageProcessedAwaiter<FooDto> Awaiter { get; set; }
			public Assertable<List<FooDto>> ProcessedMessages { get; set; }
			public Action AssertAllMessagesWereAcked { get; set; }

			public async Task VerifyConsumerCalls(params Expression<Action<IRabbitMqWrapper>>[] calls)
			{
				m_consumerCount++;
				m_expectedCallCount += 2;

				A.CallTo(() => m_mock.StartConsumer(A<string>._, A<Action<ulong, string>>._, A<Action>._)).MustHaveHappened(m_consumerCount, Times.Exactly);

				await VerifyCalls(calls);

				foreach (var consumer in m_consumers)
					A.CallTo(() => m_mock.BasicCancel(consumer.ConsumerTag)).MustHaveHappenedOnceExactly();
			}

			public async Task VerifyCalls(params Expression<Action<IRabbitMqWrapper>>[] calls)
			{
				await Task.WhenAll(m_consumers.Select(c => c.Cancelled));

				foreach (var call in calls)
					A.CallTo(Expression.Lambda<Action>(ExpressionHelper.ReplaceParameters(call, Expression.Constant(m_mock)))).MustHaveHappenedOnceExactly();

				m_expectedCallCount += calls.Length;
				A.CallTo(m_mock).MustHaveHappened(m_expectedCallCount, Times.Exactly);
			}

			private readonly IRabbitMqWrapper m_mock;
			private readonly List<(string ConsumerTag, Task Cancelled)> m_consumers;

			private int m_consumerCount;
			private int m_expectedCallCount;
		}

		private sealed class FooDto
		{
			public int Id { get; set; }
			public string Bar { get; set; }
		}
	}
}
