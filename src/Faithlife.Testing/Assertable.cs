using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Faithlife.Testing.TestFrameworks;

namespace Faithlife.Testing
{
	/// <summary>
	/// Allows making assertions about a particular value of type <typeparamref name="T"/>.
	/// </summary>
	public sealed class Assertable<T>
		where T : class
	{
		internal static Assertable<T> NoOp() => FromValueExpression(null, null);

		internal static Assertable<T> FromValueExpression(T value, Expression valueExpression)
			=> new(value, valueExpression, ImmutableStack<(string Name, object Value)>.Empty, null);

		private Assertable(T value, Expression valueExpression, ImmutableStack<(string Name, object Value)> context, TryExtractValue tryExtractValue)
		{
			Value = value;
			m_valueExpression = valueExpression;
			m_context = context;
			m_tryExtractValue = tryExtractValue;
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

			var (getValueExpression, message) = CoalesceValueWith(mapExpression);

			if (message == null)
			{
				TResult value;
				(value, message) = AssertEx.GetValueOrNullMessage(getValueExpression, m_context);

				if (message == null)
					return new Assertable<TResult>(value, getValueExpression.Body, m_context, null);
			}

			TestFrameworkProvider.Fail(message);
			return Assertable<TResult>.NoOp();
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

			var (getValueExpression, message) = CoalesceValueWith(mapExpression);

			if (message == null)
			{
				TResult? value;
				(value, message) = AssertEx.GetValueOrNullMessage(getValueExpression, m_context);

				if (message == null)
					return value.Value;
			}

			TestFrameworkProvider.Fail(message);

			// This only executes when the test-framework supports multiple assertions.
			throw new InvalidOperationException("Nullable object must have a value.");
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
				var (getValueExpression, message) = CoalesceValueWith(predicateExpression);

				message ??= AssertEx.GetMessageIfFalse(getValueExpression, m_context);

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

			if (!IsNoOp)
			{
				var (actionExpression, message) = CoalesceValueWith(assertionExpression);

				message ??= AssertEx.GetMessageIfException(Expression.Lambda<Action>(actionExpression), m_context);

				if (message != null)
					TestFrameworkProvider.Fail(message);
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
		/// Copies informational context from an <paramref name="other"/> assertable.
		/// </summary>
		public Assertable<T> Context<TOther>(Assertable<TOther> other)
			where TOther : class
		{
			if (other == null)
				throw new ArgumentNullException(nameof(other));

			return Context(other.m_context);
		}

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
				? new Assertable<T>(Value, m_valueExpression, newContext, m_tryExtractValue)
				: this;
		}

		/// <summary>
		/// Tries to intercept the next assertion, and can choose to re-write it.
		/// </summary>
		public Assertable<T> WithExtrator(TryExtractValue extractor)
		{
			if (extractor == null)
				throw new ArgumentNullException(nameof(extractor));

			return new Assertable<T>(Value, m_valueExpression, m_context, extractor);
		}

		/// <summary>
		/// Delegate which can optionally extract a `HasValue` expression from a lambda <paramref name="sourceExpression"/>.
		/// </summary>
		public delegate bool TryExtractValue(LambdaExpression sourceExpression, out LambdaExpression hasValueExpression, out LambdaExpression remainingExpression);

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

		private bool IsNoOp => Value == null;

		private (Expression<Func<TResult>> GetValueExpression, string AssertMessage) CoalesceValueWith<TResult>(Expression<Func<T, TResult>> mapExpression)
		{
			var (resultExpression, message) = CoalesceValueWith((LambdaExpression) mapExpression);
			return (Expression.Lambda<Func<TResult>>(resultExpression), message);
		}

		private (Expression ValueExpression, string AssertMessage) CoalesceValueWith(LambdaExpression mapExpression)
		{
			if (m_tryExtractValue != null && m_tryExtractValue(mapExpression, out var hasValueExpression, out var remainingExpression))
			{
				return ExpressionHelper.ReplaceParametersIfNotNull(
					ExpressionHelper.ReplaceParameters(hasValueExpression, m_valueExpression),
					remainingExpression,
					m_context);
			}

			return (ExpressionHelper.ReplaceParameters(mapExpression, m_valueExpression), null);
		}

		private readonly Expression m_valueExpression;
		private readonly ImmutableStack<(string Name, object Value)> m_context;
		private readonly TryExtractValue m_tryExtractValue;
	}
}
