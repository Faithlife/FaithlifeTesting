using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Faithlife.Json;
using Faithlife.Testing.TestFrameworks;
using Faithlife.Utility;
using Newtonsoft.Json.Linq;

namespace Faithlife.Testing
{
	/// <summary>
	/// Helper for writing assertions using expression trees.
	/// </summary>
	public static class AssertEx
	{
		/// <summary>
		/// Asserts that <paramref name="predicateExpression"/> returns `true`.
		/// </summary>
		public static void IsTrue(Expression<Func<bool>> predicateExpression)
		{
			if (predicateExpression == null)
				throw new ArgumentNullException(nameof(predicateExpression));

			var message = GetMessageIfFalse(predicateExpression, s_emptyContext);

			if (message != null)
				TestFrameworkProvider.Fail(message);
		}

		/// <summary>
		/// Starts a chain of assertions on <paramref name="value"/>.
		/// Asserts that <paramref name="value"/> is not `null`.
		/// </summary>
		public static Assertable<T> HasValue<T>(T value, string name = null)
			where T : class
		{
			var parameter = DebugValueExpressionVisitor.GetDebugExpresssion(name ?? "value", value);
			if (value == null)
			{
				TestFrameworkProvider.Fail(GetDiagnosticMessage(Expression.NotEqual(parameter, Expression.Constant(null, typeof(T))), null, s_emptyContext));
				return Assertable<T>.NoOp();
			}

			return Assertable<T>.FromValueExpression(value, parameter);
		}

		/// <summary>
		/// Starts a chain of assertions on the result of <paramref name="valueExpression"/>.
		/// Asserts that the result of <paramref name="valueExpression"/> is not `null`.
		/// </summary>
		public static Assertable<T> HasValue<T>(Expression<Func<T>> valueExpression)
			where T : class
		{
			if (valueExpression == null)
				throw new ArgumentNullException(nameof(valueExpression));

			var (value, message) = GetValueOrNullMessage(valueExpression, s_emptyContext);

			if (message != null)
			{
				TestFrameworkProvider.Fail(message);
				return Assertable<T>.NoOp();
			}

			return Assertable<T>.FromValueExpression(value, valueExpression.Body);
		}

		/// <summary>
		/// Asserts that the result of <paramref name="valueExpression"/> is not `null`.
		/// </summary>
		public static T HasValue<T>(Expression<Func<T?>> valueExpression)
			where T : struct
		{
			if (valueExpression == null)
				throw new ArgumentNullException(nameof(valueExpression));

			var (value, message) = GetValueOrNullMessage(valueExpression, s_emptyContext);

			if (!value.HasValue)
			{
				TestFrameworkProvider.Fail(message);

				// This only executes when the test-framework supports multiple assertions.
				throw new InvalidOperationException("Nullable object must have a value.");
			}

			return value.Value;
		}

		/// <summary>
		/// Asserts that <paramref name="assertionExpression" /> does not throw an exception.
		/// </summary>
		public static void DoesNotThrow(Expression<Action> assertionExpression)
		{
			if (assertionExpression == null)
				throw new ArgumentNullException(nameof(assertionExpression));

			var message = GetMessageIfException(assertionExpression, s_emptyContext);

			if (message != null)
				TestFrameworkProvider.Fail(message);
		}

		/// <summary>
		/// Retries <paramref name="action"/> until all assertions chained after this method pass.
		/// </summary>
		public static WaitUntilAssertable<T> WaitUntil<T>(Func<T> action, string name = null)
			where T : class
		{
			if (action == null)
				throw new ArgumentNullException(nameof(action));

			return new WaitUntilAssertable<T>(() => Task.FromResult(HasValue(action(), name)));
		}

		/// <summary>
		/// Retries <paramref name="actionAsync"/> until all assertions chained after this method pass.
		/// </summary>
		public static WaitUntilAssertable<T> WaitUntil<T>(Func<Task<T>> actionAsync, string name = null)
			where T : class
		{
			if (actionAsync == null)
				throw new ArgumentNullException(nameof(actionAsync));

			return new WaitUntilAssertable<T>(async () => HasValue(await actionAsync(), name));
		}

