using System;
using System.Linq;
using System.Threading;

namespace Faithlife.Testing.TestFrameworks
{
	// Credit to https://github.com/fluentassertions/fluentassertions/blob/a8fce5df379782e466db551a683696a51969b1d8/Src/FluentAssertions/Execution/TestFrameworkProvider.cs
	internal static class TestFrameworkProvider
	{
		public static void Fail(string message) => s_currentFramework.Value.Fail(message);
		public static IDisposable GetIsolatedContext() => s_currentFramework.Value.GetIsolatedContext();

		private static readonly ITestFramework[] s_frameworks =
		{
			new LateBoundTestFramework("nunit.framework", "NUnit.Framework.Assert", "Fail", "NUnit.Framework.Internal.TestExecutionContext+IsolatedContext"),
			new FallbackTestFramework(),
		};

		private static readonly Lazy<ITestFramework> s_currentFramework = new(
			() => s_frameworks.First(f => f.IsAvailable),
			LazyThreadSafetyMode.ExecutionAndPublication);
	}
}
