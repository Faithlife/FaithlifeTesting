using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Faithlife.Json;
using Faithlife.Utility;
using RabbitMQ.Client;
using RabbitMQ.Client.MessagePatterns;

namespace Faithlife.Testing.RabbitMq
{
	public sealed class MessagePublishedAwaiter<TMessage> : IDisposable
		where TMessage : class
	{
		public MessagePublishedAwaiter(string serverName, string exchangeName, string routingKeyName)
		{
			m_serverName = serverName;
			m_exchangeName = exchangeName;
			m_routingKeyName = routingKeyName;

			m_connection = new ConnectionFactory
			{
				HostName = m_serverName,
				RequestedHeartbeat = 30,
			}.CreateConnection();

			m_model = m_connection.CreateModel();

			var queueName = $"{exchangeName}_{routingKeyName}_awaiter_{Environment.MachineName}_{Guid.NewGuid().ToLowerNoDashString()}";
			string queue = m_model.QueueDeclare(
				queue: queueName,
				durable: false,
				exclusive: true,
				autoDelete: true,
				arguments: new Dictionary<string, object> { { "x-ha-policy", "all" } });

#pragma warning disable CS0618 // Type or member is obsolete
			m_subscription = new Subscription(m_model, queue, true);
#pragma warning restore CS0618 // Type or member is obsolete
			m_model.QueueBind(queue, exchangeName, routingKeyName);

			Task.Run(SubscriberLoop, m_cancellationTokenSource.Token);
		}

		public LazyTask<AssertEx.Builder<TMessage>> WaitForMessage(Expression<Func<TMessage, bool>> predicateExpression) => WaitForMessage(predicateExpression, TimeSpan.FromMilliseconds(5000));

		public LazyTask<AssertEx.Builder<TMessage>> WaitForMessage(Expression<Func<TMessage, bool>> predicateExpression, TimeSpan timeout)
		{
			var awaiter = new Awaiter(predicateExpression);

			lock (m_lock)
			{
				if (m_exception != null)
					throw m_exception;

				m_awaiters.Add(awaiter);
			}

			return new LazyTask<AssertEx.Builder<TMessage>>(async () =>
			{
				// LazyTask ensures that the timeout begins ticking once we start awaiting, not when first registering the awaiter.
				// delayMilliseconds is not a `const` so that `AssertEx` can capture its name.
				var result = awaiter.Completion.Task;
				await Task.WhenAny(result, Task.Delay(timeout));

				lock (m_lock)
					m_awaiters.Remove(awaiter);

				if (!result.IsCompleted)
				{
					var messageCount = awaiter.MessageCount;
					var messages = awaiter.Messages;
					var exchange = $"http://{m_serverName}:15672/#/exchanges/%2f/{m_exchangeName}";

					using (AssertEx.Context(new { messageCount, delayMilliseconds = timeout.TotalMilliseconds, exchange, m_routingKeyName }))
					{
						var param = Expression.Parameter(typeof(IReadOnlyCollection<TMessage>), "m");
						var body = Expression.Call(s_first.MakeGenericMethod(typeof(TMessage)), param, predicateExpression);

						AssertEx.Select(() => messages)
							.Select(Expression.Lambda<Func<IReadOnlyCollection<TMessage>, TMessage>>(body, param));
					}
				}

				var message = result.Result;
				return AssertEx.Select(() => message);
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
				((IDisposable) m_subscription).Dispose();
				m_model.Dispose();
				m_connection.Dispose();
			}
		}

		private void SubscriberLoop()
		{
			try
			{
				while (!m_cancellationTokenSource.Token.IsCancellationRequested)
				{
					if (m_subscription.Next(1000, out var result) && result != null && !(result.Body == null || result.Body.Length == 0))
					{
						var message = JsonUtility.FromJson<TMessage>(Encoding.UTF8.GetString(result.Body), s_jsonInputSettings);

						lock (m_lock)
						{
							foreach (var awaiter in m_awaiters)
								awaiter.CheckMessage(message);
						}

						m_subscription.Ack(result);
					}
				}
			}
			catch (Exception e)
			{
				m_exception = e;
			}
		}

		private sealed class Awaiter
		{
			public Awaiter(Expression<Func<TMessage, bool>> predicateExpression)
			{
				m_predicate = predicateExpression.Compile();
			}

			public TaskCompletionSource<TMessage> Completion { get; } = new TaskCompletionSource<TMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

			public int MessageCount { get; private set; }
			public IReadOnlyCollection<TMessage> Messages => m_messages.AsReadOnly();

			public void CheckMessage(TMessage message)
			{
				MessageCount++;

				if (m_messages.Count < c_messageLimit)
					m_messages.Add(message);

				if (m_predicate(message))
					Completion.TrySetResult(message);
			}

			private const int c_messageLimit = 10;

			private readonly Func<TMessage, bool> m_predicate;
			private readonly List<TMessage> m_messages = new List<TMessage>(c_messageLimit);
		}

		private static readonly JsonSettings s_jsonInputSettings = new JsonSettings { RejectsExtraProperties = false };

		private static readonly MethodInfo s_first = typeof(Enumerable).GetMethods(BindingFlags.Static | BindingFlags.Public).Single(
				m =>
				{
					var p = m.GetParameters();
					return m.Name == "First"
						&& p.Length == 2
						&& p[0].ParameterType.IsGenericType
						&& p[0].ParameterType.GetGenericTypeDefinition() == typeof(IEnumerable<>)
						&& p[1].ParameterType.IsGenericType
						&& p[1].ParameterType.GetGenericTypeDefinition() == typeof(Func<,>);
				});

		private readonly object m_lock = new object();
		private readonly CancellationTokenSource m_cancellationTokenSource = new CancellationTokenSource();

		private readonly string m_serverName;
		private readonly string m_exchangeName;
		private readonly string m_routingKeyName;
		private readonly IConnection m_connection;
		private readonly IModel m_model;
#pragma warning disable CS0618 // Type or member is obsolete
		private readonly Subscription m_subscription;
#pragma warning restore CS0618 // Type or member is obsolete

		private readonly List<Awaiter> m_awaiters = new List<Awaiter>();
		private Exception m_exception;
	}
}