		/// <summary>
		/// Adds informational context to all assertions made within the `IDisposable` scope.
		/// </summary>
		public static IDisposable Context(string name, object value) => Context((name, value));

		/// <summary>
		/// Adds informational context to all assertions made within the `IDisposable` scope.
		/// </summary>
		public static IDisposable Context((string Name, object Value) first, params (string Key, object Value)[] rest) => Context(rest.Prepend(first));

		/// <summary>
		/// Adds informational context to all assertions made within the `IDisposable` scope.
		/// </summary>
		public static IDisposable Context<TValue>(IReadOnlyDictionary<string, TValue> data) => Context(data.Select(x => (x.Key, (object) x.Value)));

		/// <summary>
		/// Adds informational context to all assertions made within the `IDisposable` scope.
		/// </summary>
		public static IDisposable Context(object context) => Context(GetContextFromObject(context));

		/// <summary>
		/// Adds informational context to all assertions made within the `IDisposable` scope.
		/// </summary>
		public static IDisposable Context(params Expression<Func<object>>[] contextExpressions) => Context(GetContextFromExpressions(contextExpressions));

		/// <summary>
		/// Adds informational context to all assertions made within the `IDisposable` scope.
		/// </summary>
		public static IDisposable Context(IEnumerable<(string Name, object Value)> context)
		{
			if (context == null)
				throw new ArgumentNullException(nameof(context));

			s_contextStack.Value ??= s_emptyContext;

			var originalStack = s_contextStack.Value;

			foreach (var pair in context)
				s_contextStack.Value = s_contextStack.Value.Push(pair);

			return originalStack != s_contextStack.Value
				? Scope.Create(() => s_contextStack.Value = originalStack)
				: Scope.NoOp;
		}

		/// <summary>
		/// Asserts that <paramref name="mapExpression" /> does not return `null`
		/// and allows chaining further asserts on that <typeparamref name="T2"/> value.
		/// </summary>
		public static async Task<Assertable<T2>> HasValue<T1, T2>(this Task<Assertable<T1>> source, Expression<Func<T1, T2>> mapExpression)
			where T1 : class
			where T2 : class
			=> (await source).HasValue(mapExpression);

		/// <summary>
		/// Asserts that <paramref name="mapExpression" /> does not return `null`
		/// and allows chaining further asserts on that <typeparamref name="T2"/> value.
		/// </summary>
		public static async Task<T2> HasValue<T1, T2>(this Task<Assertable<T1>> source, Expression<Func<T1, T2?>> mapExpression)
			where T1 : class
			where T2 : struct
			=> (await source).HasValue(mapExpression);

		/// <summary>
		/// Asserts that <paramref name="predicateExpression" /> does not return `false`
		/// and allows chaining further asserts on the current value.
		/// </summary>
		public static async Task<Assertable<T>> IsTrue<T>(this Task<Assertable<T>> source, Expression<Func<T, bool>> predicateExpression)
			where T : class
			=> (await source).IsTrue(predicateExpression);

		/// <summary>
		/// Asserts that <paramref name="assertionExpression" /> does not throw an exception
		/// and allows chaining further asserts on the current value.
		/// </summary>
		public static async Task<Assertable<T>> DoesNotThrow<T>(this Task<Assertable<T>> source, Expression<Action<T>> assertionExpression)
			where T : class
			=> (await source).DoesNotThrow(assertionExpression);

		internal static IEnumerable<(string Name, object Value)> GetContextFromObject(object context)
		{
			if (context == null)
				throw new ArgumentNullException(nameof(context));

			return context.GetType()
				.GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Instance | BindingFlags.Public)
				.Select(prop => (prop.Name, prop.GetValue(context)));
		}

		internal static IEnumerable<(string Name, object Value)> GetContextFromExpressions(IEnumerable<Expression<Func<object>>> contextExpressions) =>
			contextExpressions.SelectMany(contextExpression =>
			{
				var (contextString, contextValues) = DebugValueExpressionVisitor.GetFullString(contextExpression.Body);

				return contextValues.Append((contextString, (object) new Lazy<object>(() =>
				{
					try
					{
						return contextExpression.Compile().DynamicInvoke();
					}
					catch (Exception ex)
					{
						return ex;
					}
				}, LazyThreadSafetyMode.ExecutionAndPublication)));
			});

