using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Faithlife.Testing.RabbitMq
{
	/// <summary>
	/// Wraps all RabbitMq operations for testability.
	/// </summary>
	internal sealed class RabbitMqWrapper : IRabbitMqWrapper, IDisposable
	{
		public RabbitMqWrapper(string serverName, string queueName, int priority, bool autoAck, Action<Exception> onError, Action<IModel> setup)
		{
			m_queueName = queueName;
			m_priority = priority;
			m_autoAck = autoAck;
			m_onError = onError;
			m_connection = new ConnectionFactory
			{
				HostName = serverName,
				RequestedHeartbeat = 30,
			}.CreateConnection();

			try
			{
				m_model = m_connection.CreateModel();

				try
				{
					setup(m_model);
				}
				catch
				{
					TryDispose(ref m_model);
					throw;
				}
			}
			catch
			{
				TryDispose(ref m_connection);
				throw;
			}
		}

		public Task StartConsumer(string consumerTag, Action<ulong, string> onReceived, Action onCancelled)
		{
			var consumer = new EventingBasicConsumer(m_model);

			// The body of the message must be copied before returning from the event handler.
			consumer.Received += (_, args) => onReceived(args.DeliveryTag, args.Body?.Length > 0 ? Encoding.UTF8.GetString(args.Body) : "");

			consumer.ConsumerCancelled += (_, _) => onCancelled();

			var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

			consumer.Registered += (_, _) => tcs.SetResult(null);

			try
			{
				m_model.BasicConsume(
					m_queueName,
					autoAck: m_autoAck,
					consumerTag: consumerTag ?? "",
					arguments: new Dictionary<string, object>
					{
						{ Headers.XPriority, m_priority },
					},
					consumer: consumer);
			}
			catch (Exception e)
			{
				m_onError(e);
				throw;
			}

			return tcs.Task;
		}

		public void BasicAck(ulong deliveryTag)
		{
			try
			{
				m_model.BasicAck(deliveryTag, multiple: false);
			}
			catch (Exception e)
			{
				m_onError(e);
			}
		}

		public void BasicNack(ulong deliveryTag, bool multiple)
		{
			try
			{
				m_model.BasicNack(deliveryTag, multiple: multiple, requeue: true);
			}
			catch (Exception e)
			{
				m_onError(e);
			}
		}

		public void BasicCancel(string consumerTag)
		{
			try
			{
				m_model.BasicCancel(consumerTag);
			}
			catch (Exception e)
			{
				m_onError(e);
			}
		}

		public void Dispose()
		{
			TryDispose(ref m_model);
			TryDispose(ref m_connection);
		}

		private static void TryDispose<T>(ref T disposable)
			where T : class, IDisposable
		{
			Interlocked.Exchange(ref disposable, null)?.Dispose();
		}

		private readonly string m_queueName;
		private readonly int m_priority;
		private readonly bool m_autoAck;
		private readonly Action<Exception> m_onError;

		// Don't worry, it's disposed.
#pragma warning disable CA2213 // Disposable fields should be disposed
		private IConnection m_connection;
		private IModel m_model;
#pragma warning restore CA2213 // Disposable fields should be disposed
	}
}
