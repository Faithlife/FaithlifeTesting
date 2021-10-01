using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Faithlife.Testing.RabbitMq
{
	/// <summary>
	/// Utility for asserting that a message gets published.
	///
	/// Creates a new queue bound to the specified server, exchange, and routingKey;
	/// waits for a message to be published matching a specified predicate;
	/// and allows chaning further assertions on the matched message.
	/// </summary>
	public sealed class MessagePublishedAwaiter<TMessage> : IDisposable
		where TMessage : class
	{
		public MessagePublishedAwaiter(string serverName, string exchangeName, string routingKeyName)
			: this(serverName, exchangeName, routingKeyName, TimeSpan.FromMilliseconds(5000))
		{
		}

		public MessagePublishedAwaiter(string serverName, string exchangeName, string routingKeyName, TimeSpan timeout)
		{
			m_timeout = timeout;
			m_context = new
			{
				exchange = $"http://{serverName}:15672/#/exchanges/%2f/{exchangeName}",
				routingKey = routingKeyName,
			};

			var queueName = $"{exchangeName}_{routingKeyName}_awaiter_{Environment.MachineName}_{Guid.NewGuid():N}";

			m_rabbitMq = new RabbitMqWrapper(serverName, queueName, priority: 0, autoAck: true, onError: e => m_exception = e, setup: model =>
			{
				model.QueueDeclare(
					queue: queueName,
					durable: false,
					exclusive: true,
					autoDelete: true,
					arguments: new Dictionary<string, object> { { "x-ha-policy", "all" } });

				model.QueueBind(queueName, exchangeName, routingKeyName, null);
			});

			var messages = Channel.CreateUnbounded<string>();

			m_messages = messages.Reader;

			// Wait synchronously for the consumer to start before returning.
			m_rabbitMq.StartConsumer(
				consumerTag: null,
				onReceived: (_, message) => messages.Writer.TryWrite(message),
				onCancelled: () => { })
				.GetAwaiter()
				.GetResult();

			Task.Run(SubscriberLoop, m_cancellationTokenSource.Token);
		}

		public LazyTask<Assertable<TMessage>> WaitForMessage(Expression<Func<TMessage, bool>> predicateExpression)
		{
			var awaiter = new MessageAwaiter<TMessage>(m_context, predicateExpression ?? throw new ArgumentNullException(nameof(predicateExpression)));

			lock (m_lock)
			{
				if (m_exception != null)
					throw m_exception;

				m_awaiters.Add(awaiter);
			}

			return new LazyTask<Assertable<TMessage>>(async () =>
			{
				// LazyTask ensures that the timeout begins ticking once we start awaiting, not when first registering the awaiter.
				// delayMilliseconds is not a `const` so that `AssertEx` can capture its name.
				var result = awaiter.Completion.Task;
				await Task.WhenAny(result, Task.Delay(m_timeout));

				lock (m_lock)
					m_awaiters.Remove(awaiter);

				if (result.IsCompleted)
					return result.Result;

				awaiter.AssertTimeoutFailure((int) m_timeout.TotalMilliseconds);

				throw new InvalidOperationException("Multiple Assertions not supported.");
			});
		}

		public void Dispose()
		{
			lock (m_lock)
			{
				m_exception = new ObjectDisposedException(nameof(MessagePublishedAwaiter<TMessage>));

				m_cancellationTokenSource.Cancel();

				foreach (var awaiter in m_awaiters)
					awaiter.Completion.TrySetCanceled(m_cancellationTokenSource.Token);

				m_cancellationTokenSource.Dispose();
				m_rabbitMq.Dispose();
			}
		}

		private async Task SubscriberLoop()
		{
			try
			{
				while (!m_cancellationTokenSource.Token.IsCancellationRequested && (await m_messages.WaitToReadAsync(m_cancellationTokenSource.Token)))
				{
					while (m_messages.TryRead(out var body))
					{
						lock (m_lock)
						{
							MessageAwaiter<TMessage>.FirstMatch(m_awaiters, body)?.Complete();
						}
					}
				}
			}
			catch (Exception e)
			{
				m_exception = e;
			}
		}

		private readonly object m_lock = new();
		private readonly CancellationTokenSource m_cancellationTokenSource = new();

		private readonly TimeSpan m_timeout;
		private readonly IRabbitMqWrapper m_rabbitMq;
		private readonly ChannelReader<string> m_messages;
		private readonly object m_context;

		private readonly List<MessageAwaiter<TMessage>> m_awaiters = new();
		private Exception m_exception;
	}
}
