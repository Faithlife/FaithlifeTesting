using System.Collections.Generic;

internal static class NugetPackages
{
	public static readonly IReadOnlyList<string> ProjectsToPublish = new[]
	{
		@"src\Faithlife.Testing",
		@"src\Faithlife.Testing.RabbitMq",
	};
}
