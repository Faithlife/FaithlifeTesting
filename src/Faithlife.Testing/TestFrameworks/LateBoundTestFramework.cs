using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

namespace Faithlife.Testing.TestFrameworks
{
	// Credit to https://github.com/fluentassertions/fluentassertions/blob/a8fce5df379782e466db551a683696a51969b1d8/Src/FluentAssertions/Execution/LateBoundTestFramework.cs
	internal sealed class LateBoundTestFramework : ITestFramework
	{
		public LateBoundTestFramework(string assemblyName, string failTypeName, string failMethodName, string isolatedContextTypeName)
		{
			m_actions = new(
				() =>
				{
					var prefix = assemblyName + ",";
					var assembly = AppDomain.CurrentDomain
						.GetAssemblies()
						.Where(a => a.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
						.FirstOrDefault();

					if (assembly == null)
						return default;

					var message = Expression.Parameter(typeof(string), "message");
					var failMethod =
						(assembly.GetType(failTypeName)
							?? throw new Exception($"Failed to create the assertion type for the current test framework: \"{failTypeName}, {assembly.FullName}\""))
						.GetMethod(failMethodName, BindingFlags.Public | BindingFlags.Static, null, new []{ typeof(string) }, null)
							?? throw new Exception($"Failed to create the assert-failure method for the current test framework: \"{failTypeName}.{failMethodName}, {assembly.FullName}\"");

					var isolatedContext =
						assembly.GetType(isolatedContextTypeName)
						?? throw new Exception($"Failed to create the Isolated Context type for the current test framework: \"{isolatedContextTypeName}, {assembly.FullName}\"");

					return (
						Expression.Lambda<Action<string>>(Expression.Call(null, failMethod, message), message).Compile(),
						() => (IDisposable) Activator.CreateInstance(isolatedContext)
					);
				},
				LazyThreadSafetyMode.ExecutionAndPublication);
		}

		public bool IsAvailable => m_actions.Value != default;
		public void Fail(string message) => m_actions.Value.Fail(message);
		public IDisposable GetIsolatedContext() => m_actions.Value.GetIsolatedContext();

		private readonly Lazy<(Action<string> Fail, Func<IDisposable> GetIsolatedContext)> m_actions;
	}
}
