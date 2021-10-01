using System;
using System.Threading.Tasks;

namespace Faithlife.Testing.RabbitMq
{
	/// <summary>
	/// Wraps all RabbitMq operations for testability.
	/// </summary>
	internal interface IRabbitMqWrapper : IDisposable
	{
		// These handlers cannot run in paralell, minimize processing.
		Task StartConsumer(string consumerTag, Action<ulong, string> onReceived, Action onCancelled);
		void BasicAck(ulong deliveryTag);
		void BasicNack(ulong deliveryTag, bool multiple);
		void BasicCancel(string consumerTag);
	}
}
