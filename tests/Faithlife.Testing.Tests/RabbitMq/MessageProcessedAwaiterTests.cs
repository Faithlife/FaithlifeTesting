using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Faithlife.Testing.RabbitMq;
using Moq;
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

			await setup.Verify(mock => mock.Verify(r => r.BasicAck(1ul)));
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
				await setup.Verify(_ =>
				{
					if (isPublishedBeforeWaitForMessage == false)
						setup.PublishMessage("{ id: 1, bar: \"baz\" }");
				});

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

			await setup.Verify(mock => mock.Verify(r => r.BasicNack(1ul, It.IsAny<bool>())));

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

			await setup.Verify(mock => mock.Verify(r => r.BasicNack(1ul, It.IsAny<bool>())));

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

			await setup.Verify(mock => mock.Verify(r => r.BasicNack(1ul, It.IsAny<bool>())));
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

			await setup.Verify(mock =>
			{
				mock.Verify(r => r.BasicNack(It.IsIn(2ul, 4ul), It.IsAny<bool>()));
				mock.Verify(r => r.BasicAck(3ul));
				mock.Verify(r => r.BasicNack(4ul, It.IsAny<bool>()));
			});

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

			await setup.Verify(mock =>
			{
				mock.Verify(r => r.BasicAck(1ul));
				mock.Verify(r => r.BasicAck(3ul));
				mock.Verify(r => r.BasicNack(It.IsIn(2ul, 4ul), It.IsAny<bool>()));
				mock.Verify(r => r.BasicNack(4ul, It.IsAny<bool>()));
			});

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

			await setup.Verify(mock =>
			{
				mock.Verify(r => r.BasicAck(1ul));
				mock.Verify(r => r.BasicNack(2ul, It.IsAny<bool>()));
			});
		}

		[Test]
		public async Task TestSequentialAwaits()
		{
			var setup = GivenSetup();

			var firstMessageProcessed = setup.Awaiter.WaitForMessage(m => m.Id == 1);

			setup.PublishMessage("{ id: 1, bar: \"baz\" }");

			await firstMessageProcessed;

			await setup.Verify(mock => mock.Verify(r => r.BasicAck(1ul)));

			setup.PublishMessage("{ id: -1, bar: \"unseen\" }");

			var secondMessageProcessed = setup.Awaiter.WaitForMessage(m => m.Id == 2);

			setup.PublishMessage("{ id: 2, bar: \"baz\" }");

			await secondMessageProcessed;

			await setup.Verify(mock =>
			{
				mock.Verify(r => r.BasicAck(2ul));

				mock.Verify(r => r.StartConsumer(It.IsAny<string>(), It.IsAny<Action<ulong, string>>(), It.IsAny<Action>()), Times.Exactly(2));
				mock.Verify(r => r.BasicCancel(It.IsAny<string>()), Times.Exactly(2));
			});

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

				await setup.Verify(mock => mock.Verify(r => r.BasicNack(1ul, true)));

				var secondMessageProcessed = setup.Awaiter.WaitForMessage(m => m.Id == 3);

				Assert.ThrowsAsync<AssertionException>(async () => await secondMessageProcessed);
			}

			await setup.Verify(mock =>
			{
				mock.Verify(r => r.BasicNack(1ul, It.IsAny<bool>()), Times.Once);

				mock.Verify(r => r.StartConsumer(It.IsAny<string>(), It.IsAny<Action<ulong, string>>(), It.IsAny<Action>()), Times.Exactly(2));
				mock.Verify(r => r.BasicCancel(It.IsAny<string>()), Times.Exactly(2));
			});

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

			await setup.Verify(mock => mock.Verify(r => r.BasicNack(1ul, It.IsAny<bool>())));

			var secondMessageProcessed = setup.Awaiter.WaitForMessage(m => m.Id == 2);

			setup.PublishMessage("{ id: 2, bar: \"baz\" }");

			await secondMessageProcessed;

			await setup.Verify(mock => mock.Verify(r => r.BasicAck(3ul)));

			tcs.SetResult();

			await firstMessageProcessed;

			await setup.Verify(mock => mock.Verify(r => r.BasicAck(2ul)));
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
			await setup.Verify(mock => mock.Verify(r => r.BasicNack(1ul, true)));

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
			await setup.Verify(mock => mock.Verify(r => r.BasicNack(1ul, true)));

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
			await setup.Verify(mock => mock.Verify(r => r.BasicNack(1ul, true)));

			// Does nothing.
			setup.PublishMessage("{ id: 2, bar: \"baz\" }");

			try
			{
				await messageProcessed;
			}
			finally
			{
				await setup.Verify(_ => { });

				setup.ProcessedMessages.IsTrue(m => !m.Any());
			}
		}

		[Test]
		public void TestConstructorDoesNotCreateConsumer()
		{
			var mock = new Mock<IRabbitMqWrapper>();

			var unused = new MessageProcessedAwaiter<FooDto>(
				new { },
				_ => Task.CompletedTask,
				new MessageProcessedSettings(),
				mock.Object);

			mock.VerifyNoOtherCalls();
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
				await setup.Verify(model => model.Verify(r => r.BasicAck(1ul)));

				setup.ProcessedMessages.IsTrue(m => m.Single().Id == 1);
			}
		}

		[Test]
		public async Task TestDispose()
		{
			var setup = GivenSetup(noStart: true);

			setup.Awaiter.Dispose();

			await setup.Verify(model => model.Verify(r => r.Dispose(), Times.Once));
		}

		[Test]
		public async Task TestDisposeAfterMessage()
		{
			var setup = GivenSetup();

			var messageProcessed = setup.Awaiter.WaitForMessage(m => true);

			setup.PublishMessage("{ id: 1, bar: \"baz\" }");

			await messageProcessed;

			setup.Awaiter.Dispose();

			await setup.Verify(
				model =>
				{
					model.Verify(r => r.BasicAck(1ul));
					model.Verify(r => r.Dispose(), Times.Once);
					model.Verify(r => r.BasicCancel(It.IsAny<string>()), Times.Once);
				});
		}

		[Test]
		public async Task TestDisposeBeforeAwait()
		{
			var setup = GivenSetup();

			var messageProcessed = setup.Awaiter.WaitForMessage(m => true);

			setup.Awaiter.Dispose();

			Assert.ThrowsAsync<TaskCanceledException>(async () => await messageProcessed);

			await setup.Verify(
				model =>
				{
					model.Verify(r => r.Dispose(), Times.Once);
					model.Verify(r => r.BasicCancel(It.IsAny<string>()), Times.Once);
				});
		}

		private static Setup GivenSetup(bool shortTimeout = false, Func<FooDto, Task> processMessage = null, bool noStart = false)
		{
			var mock = new Mock<IRabbitMqWrapper>();

			var mutex = new object();
			var deliveryTag = 0ul;
			TaskCompletionSource tcs = null;
			Action<ulong, string> onRecievedWrapper = null;
			var processedMessages = new List<FooDto>();
			var observedTags = new HashSet<ulong>();
			var isDisposed = false;

			if (!noStart)
			{
				mock.Setup(r => r.StartConsumer(It.IsAny<string>(), It.IsAny<Action<ulong, string>>(), It.IsAny<Action>()))
					.Callback<string, Action<ulong, string>, Action>(
						(consumerTag, onRecieved, onClose) =>
						{
							lock (mutex)
							{
								if (isDisposed)
									throw new ObjectDisposedException(nameof(IRabbitMqWrapper));

								tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
								onRecievedWrapper = onRecieved;
								mock
									.Setup(r => r.BasicCancel(consumerTag))
									.Callback(
										() =>
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
										})
									.Verifiable();
							}
						})
					.Returns(Task.CompletedTask)
					.Verifiable();
			}

			mock.Setup(r => r.BasicNack(It.IsAny<ulong>(), It.IsAny<bool>()))
				.Callback<ulong, bool>(
					(dt, multiple) =>
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

			mock.Setup(r => r.BasicAck(It.IsAny<ulong>()))
				.Callback<ulong>(
					dt =>
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

			mock.Setup(r => r.Dispose())
				.Callback(
					() =>
					{
						lock (mutex)
						{
							if (isDisposed)
								throw new ObjectDisposedException(nameof(IRabbitMqWrapper));
							isDisposed = true;
						}
					});

			return new Setup
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
					new LockingWrapper(mock.Object)),
				Verify = async verify =>
				{
					if (tcs is not null)
						await tcs.Task;

					verify(mock);

					mock.Verify();
					mock.VerifyNoOtherCalls();
				},
				AssertAllMessagesWereAcked = () => AssertEx.IsTrue(() => deliveryTag == (ulong) observedTags.Count),
			};
		}

		private sealed class Setup
		{
			public void PublishMessage(string message) => PublishMessagesCore(new[] { message });
			public void PublishMessages(params string[] messages) => PublishMessagesCore(messages );

			public Action<IEnumerable<string>> PublishMessagesCore { get; set; }
			public MessageProcessedAwaiter<FooDto> Awaiter { get; set; }
			public Assertable<List<FooDto>> ProcessedMessages { get; set; }
			public Func<Action<Mock<IRabbitMqWrapper>>, Task> Verify { get; set; }
			public Action AssertAllMessagesWereAcked { get; set; }
		}

		private sealed class FooDto
		{
			public int Id { get; set; }
			public string Bar { get; set; }
		}

		// Moq's internals get *very* confused when called from multiple threads.
		private sealed class LockingWrapper : IRabbitMqWrapper
		{
			public LockingWrapper(IRabbitMqWrapper inner)
			{
				m_inner = inner;
			}

			public void Dispose()
			{
				lock (m_lock)
					m_inner.Dispose();
			}

			public Task StartConsumer(string consumerTag, Action<ulong, string> onReceived, Action onCancelled)
			{
				lock (m_lock)
					return m_inner.StartConsumer(consumerTag, onReceived, onCancelled);
			}

			public void BasicAck(ulong deliveryTag)
			{
				lock (m_lock)
					m_inner.BasicAck(deliveryTag);
			}

			public void BasicNack(ulong deliveryTag, bool multiple)
			{
				lock (m_lock)
					m_inner.BasicNack(deliveryTag, multiple);
			}

			public void BasicCancel(string consumerTag)
			{
				lock (m_lock)
					m_inner.BasicCancel(consumerTag);
			}

			private readonly IRabbitMqWrapper m_inner;
			private readonly object m_lock = new();
		}
	}
}
