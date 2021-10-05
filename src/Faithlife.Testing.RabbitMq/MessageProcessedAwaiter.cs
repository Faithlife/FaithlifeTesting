using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Faithlife.Utility;

namespace Faithlife.Testing.RabbitMq
{
	/// <summary>
	/// Utility for testing the processing of a message.
	///
	/// Binds to an existing queue and "steals" messages from it using Consumer Priority.
	/// For messages matching a predicate, executes a `processMessage` callback and returns.
	/// All un-matched messages are returned to the existing queue and its consumers.
	/// </summary>
	public sealed class MessageProcessedAwaiter<TMessage> : IDisposable
		where TMessage : class
	{
		public MessageProcessedAwaiter(string serverName, string queueName, Func<TMessage, Task> processMessage, MessageProcessedSettings settings = null)
		{
			m_context = new { queue = $"http://{serverName}:15672/#/queues/%2f/{queueName}" };
			m_processMessage = processMessage;
			m_settings = settings ?? new MessageProcessedSettings();
			m_rabbitMq = new RabbitMqWrapper(serverName, queueName, m_settings.Priority, autoAck: false, onError: HandleSubscriberException, setup: model =>
			{
				model.BasicQos(prefetchSize: 0, prefetchCount: m_settings.PrefetchCount, global: false);
				model.QueueDeclarePassive(queueName);
			});
		}

		internal MessageProcessedAwaiter(object context, Func<TMessage, Task> processMessage, MessageProcessedSettings settings, IRabbitMqWrapper rabbitMq)
		{
			m_context = context;
			m_settings = settings;
			m_processMessage = processMessage;
			m_rabbitMq = rabbitMq;
		}

		public LazyTask<Assertable<TMessage>> WaitForMessage(Expression<Func<TMessage, bool>> predicateExpression)
		{
			var awaiter = new MessageAwaiter<TMessage>(
				m_context,
				predicateExpression ?? throw new ArgumentNullException(nameof(predicateExpression)));

			// Wait synchronously for the consumer to start
			// so it is guaranteed to observe all messages published after we return from this method.
			// We do not return `Task<LazyTask<Assertable<TMessage>>>` because:
			//  (a) that's confusing if you're not hyper-tuned into `async/await` semantics, and
			//  (b) the compiler won't warn you if you fail to `await` the `LazyTask`.
			AddAwaiter(awaiter).GetAwaiter().GetResult();

			// LazyTask ensures that the timeout begins ticking once we start awaiting, not when first registering the awaiter.
			return new LazyTask<Assertable<TMessage>>(async () =>
			{
				var result = awaiter.Completion.Task;

				await WithTimeout()(result);

				if (result.IsCompleted)
					return await result;

				bool shouldTimeOut;
				lock (m_lock)
				{
					// If the awaiter found a match, we're waiting on processing
					// and we've already been removed from `m_awaiters`;
					// don't apply the RabbitMq message timeout.
					shouldTimeOut = awaiter.Message == null;
					if (shouldTimeOut)
					{
						m_awaiters.Remove(awaiter);

						// If this was the last awaiter, stop the consumer.
						if (m_awaiters.Count == 0 && m_consumerState is { Current: ConsumerState.State.Started })
							m_consumerState.Stop();
					}
				}

				if (!shouldTimeOut)
					return await result;

				// Perhaps we missed the message because another subscriber got it
				// while our prefetch count was overwhelmed (unlikely)
				using var a = m_lastObservedDeliveryTag >= m_settings.PrefetchCount
					? AssertEx.Context(new { prefetchCount = m_settings.PrefetchCount, m_lastObservedDeliveryTag })
					: null;

				using (AssertEx.Context(c_timeoutReason, "after `await`"))
					awaiter.AssertTimeoutFailure(m_settings.TimeoutMilliseconds);

				throw new InvalidOperationException("Multiple Assertions not supported here.");
			});
		}

		public void Dispose()
		{
			lock (m_lock)
			{
				if (m_consumerState != null)
				{
					m_consumerState.Stop();

					foreach (var awaiter in m_awaiters)
						awaiter.Completion.TrySetCanceled(m_consumerState.ShouldStop);

					m_awaiters.Clear();
				}

				m_exception = new ObjectDisposedException(nameof(MessagePublishedAwaiter<TMessage>));
				m_rabbitMq.Dispose();
			}
		}

		private Task AddAwaiter(MessageAwaiter<TMessage> awaiter)
		{
			ConsumerState currentConsumer;
			Task previousConsumerIsComplete = null;
			bool shouldStartNewConsumer;
			lock (m_lock)
			{
				if (m_exception != null)
					throw m_exception;

				shouldStartNewConsumer = m_consumerState == null
					|| m_consumerState.Current is ConsumerState.State.Complete or ConsumerState.State.Stopped;

				if (m_consumerState?.Current == ConsumerState.State.Stopped)
					previousConsumerIsComplete = m_consumerState.IsComplete;

				if (shouldStartNewConsumer)
					m_consumerState = new ConsumerState();

				currentConsumer = m_consumerState;

				m_awaiters.Add(awaiter);
			}

			if (shouldStartNewConsumer)
			{
				var withTimeout = WithTimeout();
				RunInBackground(async () =>
				{
					// Ensure our previous consumer has completed, so:
					// (1) we do not try to duplicate-ack any messages, and
					// (2) There's at least *some* window for the background consumer to flush any backlog of un-matched messages.
					if (previousConsumerIsComplete != null)
					{
						await withTimeout(previousConsumerIsComplete);

						if (!previousConsumerIsComplete.IsCompleted)
							throw new OperationCanceledException($"Timeout waiting for previous consumer to complete after {MessageAwaiter<TMessage>.HumanReadable(m_settings.TimeoutMilliseconds)}.");
					}

					var messages = Channel.CreateUnbounded<(ulong, string)>();

					await StartPriorityConsumer(currentConsumer, messages.Writer, withTimeout);

					await SubscribeAsync(currentConsumer, messages.Reader);
				});
			}

			return currentConsumer.IsStarted;
		}

		private async Task StartPriorityConsumer(ConsumerState consumerState, ChannelWriter<(ulong DeliveryTag, string Body)> messages, Func<Task, Task> withTimeout)
		{
			try
			{
				var startConsumer = m_rabbitMq.StartConsumer(
					consumerState.ConsumerTag,
					onReceived: (deliveryTag, message) =>
					{
						// OK to write to outside of `m_lock` because we guarantee `onReceived` is exclusive to `onCancelled`.
						m_lastObservedDeliveryTag = deliveryTag;

						messages.TryWrite((deliveryTag, message));
					},
					onCancelled: () =>
					{
						// The client may receive an arbitrary number of messages in between sending the cancel method and receiving the cancel-ok reply.
						//   - https://www.rabbitmq.com/amqp-0-9-1-reference.html#basic.cancel.consumer-tag
						//
						// Ensure we've ACKed xor NACKed them all through `m_lastObservedDeliveryTag`
						ulong nackMultiple;
						var nackSingle = new List<ulong>();

						lock (m_lock)
						{
							consumerState.SetComplete();

							CalculateNacks(m_lastObservedDeliveryTag, m_previouslyNackedThrough, m_processingDeliveryTags, m_ackedDeliveryTags, m_shouldNackDeliveryTags, out nackMultiple, ref nackSingle);

							// Ensure we don't nack anything twice when the next subscriber comes along
							m_previouslyNackedThrough = m_lastObservedDeliveryTag;

							// Cleanup tracking sets
							m_shouldNackDeliveryTags.Clear();

							if (!m_processingDeliveryTags.Any())
							{
								m_ackedDeliveryTags.Clear();
							}
							else
							{
								var min = m_processingDeliveryTags.Min();
								m_ackedDeliveryTags.RemoveWhere(dt => dt < min);
							}
						}

						// If set to 1, the delivery tag is treated as "up to and including", so that multiple messages can be rejected with a single method.
						//   - https://www.rabbitmq.com/amqp-0-9-1-reference.html#basic.nack.multiple
						//
						// (NOTE RE: "If the multiple field is 1, and the delivery tag is zero, this indicates rejection of all outstanding messages.":
						//    This is lies, they do not.)
						if (nackMultiple > 0ul)
							m_rabbitMq.BasicNack(nackMultiple, multiple: true);

						foreach (var deliveryTag in nackSingle)
							m_rabbitMq.BasicNack(deliveryTag, multiple: false);
					});

				await withTimeout(startConsumer);

				if (startConsumer.IsCompleted)
					consumerState.SetStarted();
				else
					throw new OperationCanceledException($"Timeout waiting for consumer to start after {MessageAwaiter<TMessage>.HumanReadable(m_settings.TimeoutMilliseconds)}.");
			}
			catch (Exception e)
			{
				consumerState.SetException(e);
				throw;
			}
		}

		internal static void CalculateNacks(ulong lastObserved, ulong previouslyNackedThrough, HashSet<ulong> processing, HashSet<ulong> acked, HashSet<ulong> shouldNack, out ulong nackMultiple, ref List<ulong> nackSingle)
		{
			if (!processing.Any())
			{
				// If no delivery-tags are processing, nack the last non-acked one.
				nackMultiple = lastObserved;
			}
			else
			{
				// Ensure we do not NACK a tag awaiting ACKing by an awaiter when doing our `nackMultiple`.
				// It's OK for there to be previously-ACKed messages *smaller* than this value, though.
				var firstNackSingle = processing.Min();

				// nack all messages until the last non-acked message before the first message still processing.
				nackMultiple = firstNackSingle - 1ul;

				// NACK all the tags after our `firstNackSingle` RBAR
				for (var tag = firstNackSingle + 1ul; tag <= lastObserved; tag++)
				{
					if (!acked.Contains(tag) && !processing.Contains(tag))
						nackSingle.Add(tag);
				}
			}

			// We'll get an error if we NACK something we've already ACKed.
			while (acked.Contains(nackMultiple))
				nackMultiple -= 1ul;

			// Never `nack` before `previouslyNackedThrough` **unless** it's been put in `shouldNack` since.
			if (nackMultiple <= previouslyNackedThrough)
			{
				var needsNack = shouldNack.Where(dt => dt <= previouslyNackedThrough).ToList();

				nackMultiple = needsNack.Any()
					? needsNack.Max()
					: 0ul;
			}
		}

		private async Task SubscribeAsync(ConsumerState consumer, ChannelReader<(ulong DeliveryTag, string Body)> messages)
		{
			CancellationTokenSource timeout = null;
			var hasAwaiters = true;
			var subscriberCancellation = consumer.ShouldStop;
			try
			{
				while (hasAwaiters && !subscriberCancellation.IsCancellationRequested && await messages.WaitToReadAsync(subscriberCancellation))
				{
					while (hasAwaiters && messages.TryRead(out var item))
					{
						MessageAwaiter<TMessage> awaiter;
						lock (m_lock)
						{
							awaiter = MessageAwaiter<TMessage>.FirstMatch(m_awaiters, item.Body);

							if (awaiter != null)
							{
								m_processingDeliveryTags.Add(item.DeliveryTag);
								m_awaiters.Remove(awaiter);
							}

							hasAwaiters = m_awaiters.Any();

							if (!hasAwaiters)
								consumer.Stop();
						}

						if (awaiter != null)
						{
							RunInBackground(() => CompleteAsync(awaiter, item.DeliveryTag));
						}
						else if (hasAwaiters && timeout == null)
						{
							// Ensure this message waits no more than m_settings.TimeoutSeconds before we nack it.
							// Can't just nack it now because RabbitMQ doesn't make the same guarantees the AMQP spec promises.
							timeout = new CancellationTokenSource(m_settings.TimeoutMilliseconds);
							subscriberCancellation = CancellationTokenSource.CreateLinkedTokenSource(consumer.ShouldStop, timeout.Token).Token;
						}
					}
				}
			}
			catch (OperationCanceledException) when (subscriberCancellation.IsCancellationRequested)
			{
				// Just cleanup our subscriber
			}
			finally
			{
				if (timeout != null)
				{
					if (timeout.IsCancellationRequested)
					{
						var awaiters = new List<MessageAwaiter<TMessage>>();
						lock (m_lock)
						{
							consumer.Stop();
							awaiters.AddRange(m_awaiters);
							m_awaiters.Clear();
						}

						using (AssertEx.Context(c_timeoutReason, "unacked message"))
						{
							foreach (var awaiter in awaiters)
							{
								try
								{
									awaiter.AssertTimeoutFailure(m_settings.TimeoutMilliseconds);

									// Handles the multiple-assertion case.
									awaiter.Completion.TrySetCanceled(timeout.Token);
								}
								catch (Exception e)
								{
									awaiter.Completion.SetException(e);
								}
							}
						}
					}

					timeout.Dispose();
				}

				// Cancel our consumer so that we can release all backed-up messages.
				m_rabbitMq.BasicCancel(consumer.ConsumerTag);
			}
		}

		private async Task CompleteAsync(MessageAwaiter<TMessage> awaiter, ulong deliveryTag)
		{
			try
			{
				await m_processMessage(awaiter.Message);
			}
			catch (Exception e)
			{
				// This lets us NACK the message when our subscriber stops.
				// The backup subscriber will attempt to pick it up, potentially putting it in a queueing-errors table if it fails there too.
				// Our code-under-test ought to *at least* make sure nothing too horrendous happens if later messages operate on the same data.
				Nack(deliveryTag);

				// Comminicate this exception to the awaiter
				// rather than counting it against our subscriber.
				awaiter.Completion.TrySetException(e);

				return;
			}

			// Ack *after* processing so the backup subscriber can attempt to handle the message if our process dies.
			Ack(deliveryTag);

			awaiter.Complete();
		}

		private void Ack(ulong deliveryTag)
		{
			m_rabbitMq.BasicAck(deliveryTag);

			lock (m_lock)
			{
				m_ackedDeliveryTags.Add(deliveryTag);
				m_processingDeliveryTags.Remove(deliveryTag);
			}
		}

		private void Nack(ulong deliveryTag)
		{
			// If the consumer is complete, we've gotta nack the message ourselves.
			// Otherwise its shutdown handler will nack it for us when not marked explicit.
			bool shouldNack;
			lock (m_lock)
			{
				shouldNack = m_consumerState.Current == ConsumerState.State.Complete;
				m_processingDeliveryTags.Remove(deliveryTag);
				m_shouldNackDeliveryTags.Add(deliveryTag);
			}

			if (shouldNack)
				m_rabbitMq.BasicNack(deliveryTag, multiple: false);
		}

		private void RunInBackground(Func<Task> work) => Task.Run(async () =>
		{
			try
			{
				await work();
			}
			catch (Exception e)
			{
				HandleSubscriberException(e);
			}
		});

		private void HandleSubscriberException(Exception e)
		{
			lock (m_lock)
			{
				m_exception = e;

				foreach (var awaiter in m_awaiters)
					awaiter.Completion.TrySetException(e);

				m_awaiters.Clear();
				m_consumerState = null;

				// Design Decision: No re-connect logic.
				// Doesn't matter if one test fails due to RabbitMq trouble or 15, the build's still broken.
				// (change this if test retries become important)
				m_rabbitMq.Dispose();
			}
		}

		private Func<Task, Task> WithTimeout()
		{
			var timeout = Task.Delay(m_settings.TimeoutMilliseconds);

			return other => Task.WhenAny(other, timeout);
		}

		/// <summary>
		/// Transitions our consumer through states
		/// Initial => Started => Stopped => Complete.
		/// </summary>
		private sealed class ConsumerState
		{
			public string ConsumerTag { get; } = $"message_picker_{Environment.MachineName}_{Guid.NewGuid().ToLowerNoDashString()}";
			public State Current { get; private set; }
			public Task IsStarted => m_started.Task;
			public Task IsComplete => m_complete.Task;
			public CancellationToken ShouldStop => m_stop.Token;

			public void SetStarted()
			{
				lock (m_lock)
				{
					if (Current != State.Initial)
						return;

					Current = State.Started;

					m_started.SetResult(null);
				}
			}

			public void SetComplete()
			{
				lock (m_lock)
				{
					if (Current is State.Initial or State.Started)
					{
						var e = new InvalidOperationException($"Cannot complete when in state {Current}");
						SetException(e);
						throw e;
					}

					if (Current != State.Stopped)
						return;

					Current = State.Complete;

					m_complete.SetResult(null);
				}
			}

			public void Stop()
			{
				lock (m_lock)
				{
					if (Current is State.Stopped or State.Complete or State.Errored)
						return;

					if (Current == State.Initial)
						m_started.TrySetCanceled(ShouldStop);

					Current = State.Stopped;

					m_stop.Cancel();
				}
			}

			public void SetException(Exception e)
			{
				lock (m_lock)
				{
					if (Current == State.Errored)
						return;

					if (Current == State.Initial)
						m_started.TrySetException(e);

					if (Current != State.Complete)
						m_complete.TrySetException(e);

					Current = State.Errored;
				}
			}

			public enum State
			{
				Initial,
				Started,
				Stopped,
				Complete,
				Errored,
			}

			private readonly object m_lock = new();
			private readonly CancellationTokenSource m_stop = new();
			private readonly TaskCompletionSource<object> m_complete = new(TaskCreationOptions.RunContinuationsAsynchronously);
			private readonly TaskCompletionSource<object> m_started = new(TaskCreationOptions.RunContinuationsAsynchronously);
		}

		private const string c_timeoutReason = "timeoutReason";

		private readonly object m_lock = new();
		private readonly object m_context;
		private readonly Func<TMessage, Task> m_processMessage;
		private readonly MessageProcessedSettings m_settings;

		private readonly List<MessageAwaiter<TMessage>> m_awaiters = new();
		private readonly IRabbitMqWrapper m_rabbitMq;
		private readonly HashSet<ulong> m_processingDeliveryTags = new();
		private readonly HashSet<ulong> m_ackedDeliveryTags = new();
		private readonly HashSet<ulong> m_shouldNackDeliveryTags = new();

		private ulong m_lastObservedDeliveryTag;
		private ulong m_previouslyNackedThrough;
		private Exception m_exception;
		private ConsumerState m_consumerState;
	}
}
