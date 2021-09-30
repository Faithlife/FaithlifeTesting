namespace Faithlife.Testing.RabbitMq
{
	// false positive
	// https://github.com/dotnet/roslyn-analyzers/issues/4397
#pragma warning disable CA1801 // Review unused parameters
	public sealed record MessageProcessedSettings(
		int TimeoutMilliseconds = 5_000,
		int Priority = 10,
		ushort PrefetchCount = 10_000);
#pragma warning restore CA1801 // Review unused parameters
}
