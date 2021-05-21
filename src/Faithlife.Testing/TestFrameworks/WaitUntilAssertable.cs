using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Faithlife.Testing.TestFrameworks
{
	/// <summary>
	/// Waits for a value which pases provided assertions.
	/// </summary>
	public sealed class WaitUntilAssertable<T>
		where T : class
	{
		internal WaitUntilAssertable(Func<Task<Assertable<T>>> getAssertable, TimeSpan? timeout = null)
		{
			m_getAssertable = getAssertable;
			m_timeout = timeout;
		}

		/// <summary>
		/// Returns a new `WaitUntilAssertable{T}` with the specified <paramref name="timeout"/>
		/// </summary>
		public WaitUntilAssertable<T> WithTimeout(TimeSpan timeout)
		{
			if (timeout <= TimeSpan.Zero)
				throw new ArgumentException("Timeout must be positive.", nameof(timeout));

			return new WaitUntilAssertable<T>(m_getAssertable, timeout);
		}

		/// <summary>
		/// Asserts that <paramref name="mapExpression" /> does not return `null`
		/// and allows chaining further asserts on that <typeparamref name="TResult"/> value.
		/// </summary>
		public WaitUntilAssertable<TResult> HasValue<TResult>(Expression<Func<T, TResult>> mapExpression)
			where TResult : class
		{
			if (mapExpression == null)
				throw new ArgumentNullException(nameof(mapExpression));

			return Apply(a => a.HasValue(mapExpression));
		}

		/// <summary>
		/// Asserts that <paramref name="mapExpression" /> does not return `null`
		/// </summary>
		public async Task<TResult> HasValue<TResult>(Expression<Func<T, TResult?>> mapExpression)
			where TResult : struct
		{
			if (mapExpression == null)
				throw new ArgumentNullException(nameof(mapExpression));

			return await WaitForValue(async () => (await m_getAssertable()).HasValue(mapExpression));
		}

		/// <summary>
		/// Asserts that <paramref name="predicateExpression" /> does not return `false`
		/// and allows chaining further asserts on the current value.
		/// </summary>
		public WaitUntilAssertable<T> IsTrue(Expression<Func<T, bool>> predicateExpression)
		{
			if (predicateExpression == null)
				throw new ArgumentNullException(nameof(predicateExpression));

			return Apply(a => a.IsTrue(predicateExpression));
		}

		/// <summary>
		/// Asserts that <paramref name="assertionExpression" /> does not throw an exception
		/// and allows chaining further asserts on the current value.
		/// </summary>
		public WaitUntilAssertable<T> DoesNotThrow(Expression<Action<T>> assertionExpression)
		{
			if (assertionExpression == null)
				throw new ArgumentNullException(nameof(assertionExpression));

			return Apply(a => a.DoesNotThrow(assertionExpression));
		}

		/// <summary>
		/// Adds informational context to all assertions made using this chain.
		/// </summary>
		public WaitUntilAssertable<T> Context(string name, object value) => Context((name, value));

		/// <summary>
		/// Adds informational context to all assertions made using this chain.
		/// </summary>
		public WaitUntilAssertable<T> Context((string Name, object Value) first, params (string Key, object Value)[] rest) => Context(rest.Prepend(first));

		/// <summary>
		/// Adds informational context to all assertions made using this chain.
		/// </summary>
		public WaitUntilAssertable<T> Context<TValue>(IReadOnlyDictionary<string, TValue> data) => Context(data.Select(x => (x.Key, (object) x.Value)));

		/// <summary>
		/// Adds informational context to all assertions made using this chain.
		/// </summary>
		public WaitUntilAssertable<T> Context(object context) => Context(AssertEx.GetContextFromObject(context));

		/// <summary>
		/// Adds informational context to all assertions made using this chain.
		/// </summary>
		public WaitUntilAssertable<T> Context(params Expression<Func<object>>[] contextExpressions) => Context(AssertEx.GetContextFromExpressions(contextExpressions));

		/// <summary>
		/// Adds informational context to all assertions made using this chain.
		/// </summary>
		public WaitUntilAssertable<T> Context(IEnumerable<(string Name, object Value)> context)
		{
			if (context == null)
				throw new ArgumentNullException(nameof(context));

			return Apply(a => a.Context(context));
		}

		/// <summary>
		/// Applies a <paramref name="transform"/> to the assertion we will wait for.
		/// </summary>
		public WaitUntilAssertable<TResult> Apply<TResult>(Func<Assertable<T>, Assertable<TResult>> transform)
			where TResult : class
		{
			if (transform == null)
				throw new ArgumentNullException(nameof(transform));

			return new WaitUntilAssertable<TResult>(
				async () => transform(await m_getAssertable()),
				m_timeout);
		}

		/// <summary>
		/// Starts waiting.
		/// </summary>
		public TaskAwaiter<T> GetAwaiter() => WaitForValue(async () => (await m_getAssertable()).Value).GetAwaiter();

		/// <summary>
		/// Synchronously waits for an appropiate value.
		/// </summary>
		public T Value => GetAwaiter().GetResult();

		/// <summary>
		/// Synchronously waits for an appropiate value.
		/// </summary>
#pragma warning disable CA2225 // Operator overloads have named alternates
#pragma warning disable CA1065 // Do not raise exceptions in unexpected locations
		public static implicit operator T(WaitUntilAssertable<T> source) => source?.Value ?? throw new ArgumentNullException(nameof(source));
#pragma warning restore CA1065 // Do not raise exceptions in unexpected locations
#pragma warning restore CA2225 // Operator overloads have named alternates

		private async Task<TResult> WaitForValue<TResult>(Func<Task<TResult>> actionAsync)
		{
			var timeout = m_timeout ?? s_defaultTimeout;
			using var cts = new CancellationTokenSource(timeout);
			var attemptNumber = 0;

			while (true)
			{
				using (TestFrameworkProvider.GetIsolatedContext())
				{
					// ReSharper disable once EmptyGeneralCatchClause
					try
					{
						return await actionAsync();
					}
					catch (Exception)
					{
					}
				}

				attemptNumber++;

				try
				{
					await Task.Delay(c_sleepTimeMs * attemptNumber * attemptNumber, cts.Token);
					continue;
				}
				catch (TaskCanceledException)
				{
				}

				using (AssertEx.Context(("totalRetries", attemptNumber + 1), ("timeoutSeconds", timeout.TotalSeconds)))
					return await actionAsync();
			}
		}

		private const int c_sleepTimeMs = 100;
		private static readonly TimeSpan s_defaultTimeout = TimeSpan.FromSeconds(30);

		private readonly Func<Task<Assertable<T>>> m_getAssertable;
		private readonly TimeSpan? m_timeout;
	}
}
