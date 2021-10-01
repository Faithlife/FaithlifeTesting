using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Faithlife.Testing.RabbitMq;
using Moq;
using NUnit.Framework;

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
				await setup.Verify(
					_ =>
					{
						if (isPublishedBeforeWaitForMessage == false)
							setup.PublishMessage("{ id: 1, bar: \"baz\" }");
					});

				setup.ProcessedMessages.IsTrue(m => !m.Any());
			}
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

		private static Setup GivenSetup(bool shortTimeout = false, Action<FooDto> processMessage = null)
		{
			var mock = new Mock<IRabbitMqWrapper>();
			TaskCompletionSource<object> tcs = null;
			var deliveryTag = 0ul;
			Action<ulong, string> onRecievedWrapper = null;

			var mutex = new object();
			var processedMessages = new List<FooDto>();

			mock.Setup(r => r.StartConsumer(It.IsAny<string>(), It.IsAny<Action<ulong, string>>(), It.IsAny<Action>()))
				.Callback<string, Action<ulong, string>, Action>((consumerTag, onRecieved, onClose) =>
				{
					tcs = new TaskCompletionSource<object>();
					onRecievedWrapper = onRecieved;
					mock
						.Setup(r => r.BasicCancel(consumerTag))
						.Callback(() =>
						{
							try
							{
								onClose();
								tcs.TrySetResult(null);
							}
							catch (Exception e)
							{
								tcs.TrySetException(e);
							}

							onRecievedWrapper = null;
						})
						.Verifiable();
				})
				.Returns(Task.CompletedTask)
				.Verifiable();

			return new Setup
			{
				ProcessedMessages = AssertEx.HasValue(() => processedMessages),
				PublishMessagesCore = messages =>
				{
					foreach (var m in messages)
						onRecievedWrapper?.Invoke(++deliveryTag, m);
				},
				Awaiter = new MessageProcessedAwaiter<FooDto>(
					new { context = "present" },
					m =>
					{
						lock (mutex)
							processedMessages.Add(m);

						processMessage?.Invoke(m);
						return Task.CompletedTask;
					},
					new MessageProcessedSettings { TimeoutMilliseconds = shortTimeout ? 50 : 5_000 },
					new LockingWrapper(mock.Object)),
				Verify = async verify =>
				{
					await tcs.Task;

					verify(mock);

					mock.Verify();
					mock.VerifyNoOtherCalls();
				},
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
