namespace Faithlife.Testing.RabbitMq
{
	/// <summary>
	/// Settins for the MessageProcessedAwaiter.
	/// </summary>
	public sealed class MessageProcessedSettings
	{
		public int TimeoutMilliseconds { get; set; } = 5_000;
		public int Priority { get; set; } = 10;
		public ushort PrefetchCount { get; set; } = 10_000;
	}
}
