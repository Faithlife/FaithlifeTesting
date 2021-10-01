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
			var (awaiter, processedMessages, verify) = GivenMessages("{ id: 1, bar: \"baz\" }");

			var assertableMessage = await awaiter
				.WaitForMessage(m => m.Id == 1);

			assertableMessage.IsTrue(m =>
				m.Id == 1
				&& m.Bar == "baz"
				&& m == processedMessages.Single());

			await verify(mock => mock.Verify(r => r.BasicAck(1ul)));
		}

		[Test]
		public async Task TestProcessMessageFailure()
		{
			const string message = "Failure to obtain distributed lock";

			var (awaiter, _, verify) = GivenSetup(
				new[] { "{ id: 1, bar: \"baz\" }" },
				_ => throw new InvalidOperationException(message));

			var messageProcessed = awaiter
				.WaitForMessage(m => m.Id == 1);

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
			var (awaiter, processedMessages, verify) = GivenMessages(
				"{ id: 1, bar: \"baz\" }",
				"{ id: 2, bar: \"baz\" }",
				"{ id: 3, bar: \"baz\" }",
				"{ id: 4, bar: \"baz\" }");

			var assertableMessage = await awaiter
				.WaitForMessage(m => m.Id == 3);

			assertableMessage.IsTrue(m =>
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

		[Test, ExpectedMessage(@"Expected:
	messages.First(m => m.Id == 1)

Actual:
	messages = []

Context:
	messageCount = 0
	context = ""present""
	timeoutSeconds = 0

System.InvalidOperationException: Sequence contains no matching element", expectStackTrace: true)]
		public async Task TestNoMessages()
		{
			var (awaiter, processedMessages, verify) = GivenMessages();

			var messageProcessed = awaiter.WaitForMessage(m => m.Id == 1);

			try
			{
				await messageProcessed;
			}
			finally
			{
				AssertEx.IsTrue(() => !processedMessages.Any());

				await verify(_ => { });
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
			var (awaiter, processedMessages, verify) = GivenMessages("{ id: 2, bar: \"baz\" }");

			var messageProcessed = awaiter.WaitForMessage(m => m.Id == 1);

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
			var (awaiter, processedMessages, verify) = GivenMessages("garbage");

			var messageProcessed = awaiter.WaitForMessage(m => m.Id == 1);

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

		private static (MessageProcessedAwaiter<FooDto> Awaiter, List<FooDto> ProcessedMessages, Func<Action<Mock<IRabbitMqWrapper>>, Task> Verify) GivenMessages(params string[] messages)
			=> GivenSetup(messages, null);

		private static (MessageProcessedAwaiter<FooDto> Awaiter, List<FooDto> ProcessedMessages, Func<Action<Mock<IRabbitMqWrapper>>, Task> Verify) GivenSetup(IEnumerable<string> messages, Action<FooDto> processMessage = null)
		{
			var mock = new Mock<IRabbitMqWrapper>();
			var tcs = new TaskCompletionSource<object>();
			var deliveryTag = 0ul;

			mock.Setup(r => r.StartConsumer(It.IsAny<string>(), It.IsAny<Action<ulong, string>>(), It.IsAny<Action>()))
				.Callback<string, Action<ulong, string>, Action>((consumerTag, _, onClose) => mock
					.Setup(r => r.BasicCancel(consumerTag))
					.Callback(() =>
					{
						onClose();
						tcs.TrySetResult(null);
					})
					.Verifiable())
				.Returns(Task.CompletedTask)
				.Callback<string, Action<ulong, string>, Action>(
					(_, onRecieved, _) =>
					{
						foreach (var m in messages)
							onRecieved(++deliveryTag, m);
					})
				.Verifiable();
			var processedMessages = new List<FooDto>();

			return (new MessageProcessedAwaiter<FooDto>(
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
