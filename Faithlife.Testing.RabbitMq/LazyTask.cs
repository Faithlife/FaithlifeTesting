using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Faithlife.Testing.RabbitMq
{
	public struct LazyTask<TResult>
	{
		public LazyTask(Func<Task<TResult>> onAwaited)
		{
			m_innerTask = new Lazy<Task<TResult>>(onAwaited, LazyThreadSafetyMode.ExecutionAndPublication);
		}

		public TaskAwaiter<TResult> GetAwaiter() => m_innerTask.Value.GetAwaiter();

		public LazyTask<TNext> Select<TNext>(Func<TResult, TNext> continuation)
		{
			var innerTask = m_innerTask;
			return new LazyTask<TNext>(
				async () =>
				{
					var result = await innerTask.Value;
					return continuation(result);
				});
		}

		private readonly Lazy<Task<TResult>> m_innerTask;
	}
}
