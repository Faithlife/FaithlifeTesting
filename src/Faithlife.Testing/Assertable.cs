using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Faithlife.Testing.TestFrameworks;

namespace Faithlife.Testing
{
	/// <summary>
	/// Allows making assertions about a particular value of type <typeparamref name="T"/>
	/// </summary>
	public sealed class Assertable<T>
		where T : class
	{
		internal static Assertable<T> NoOp() => new(null, null, ImmutableStack<(string Name, object Value)>.Empty);

		internal Assertable(T value, Expression<Func<T>> valueExpression, ImmutableStack<(string Name, object Value)> context)
		{
			Value = value ;
			m_valueExpression = valueExpression;
			m_context = context;
		}

		/// <summary>
		/// Asserts that <paramref name="mapExpression" /> does not return `null`
		/// and allows chaining further asserts on that <typeparamref name="TResult"/> value.
		/// </summary>
		public Assertable<TResult> HasValue<TResult>(Expression<Func<T, TResult>> mapExpression)
			where TResult : class
		{
			if (mapExpression == null)
				throw new ArgumentNullException(nameof(mapExpression));

			if (IsNoOp)
				return Assertable<TResult>.NoOp();

			var valueExpression = CoalesceWith(mapExpression);

			var (value, message) = AssertEx.GetValueOrNullMessage(valueExpression, m_context);

			if (message != null)
			{
				TestFrameworkProvider.Fail(message);
				return Assertable<TResult>.NoOp();
			}

			return new Assertable<TResult>(value, valueExpression, m_context);
		}

		/// <summary>
		/// Asserts that <paramref name="mapExpression" /> does not return `null`
		/// </summary>
		public TResult HasValue<TResult>(Expression<Func<T, TResult?>> mapExpression)
			where TResult : struct
		{
			if (mapExpression == null)
				throw new ArgumentNullException(nameof(mapExpression));

			if (IsNoOp)
				throw new InvalidOperationException("A previous assertion failed.");

			var valueExpression = CoalesceWith(mapExpression);

			var (value, message) = AssertEx.GetValueOrNullMessage(valueExpression, m_context);

			if (message != null)
				TestFrameworkProvider.Fail(message);

			return value.Value;
		}

		/// <summary>
		/// Asserts that <paramref name="predicateExpression" /> does not return `false`
		/// and allows chaining further asserts on the current value.
		/// </summary>
		public Assertable<T> IsTrue(Expression<Func<T, bool>> predicateExpression)
		{
			if (predicateExpression == null)
				throw new ArgumentNullException(nameof(predicateExpression));

			if (!IsNoOp)
			{
				var message = AssertEx.GetMessageIfFalse(CoalesceWith(predicateExpression), m_context);

				if (message != null)
					TestFrameworkProvider.Fail(message);
			}

			return this;
		}

		/// <summary>
		/// Asserts that <paramref name="assertionExpression" /> does not throw an exception
		/// and allows chaining further asserts on the current value.
		/// </summary>
		public Assertable<T> DoesNotThrow(Expression<Action<T>> assertionExpression)
		{
			if (assertionExpression == null)
				throw new ArgumentNullException(nameof(assertionExpression));

			if (IsNoOp)
				return this;

			var visitor = new ReplaceParameterWithExpressionVisitor(assertionExpression.Parameters, m_valueExpression.Body);
			var coalescedAssertion = Expression.Lambda<Action>(visitor.Visit(assertionExpression.Body));
			var assertFunc = coalescedAssertion.Compile();

			try
			{
				assertFunc();
			}
			catch (Exception exception)
			{
				TestFrameworkProvider.Fail(AssertEx.GetDiagnosticMessage(coalescedAssertion.Body, exception, m_context));
			}

			return this;
		}


		/// <summary>
		/// Adds informational context to all assertions made using this chain.
		/// </summary>
		public Assertable<T> Context(string name, object value) => Context((name, value));

		/// <summary>
		/// Adds informational context to all assertions made using this chain.
		/// </summary>
		public Assertable<T> Context((string Name, object Value) first, params (string Key, object Value)[] rest) => Context(rest.Prepend(first));

		/// <summary>
		/// Adds informational context to all assertions made using this chain.
		/// </summary>
		public Assertable<T> Context<TValue>(IReadOnlyDictionary<string, TValue> data) => Context(data.Select(x => (x.Key, (object) x.Value)));

		/// <summary>
		/// Adds informational context to all assertions made using this chain.
		/// </summary>
		public Assertable<T> Context(object context) => Context(AssertEx.GetContextFromObject(context));

		/// <summary>
		/// Adds informational context to all assertions made using this chain.
		/// </summary>
		public Assertable<T> Context(params Expression<Func<object>>[] contextExpressions) => Context(AssertEx.GetContextFromExpressions(contextExpressions));

		/// <summary>
		/// Adds informational context to all assertions made using this chain.
		/// </summary>
		public Assertable<T> Context(IEnumerable<(string Name, object Value)> context)
		{
			if (context == null)
				throw new ArgumentNullException(nameof(context));

			var newContext = m_context;

			foreach (var pair in context)
				newContext = newContext.Push(pair);

			return newContext != m_context
				? new Assertable<T>(Value, m_valueExpression, newContext)
				: this;
		}

		/// <summary>
		/// The current value which assertions are made on.
		/// </summary>
		public T Value { get; }

		/// <summary>
		/// The current value which assertions are made on.
		/// </summary>
#pragma warning disable CA2225 // Operator overloads have named alternates
#pragma warning disable CA1065 // Do not raise exceptions in unexpected locations
		public static implicit operator T(Assertable<T> source) => source?.Value ?? throw new ArgumentNullException(nameof(source));
#pragma warning restore CA1065 // Do not raise exceptions in unexpected locations
#pragma warning restore CA2225 // Operator overloads have named alternates

		private bool IsNoOp => m_valueExpression == null;

		private Expression<Func<TResult>> CoalesceWith<TResult>(Expression<Func<T, TResult>> mapExpression)
		{
			var visitor = new ReplaceParameterWithExpressionVisitor(mapExpression.Parameters, m_valueExpression.Body);
			return Expression.Lambda<Func<TResult>>(visitor.Visit(mapExpression.Body));
		}

		private sealed class ReplaceParameterWithExpressionVisitor : ExpressionVisitor
		{
			public ReplaceParameterWithExpressionVisitor(IEnumerable<ParameterExpression> oldParameters, Expression newExpression)
			{
				m_parameterMap = oldParameters
					.ToDictionary(p => p, _ => newExpression);
			}

			protected override Expression VisitParameter(ParameterExpression parameter) =>
				m_parameterMap.TryGetValue(parameter, out var replacement)
					? replacement
					: base.VisitParameter(parameter);

			private readonly IDictionary<ParameterExpression, Expression> m_parameterMap;
		}

		private readonly Expression<Func<T>> m_valueExpression;
		private readonly ImmutableStack<(string Name, object Value)> m_context;
	}
}
