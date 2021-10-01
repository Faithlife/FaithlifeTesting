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
			var (publishMessage, awaiter, processedMessages, verify) = GivenSetup();

			var messagePublished = awaiter
				.WaitForMessage(m => m.Id == 1);

			publishMessage("{ id: 1, bar: \"baz\" }");

			(await messagePublished).IsTrue(m =>
				m.Id == 1
				&& m.Bar == "baz"
				&& m == processedMessages.Single());

			await verify(mock => mock.Verify(r => r.BasicAck(1ul)));
		}

		[Test]
		public async Task TestProcessMessageFailure()
		{
			const string message = "Failure to obtain distributed lock";

			var (publishMessage, awaiter, _, verify) = GivenSetup(_ => throw new InvalidOperationException(message));

			var messageProcessed = awaiter
				.WaitForMessage(m => m.Id == 1);

			publishMessage("{ id: 1, bar: \"baz\" }");

			Exception exception;
			try
			{
				await messageProcessed;

				exception = null;
			}
			catch (Exception ex)
			{
				exception = ex;
			}

			// Throws an AggregateException when run as a test suite, but InvalidOperationException when run by itself.
			AssertEx.IsTrue(() => exception.Message.Contains(message, StringComparison.InvariantCulture));

			await verify(mock => mock.Verify(r => r.BasicNack(1ul, It.IsAny<bool>())));
		}

		[Test]
		public async Task TestNacking()
		{
			var (publishMessage, awaiter, processedMessages, verify) = GivenSetup();

			var messagePublished = awaiter
				.WaitForMessage(m => m.Id == 3);

			publishMessage("{ id: 1, bar: \"baz\" }");
			publishMessage("{ id: 2, bar: \"baz\" }");
			publishMessage("{ id: 3, bar: \"baz\" }");
			publishMessage("{ id: 4, bar: \"baz\" }");

			(await messagePublished).IsTrue(m =>
				m.Id == 3
				&& m == processedMessages.Single());

			await verify(
				mock =>
				{
					mock.Verify(r => r.BasicNack(2ul, true));
					mock.Verify(r => r.BasicAck(3ul));
					mock.Verify(r => r.BasicNack(4ul, false));
				});
		}

		[Test]
		public async Task TestOverlappingMessages()
		{
			var (publishMessage, awaiter, processedMessages, verify) = GivenSetup();

			var firstMessagePublished = awaiter
				.WaitForMessage(m => m.Id == 1);

			var thirdMessagePublished = awaiter
				.WaitForMessage(m => m.Id == 3);

			publishMessage("{ id: 1, bar: \"baz\" }");

			publishMessage("{ id: 2, bar: \"baz\" }");

			publishMessage("{ id: 3, bar: \"baz\" }");

			publishMessage("{ id: 4, bar: \"baz\" }");

			await firstMessagePublished;
			await thirdMessagePublished;

			AssertEx.IsTrue(() => processedMessages.Count == 2
				&& processedMessages.Any(m => m.Id == 1)
				&& processedMessages.Any(m => m.Id == 3));

			await verify(
				mock =>
				{
					mock.Verify(r => r.BasicAck(1ul));
					mock.Verify(r => r.BasicNack(2ul, It.IsAny<bool>()));
					mock.Verify(r => r.BasicAck(3ul));
					mock.Verify(r => r.BasicNack(4ul, It.IsAny<bool>()));
				});
		}

		[Test]
		public async Task TestSequentialAwaits()
		{
			var (publishMessage, awaiter, processedMessages, verify) = GivenSetup();

			var firstMessagePublished = awaiter
				.WaitForMessage(m => m.Id == 1);

			publishMessage("{ id: 1, bar: \"baz\" }");

			await firstMessagePublished;

			await verify(mock => mock.Verify(r => r.BasicAck(1ul)));

			var secondMessagePublished = awaiter
				.WaitForMessage(m => m.Id == 2);

			publishMessage("{ id: 2, bar: \"baz\" }");

			await secondMessagePublished;

			await verify(mock =>
			{
				mock.Verify(r => r.BasicAck(2ul));

				mock.Verify(r => r.StartConsumer(It.IsAny<string>(), It.IsAny<Action<ulong, string>>(), It.IsAny<Action>()), Times.Exactly(2));
				mock.Verify(r => r.BasicCancel(It.IsAny<string>()), Times.Exactly(2));
			});

			AssertEx.IsTrue(() => processedMessages.Count == 2
				&& processedMessages[0].Id == 1
				&& processedMessages[1].Id == 2);
		}

		[Test, ExpectedMessage(@"Expected:
	messages.First(m => m.Id == 1)

Actual:
	messages = []

Context:
	messageCount = 0
	context = ""present""
	timeoutSeconds = 0

System.InvalidOperationException: Sequence contains no matching element", expectStackTrace: true)]
		public async Task TestNoMessages([Values] bool? isPublishedBeforeWaitForMessage)
		{
			var (publishMessage, awaiter, processedMessages, verify) = GivenSetup();

			if (isPublishedBeforeWaitForMessage == true)
				publishMessage("{ id: 1, bar: \"baz\" }");

			var messageProcessed = awaiter.WaitForMessage(m => m.Id == 1);

			try
			{
				await messageProcessed;
			}
			finally
			{
				await verify(
					_ =>
					{
						if (isPublishedBeforeWaitForMessage == false)
							publishMessage("{ id: 1, bar: \"baz\" }");
					});

				AssertEx.IsTrue(() => !processedMessages.Any());
			}
		}

		[Test, ExpectedMessage(@"Expected:
	messages.First(m => m.Id == 1)

Actual:
	messages = [{ ""id"": 2, ""bar"": ""baz"" }]

Context:
	messageCount = 1
	context = ""present""
	timeoutSeconds = 0

System.InvalidOperationException: Sequence contains no matching element", expectStackTrace: true)]
		public async Task TestMismatchedMessage()
		{
			var (publishMessage, awaiter, processedMessages, verify) = GivenSetup();

			var messageProcessed = awaiter.WaitForMessage(m => m.Id == 1);

			publishMessage("{ id: 2, bar: \"baz\" }");

			try
			{
				await messageProcessed;
			}
			finally
			{
				AssertEx.IsTrue(() => !processedMessages.Any());

				await verify(mock => mock.Verify(r => r.BasicNack(1ul, true)));
			}
		}

		[Test, ExpectedMessage(@"Expected:
	messages.First(m => m.Id == 1)

Actual:
	messages = []

Context:
	malformedMessages = [""garbage""]
	messageCount = 1
	context = ""present""
	timeoutSeconds = 0

System.InvalidOperationException: Sequence contains no matching element", expectStackTrace: true)]
		public async Task TestMalformedMessage()
		{
			var (publishMessage, awaiter, processedMessages, verify) = GivenSetup();

			var messageProcessed = awaiter.WaitForMessage(m => m.Id == 1);

			publishMessage("garbage");

			try
			{
				await messageProcessed;
			}
			finally
			{
				AssertEx.IsTrue(() => !processedMessages.Any());

				await verify(mock => mock.Verify(r => r.BasicNack(1ul, true)));
			}
		}

		private static (Action<string> PublishMessage, MessageProcessedAwaiter<FooDto> Awaiter, List<FooDto> ProcessedMessages, Func<Action<Mock<IRabbitMqWrapper>>, Task> Verify) GivenSetup(Action<FooDto> processMessage = null)
		{
			var mock = new Mock<IRabbitMqWrapper>();
			TaskCompletionSource<object> tcs = null;
			var deliveryTag = 0ul;
			Action<ulong, string> onRecievedWrapper = null;
			var processedMessages = new List<FooDto>();

			mock.Setup(r => r.StartConsumer(It.IsAny<string>(), It.IsAny<Action<ulong, string>>(), It.IsAny<Action>()))
				.Callback<string, Action<ulong, string>, Action>((consumerTag, onRecieved, onClose) =>
				{
					tcs = new TaskCompletionSource<object>();
					onRecievedWrapper = onRecieved;
					mock
						.Setup(r => r.BasicCancel(consumerTag))
						.Callback(
							() =>
							{
								onClose();
								tcs.TrySetResult(null);
							})
						.Verifiable();
				})
				.Returns(Task.CompletedTask)
				.Verifiable();

			return (
				m => onRecievedWrapper?.Invoke(++deliveryTag, m),
				new MessageProcessedAwaiter<FooDto>(
					new { context = "present" },
					m =>
					{
						processedMessages.Add(m);
						processMessage?.Invoke(m);
						return Task.CompletedTask;
					},
					new MessageProcessedSettings { TimeoutMilliseconds = 50 },
					mock.Object),
				processedMessages,
				async verify =>
				{
					await tcs.Task;

					verify(mock);

					mock.Verify();
					mock.VerifyNoOtherCalls();
				}
			);
		}

		private sealed class FooDto
		{
			public int Id { get; set; }
			public string Bar { get; set; }
		}
	}
}