		internal static (T Value, string AssertMessage) GetValueOrNullMessage<T>(Expression<Func<T>> valueExpression, IEnumerable<(string Name, object Value)> immediateContext)
		{
			var predicateFunc = valueExpression.Compile();
			Exception e = null;

			using (TestFrameworkProvider.GetIsolatedContext())
			{
				try
				{
					var value = predicateFunc.Invoke();
					if (value != null)
						return (value, null);
				}
				catch (Exception exception)
				{
					e = exception;
				}
			}

			return (default, GetDiagnosticMessage(
				e != null
					? valueExpression.Body
					: Expression.NotEqual(valueExpression.Body, Expression.Constant(null, typeof(T))),
				e,
				immediateContext));
		}

		internal static string GetMessageIfFalse(Expression<Func<bool>> predicateExpression, IEnumerable<(string Name, object Value)> immediateContext)
		{
			var predicateFunc = predicateExpression.Compile();
			Exception e = null;

			using (TestFrameworkProvider.GetIsolatedContext())
			{
				try
				{
					if (predicateFunc.Invoke())
						return null;
				}
				catch (Exception exception)
				{
					e = exception;
				}
			}

			return GetDiagnosticMessage(predicateExpression.Body, e, immediateContext);
		}

		internal static string GetMessageIfException(Expression<Action> actionExpression, IEnumerable<(string Name, object Value)> immediateContext)
		{
			var actionFunc = actionExpression.Compile();
			Exception e;

			using (TestFrameworkProvider.GetIsolatedContext())
			{
				try
				{
					actionFunc.Invoke();
					return null;
				}
				catch (Exception exception)
				{
					e = exception;
				}
			}

			return GetDiagnosticMessage(actionExpression.Body, e, immediateContext);
		}

		internal static string GetDiagnosticMessage(Expression body, Exception e, IEnumerable<(string Name, object Value)> immediateContextValues)
		{
			var (bodyString, bodyValues) = DebugValueExpressionVisitor.GetDiagnosticString(body);
			return GetDiagnosticMessage(bodyString, bodyValues, e, immediateContextValues);
		}

		internal static string GetDiagnosticMessage(string expected, IReadOnlyCollection<(string Name, object Value)> actualValues, Exception e, IEnumerable<(string Name, object Value)> immediateContextValues)
		{
			var sb = new StringBuilder();
			sb.AppendLineLf("Expected:");
			sb.Append('\t');
			sb.Append(expected);

			if (actualValues.Any())
			{
				sb.AppendLineLf();
				sb.AppendLineLf();
				sb.Append("Actual:");

				foreach (var (name, value) in actualValues)
				{
					sb.AppendLineLf();
					sb.Append($"\t{name} = {ToString(value)}");
				}
			}

			var allContextValues = new List<(string Name, string Value)>();
			foreach (var (name, value) in immediateContextValues.Concat(s_contextStack.Value ?? Enumerable.Empty<(string Name, object Value)>()))
			{
				if (actualValues.All(v => v.Name != name) && allContextValues.All(v => v.Name != name))
					allContextValues.Add((name, ToString(value)));
			}

			if (allContextValues.Any())
			{
				sb.AppendLineLf();
				sb.AppendLineLf();
				sb.Append("Context:");

				foreach (var (name, value) in allContextValues)
				{
					sb.AppendLineLf();
					sb.Append($"\t{name} = {value}");
				}
			}

			if (e != null)
			{
				sb.AppendLineLf();
				sb.AppendLineLf();
				sb.Append(e.GetType());
				sb.Append(": ");
				sb.AppendLineLf(e.Message);
				sb.Append(e.StackTrace);
			}

			return sb.ToString();
		}

