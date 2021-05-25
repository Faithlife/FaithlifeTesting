using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Faithlife.WebRequests.Json;

namespace Faithlife.Testing.RabbitMq
{
	/// <summary>
	/// Utility for improving error messages when encountering backed-up queues.
	/// Waits until the specified queue is empty, even counting acked messages, or fails with a time-out.
	/// IF awaiting starts after publishing, guarantees that any messages published before awaiting begins are fully processed and acked before returning.
	/// NOTE: if publishing is asynchronous (e.g., using a bridge), a successful API call does *not* guarantee that a message has been published yet.
	/// NOTE: A message fully processed does not guarantee any MySql replication or background Redis cache operations have completed.
	/// </summary>
	public sealed class EmptyQueueAwaiter
	{
		public EmptyQueueAwaiter(string queueName, string serverName, int secondsDelay)
		{
			m_queueName = queueName;
			m_uri = new Uri($"http://{serverName}:15672/api/queues/%2f/{m_queueName}");
			m_secondsDelay = secondsDelay;

			Task.Run(PollingLoop);
		}

		public Task WaitForEmptyQueue()
		{
			var tcs = new TaskCompletionSource<object>();

			lock (m_lock)
			{
				if (m_exception != null)
					throw m_exception;

				m_awaiters.Add(tcs);
				m_loopAwaiter.TrySetResult(null);
			}

			return tcs.Task;
		}

		private async Task PollingLoop()
		{
			try
			{
				while (true)
				{
					await m_loopAwaiter.Task.ConfigureAwait(false);

					using var cts = new CancellationTokenSource(m_secondsDelay * 1000);
					while (true)
					{
						try
						{
							var response = (await new AutoWebServiceRequest<GetQueueResponse>(m_uri)
							{
								AdditionalHeaders = s_authorizationHeader,
							}
									.GetResponseAsync(cts.Token))
								.GetExpectedResult(r => r.OK);

							// TODO: The response has some message-processing-rate data we could use to be smart here.
							if (response.Messages == 0)
							{
								ResetLoop(a => a.SetResult(null));
								break;
							}

							var waitMilliseconds = response.Messages < 100
								? 100
								: 1000;

							await Task.Delay(TimeSpan.FromMilliseconds(waitMilliseconds), cts.Token).ConfigureAwait(false);
						}
						catch (TaskCanceledException)
						{
							var exception = new TimeoutException($"Timeout waiting for queue http://{m_uri.Host}:15672/#/queues/%2f/{m_queueName} to drain after {m_secondsDelay} seconds.");
							ResetLoop(a => a.TrySetException(exception));
						}
					}
				}
			}
			catch (Exception e)
			{
				m_exception = e;
				ResetLoop(a => a.TrySetException(e));
			}

			void ResetLoop(Action<TaskCompletionSource<object>> action)
			{
				List<TaskCompletionSource<object>> awaiters;
				lock (m_lock)
				{
					m_loopAwaiter = new TaskCompletionSource<object>();
					awaiters = m_awaiters;
					m_awaiters = new List<TaskCompletionSource<object>>();
				}

				foreach (var awaiter in awaiters)
					action(awaiter);
			}
		}

		private sealed class GetQueueResponse : AutoWebServiceResponse
		{
			public QueueDto OK { get; set; }
		}

		private sealed class QueueDto
		{
			public int Messages { get; set; }
		}

		private static readonly WebHeaderCollection s_authorizationHeader = new() { { "Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("guest:guest")) } };

		private readonly string m_queueName;
		private readonly Uri m_uri;
		private readonly int m_secondsDelay;
		private readonly object m_lock = new();

		private List<TaskCompletionSource<object>> m_awaiters = new();
		private Exception m_exception;
		private TaskCompletionSource<object> m_loopAwaiter = new();
	}
}