		private static string ToString(object obj)
		{
			while (obj is TargetInvocationException { InnerException: { } } tie)
				obj = tie.InnerException;

			switch (obj)
			{
				case null:
					return "null";
				case bool b:
					return b ? "true" : "false";
				case string s:
					return $@"""{s}""";
				case Exception e:
					return $"[{e.GetType()}: {e.Message}]";
			}

			var type = obj.GetType();

			if (type.IsGenericType)
			{
				var genericType = type.GetGenericTypeDefinition();

				if (genericType == typeof(Lazy<>))
					return Property("Value");

				if (genericType == typeof(ValueTuple<>))
					return $"({Field("Item1")})";
				if (genericType == typeof(ValueTuple<,>))
					return $"({Field("Item1")}, {Field("Item2")})";
				if (genericType == typeof(ValueTuple<,,>))
					return $"({Field("Item1")}, {Field("Item2")}, {Field("Item3")})";
				if (genericType == typeof(ValueTuple<,,,>))
					return $"({Field("Item1")}, {Field("Item2")}, {Field("Item3")}, {Field("Item4")})";
				if (genericType == typeof(ValueTuple<,,,,>))
					return $"({Field("Item1")}, {Field("Item2")}, {Field("Item3")}, {Field("Item4")}, {Field("Item5")})";
				if (genericType == typeof(ValueTuple<,,,,,>))
					return $"({Field("Item1")}, {Field("Item2")}, {Field("Item3")}, {Field("Item4")}, {Field("Item5")}, {Field("Item6")})";
				if (genericType == typeof(ValueTuple<,,,,,,>))
					return $"({Field("Item1")}, {Field("Item2")}, {Field("Item3")}, {Field("Item4")}, {Field("Item5")}, {Field("Item6")}, {Field("Item7")})";
				if (genericType == typeof(ValueTuple<,,,,,,,>))
					return $"({Field("Item1")}, {Field("Item2")}, {Field("Item3")}, {Field("Item4")}, {Field("Item5")}, {Field("Item6")}, {Field("Item7")}, {Field("Rest").TrimStart('(')}";

				string Property(string name) => ToString((type.GetProperty(name) ?? throw new InvalidOperationException($"Unexpected Error: No property {name} on type {type.Name}")).GetValue(obj));
				string Field(string name) => ToString((type.GetField(name) ?? throw new InvalidOperationException($"Unexpected Error: No field {name} on type {type.Name}")).GetValue(obj));
			}

			var toString = obj.ToString();

			// This means the class doesn't override ToString by itself.
			if (toString == type.ToString())
			{
				try
				{
					return ToPrettyJson(JsonUtility.ToJToken(obj), 1).Trim();
				}
				catch (Exception ex)
				{
					return $"<{toString}> [{ex.GetType()}: {ex.Message}]";
				}
			}

			return toString;
		}

		private static string ToPrettyJson(JToken token, int tabLevel)
		{
			var indent = new string('\t', tabLevel);
			switch (token.Type)
			{
				case JTokenType.Array:
				{
					var children = token.Children();
					if (!children.Any())
						return indent + "[]";

					var childLines = children.Select(c => ToPrettyJson(c, tabLevel + 1)).AsReadOnlyList();
					var shortJson = '[' + childLines.Select(l => l.Trim()).Join(", ") + ']';
					if (shortJson.Length + tabLevel * 4 < c_maxJsonLength)
						return indent + shortJson;

					return indent + "[\n" + childLines.Join(",\n") + '\n' + indent + "]";
				}
				case JTokenType.Object:
				{
					var children = token.Children<JProperty>();
					if (!children.Any())
						return indent + "{}";

					var childLines = children.Select(c => ToPrettyJson(c, tabLevel + 1)).AsReadOnlyList();
					var shortJson = "{ " + childLines.Select(l => l.Trim()).Join(", ") + " }";
					if (shortJson.Length + tabLevel * 4 < c_maxJsonLength)
						return indent + shortJson;

					return indent + "{\n" + childLines.Join(",\n") + '\n' + indent + "}";
				}
				case JTokenType.Property:
				{
					var prop = token as JProperty;
					return indent + '"' + prop.Name + "\": " + ToPrettyJson(prop.Value, tabLevel).Trim();
				}
				default:
				{
					return indent + JsonUtility.ToJson(token);
				}
			}
		}

		private const int c_maxJsonLength = 100;

		private static readonly AsyncLocal<ImmutableStack<(string Name, object Value)>> s_contextStack = new();
		private static readonly ImmutableStack<(string Name, object Value)> s_emptyContext = ImmutableStack<(string Name, object Value)>.Empty;
	}
}
