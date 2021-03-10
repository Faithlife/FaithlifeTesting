using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Faithlife.Json;
using Faithlife.Utility;
using Newtonsoft.Json.Linq;
using NUnit.Framework.Internal;

namespace Faithlife.Testing
{
	public static class AssertEx
	{
		public static void Assert(Expression<Func<bool>> predicateExpression)
		{
			if (predicateExpression == null)
				throw new ArgumentNullException(nameof(predicateExpression));

			var message = GetMessageIfFalse(predicateExpression, ImmutableStack<(string Name, object Value)>.Empty);

			if (message != null)
				NUnit.Framework.Assert.Fail(message);
		}

		public static Builder<T> Select<T>(T value)
			where T : class
		{
			if (value == null)
			{
				NUnit.Framework.Assert.Fail(GetMessage(() => value));

				static string GetMessage(Expression<Func<T>> valueExpression)
					=> GetDiagnosticMessage(valueExpression.Body, null, ImmutableStack<(string Name, object Value)>.Empty);
			}

			return new Builder<T>(value, () => value, ImmutableStack<(string Name, object Value)>.Empty);
		}

		public static Builder<T> Select<T>(Expression<Func<T>> valueExpression)
			where T : class
		{
			if (valueExpression == null)
				throw new ArgumentNullException(nameof(valueExpression));

			var (value, message) = GetValueOrNullMessage(valueExpression, ImmutableStack<(string Name, object Value)>.Empty);

			if (message != null)
				NUnit.Framework.Assert.Fail(message);

			return new Builder<T>(value, valueExpression, ImmutableStack<(string Name, object Value)>.Empty);
		}

		public static T Select<T>(Expression<Func<T?>> valueExpression)
			where T : struct
		{
			if (valueExpression == null)
				throw new ArgumentNullException(nameof(valueExpression));

			var (value, message) = GetValueOrNullMessage(valueExpression, ImmutableStack<(string Name, object Value)>.Empty);

			if (!value.HasValue)
				NUnit.Framework.Assert.Fail(message);

			return value.Value;
		}

		public static IDisposable Context(string name, object value) => Context((name, value));
		public static IDisposable Context((string Name, object Value) first, params (string Key, object Value)[] rest) => Context(rest.Prepend(first));
		public static IDisposable Context<TValue>(IReadOnlyDictionary<string, TValue> data) => Context(data.Select(x => (x.Key, (object) x.Value)));
		public static IDisposable Context(object context) => Context(GetContextFromObject(context));
		public static IDisposable Context(params Expression<Func<object>>[] contextExpressions) => Context(GetContextFromExpressions(contextExpressions));

		public static IDisposable Context(IEnumerable<(string Name, object Value)> context)
		{
			if (context == null)
				throw new ArgumentNullException(nameof(context));

			s_contextStack.Value ??= ImmutableStack<(string Name, object Value)>.Empty;

			var originalStack = s_contextStack.Value;

			foreach (var pair in context)
				s_contextStack.Value = s_contextStack.Value.Push(pair);

			if (originalStack == s_contextStack.Value)
				throw new ArgumentException("Must provide more context! Sequence contains no elements.", nameof(context));

			return Scope.Create(() => s_contextStack.Value = originalStack);
		}

		public static async Task<Builder<T2>> Select<T1, T2>(this Task<Builder<T1>> source, Expression<Func<T1, T2>> mapExpression)
			where T1 : class
			where T2 : class
			=> (await source).Select(mapExpression);

		public static async Task<T2> Select<T1, T2>(this Task<Builder<T1>> source, Expression<Func<T1, T2?>> mapExpression)
			where T1 : class
			where T2 : struct
			=> (await source).Select(mapExpression);

		public static async Task<Builder<T1>> Assert<T1>(this Task<Builder<T1>> source, Expression<Func<T1, bool>> predicateExpression)
			where T1 : class
			=> (await source).Assert(predicateExpression);

		public sealed class Builder<T1>
			where T1 : class
		{
			internal Builder(T1 value, Expression<Func<T1>> valueExpression, ImmutableStack<(string Name, object Value)> context)
			{
				Value = value;
				m_valueExpression = valueExpression;
				m_context = context;
			}

			public Builder<T2> Select<T2>(Expression<Func<T1, T2>> mapExpression)
				where T2 : class
			{
				if (mapExpression == null)
					throw new ArgumentNullException(nameof(mapExpression));

				var valueExpression = CoalesceWith(mapExpression);

				var (value, message) = GetValueOrNullMessage(valueExpression, m_context);

				if (message != null)
					NUnit.Framework.Assert.Fail(message);

				return new Builder<T2>(value, valueExpression, m_context);
			}

			public T2 Select<T2>(Expression<Func<T1, T2?>> mapExpression)
				where T2 : struct
			{
				if (mapExpression == null)
					throw new ArgumentNullException(nameof(mapExpression));

				var valueExpression = CoalesceWith(mapExpression);

				var (value, message) = GetValueOrNullMessage(valueExpression, m_context);

				if (message != null)
					NUnit.Framework.Assert.Fail(message);

				return value.Value;
			}

			public Builder<T1> Assert(Expression<Func<T1, bool>> predicateExpression)
			{
				if (predicateExpression == null)
					throw new ArgumentNullException(nameof(predicateExpression));

				var message = GetMessageIfFalse(CoalesceWith(predicateExpression), m_context);

				if (message != null)
					NUnit.Framework.Assert.Fail(message);

				return this;
			}

			public Builder<T1> Assert(Expression<Action<T1>> assertionExpression)
			{
				if (assertionExpression == null)
					throw new ArgumentNullException(nameof(assertionExpression));

				var visitor = new ReplaceParameterWithExpressionVisitor(assertionExpression.Parameters, m_valueExpression.Body);
				var coalescedAssertion = Expression.Lambda<Action>(visitor.Visit(assertionExpression.Body));
				var assertFunc = coalescedAssertion.Compile();

				try
				{
					assertFunc();
				}
				catch (Exception exception)
				{
					NUnit.Framework.Assert.Fail(GetDiagnosticMessage(coalescedAssertion.Body, exception, m_context));
				}

				return this;
			}

			public Builder<T1> Context(string name, object value) => Context((name, value));
			public Builder<T1> Context((string Name, object Value) first, params (string Key, object Value)[] rest) => Context(rest.Prepend(first));
			public Builder<T1> Context<TValue>(IReadOnlyDictionary<string, TValue> data) => Context(data.Select(x => (x.Key, (object) x.Value)));
			public Builder<T1> Context(object context) => Context(GetContextFromObject(context));
			public Builder<T1> Context(params Expression<Func<object>>[] contextExpressions) => Context(GetContextFromExpressions(contextExpressions));

			public Builder<T1> Context(IEnumerable<(string Name, object Value)> context)
			{
				if (context == null)
					throw new ArgumentNullException(nameof(context));

				var newContext = m_context;

				foreach (var pair in context)
					newContext = newContext.Push(pair);

				if (newContext == m_context)
					throw new ArgumentException("Must provide more context! Sequence contains no elements.", nameof(context));

				return new Builder<T1>(Value, m_valueExpression, newContext);
			}

			public T1 Value { get; }

			public static implicit operator T1(Builder<T1> source) => source?.Value ?? throw new ArgumentNullException(nameof(source));

			private Expression<Func<T2>> CoalesceWith<T2>(Expression<Func<T1, T2>> mapExpression)
			{
				var visitor = new ReplaceParameterWithExpressionVisitor(mapExpression.Parameters, m_valueExpression.Body);
				return Expression.Lambda<Func<T2>>(visitor.Visit(mapExpression.Body));
			}

			private sealed class ReplaceParameterWithExpressionVisitor : ExpressionVisitor
			{
				public ReplaceParameterWithExpressionVisitor(IEnumerable<ParameterExpression> oldParameters, Expression newExpression)
				{
					m_parameterMap = oldParameters
						.ToDictionary(p => p, p => newExpression);
				}

				protected override Expression VisitParameter(ParameterExpression parameter) =>
					m_parameterMap.TryGetValue(parameter, out var replacement)
						? replacement
						: base.VisitParameter(parameter);

				private readonly IDictionary<ParameterExpression, Expression> m_parameterMap;
			}

			private readonly Expression<Func<T1>> m_valueExpression;
			private readonly ImmutableStack<(string Name, object Value)> m_context;
		}

		private static IEnumerable<(string Name, object Value)> GetContextFromObject(object context) =>
			context.GetType()
				.GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Instance | BindingFlags.Public)
				.Select(prop => (prop.Name, prop.GetValue(context)));

		private static IEnumerable<(string Name, object Value)> GetContextFromExpressions(IEnumerable<Expression<Func<object>>> contextExpressions) =>
			contextExpressions.SelectMany(contextExpression =>
			{
				var (contextString, contextValues) = DebugValueExpressionVisitor.BodyToString(contextExpression.Body);

				return contextValues.Append((contextString, (object) new Lazy<object>(() =>
				{
					try
					{
						return contextExpression.Compile().DynamicInvoke();
					}
					catch (Exception ex)
					{
						return (object) ex;
					}
				}, LazyThreadSafetyMode.ExecutionAndPublication)));
			});

		private static (T Value, string AssertMessage) GetValueOrNullMessage<T>(Expression<Func<T>> valueExpression, IEnumerable<(string Name, object Value)> immediateContext)
		{
			var predicateFunc = valueExpression.Compile();
			Exception e = null;

			using (new TestExecutionContext.IsolatedContext())
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

			return (default, GetDiagnosticMessage(valueExpression.Body, e, immediateContext));
		}

		private static string GetMessageIfFalse(Expression<Func<bool>> predicateExpression, IEnumerable<(string Name, object Value)> immediateContext)
		{
			var predicateFunc = predicateExpression.Compile();
			Exception e = null;

			using (new TestExecutionContext.IsolatedContext())
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

		private static string GetDiagnosticMessage(Expression body, Exception e, IEnumerable<(string Name, object Value)> immediateContext)
		{
			var (bodyString, bodyValues) = DebugValueExpressionVisitor.GetDiagnosticMessage(body);

			var sb = new StringBuilder();
			sb.AppendLineLf("Expected:");
			sb.Append('\t');
			sb.Append(bodyString);

			var debugValues = bodyValues.ToList();
			if (debugValues.Any())
			{
				sb.AppendLineLf();
				sb.AppendLineLf();
				sb.Append("Actual:");

				foreach (var (name, value) in debugValues)
				{
					sb.AppendLineLf();
					sb.Append($"\t{name} = {ToString(value)}");
				}
			}

			var allContextValues = new List<(string Name, string Value)>();
			foreach (var (name, value) in immediateContext.Concat(s_contextStack.Value ?? Enumerable.Empty<(string Name, object Value)>()))
			{
				if (debugValues.All(v => v.Name != name) && allContextValues.All(v => v.Name != name))
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

		private sealed class DebugValueExpressionVisitor : ExpressionStringBuilder
		{
			private const int c_andPrecedence = 9;

			public static (string BodyString, IReadOnlyCollection<(string Name, object Value)> BodyValues) BodyToString(Expression body)
			{
				/*
				 * TODO Cleanups
				 *
				 * AutoWebService URL, method, (headers, body?)
				 * Single boolean member-access expressions append an `== true`?
				 * ToString for ValueTuples
				 * Pretty-printing values for LINQ .First, .Single, .Select, etc.
				 *
				 */
				var vistor = new DebugValueExpressionVisitor();
				vistor.Visit(body);
				return (vistor.ToString(), vistor.DebugValues);
			}

			public static (string BodyString, IReadOnlyCollection<(string Name, object Value)> BodyValues) GetDiagnosticMessage(Expression body)
			{
				var vistor = new DebugValueExpressionVisitor();
				vistor.VisitDiagnosticRoot(body);
				return (vistor.ToString(), vistor.DebugValues);
			}

			private void VisitDiagnosticRoot(Expression body)
			{
				if (body.Type != typeof(bool))
				{
					Visit(body);
					return;
				}

				// A chain of "ands" at the root of our assert gets special treatment; only show the false ones, and wrap if there are more than one.
				var andedExpressions = new List<Expression>();
				var stack = new Stack<Expression>();
				stack.Push(body);
				while (stack.Any())
				{
					var current = stack.Pop();
					if (current is BinaryExpression be && be.NodeType == ExpressionType.AndAlso)
					{
						stack.Push(be.Right);
						stack.Push(be.Left);
					}
					else
					{
						andedExpressions.Add(current);
					}
				}

				if (andedExpressions.Count <= 1)
				{
					// If there is no "chain" of ands, just visit normally.
					VisitBranch(body);
				}
				else
				{
					var isFirst = true;
					foreach (var anded in andedExpressions)
					{
						var isException = false;
						try
						{
							var value = Expression.Lambda<Func<bool>>(anded).Compile().Invoke();

							// Skip and-ed values that are true. They are boring.
							if (value)
								continue;
						}
						catch (Exception)
						{
							// If previous expressions were false, assume the expression was a null-check or other precondition.
							if (!isFirst)
								break;

							isException = true;
						}

						if (isFirst)
							isFirst = false;
						else
							Out("\n\t&& ");

						// Preserve the precedence of the "and" operator when visiting children
						if (anded is BinaryExpression be)
							VisitBinary(be, c_andPrecedence, null);
						else
							VisitBranch(anded);

						// All values after we get an exception are undefined, so skip them.
						if (isException)
							break;
					}
				}

				void VisitBranch(Expression branch)
				{
					// For `seq.All(a => [predicate])` and `!seq.Any(a => [predicate])`,
					// output the values in the sequence that caused the All/Any to fail.
					var (isNegated, mce) = branch is UnaryExpression ue && ue.NodeType == ExpressionType.Not
						? (true, ue.Operand as MethodCallExpression)
						: (false, branch as MethodCallExpression);

					if (mce != null
						&& mce.Method.IsStatic
						&& mce.Method.DeclaringType == typeof(Enumerable)
						&& mce.Arguments.Count == 2
						&& ((isNegated && mce.Method.Name == "Any") || (!isNegated && mce.Method.Name == "All"))
						&& TryExtractDebugValue(mce.Arguments[0], m_debugValues, out var expressionText))
					{
						var sequence = (IEnumerable) Expression.Lambda(mce.Arguments[0]).Compile().DynamicInvoke();
						var predicate = ((LambdaExpression) mce.Arguments[1]).Compile();
						m_debugValues.Remove(expressionText);

						try
						{
							var enumerator = sequence.GetEnumerator();

							var index = 0;
							while (enumerator.MoveNext())
							{
								var value = (bool) predicate.DynamicInvoke(enumerator.Current);

								if (value == isNegated)
									m_debugValues[$"{expressionText}[{index}]"] = enumerator.Current;

								index++;
							}

							if (isNegated)
								Out("!");

							Out(expressionText);
							Out(".");
							Out(mce.Method.Name);
							Out("(");
							Visit(mce.Arguments[1]);
							Out(")");
						}
						catch
						{
							Visit(branch);
						}
					}
					else
					{
						Visit(branch);
					}
				}
			}

			protected override Expression VisitUnary(UnaryExpression node)
			{
				switch (node.NodeType)
				{
					case ExpressionType.Convert:
						return Visit(node.Operand);
					case ExpressionType.Not:
						Out("!");
						return Visit(node.Operand);
					default:
						return base.VisitUnary(node);
				}
			}

			protected override Expression VisitBinary(BinaryExpression node)
			{
				return VisitBinary(node, null, null);
			}

			private Expression VisitBinary(BinaryExpression node, int? parentPrecedence, Associativity? parentSide)
			{
				if (node.NodeType == ExpressionType.ArrayIndex)
					return base.VisitBinary(node);

				// Precedence from https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/operators/
				const int assignmentOperators = 12;
				var (op, precedence) = node.NodeType switch
				{
					ExpressionType.Divide => ("/", 1),
					ExpressionType.Multiply => ("*", 1),
					ExpressionType.Modulo => ("%", 1),
					ExpressionType.MultiplyChecked => ("*", 1),
					ExpressionType.Power => ("^", 1),
					ExpressionType.Subtract => ("-", 2),
					ExpressionType.Add => ("+", 2),
					ExpressionType.AddChecked => ("+", 2),
					ExpressionType.SubtractChecked => ("-", 2),
					ExpressionType.LeftShift => ("<<", 3),
					ExpressionType.RightShift => (">>", 3),
					ExpressionType.GreaterThan => (">", 4),
					ExpressionType.LessThan => ("<", 4),
					ExpressionType.GreaterThanOrEqual => (">=", 4),
					ExpressionType.LessThanOrEqual => ("<=", 4),
					ExpressionType.Equal => ("==", 5),
					ExpressionType.NotEqual => ("!=", 5),
					ExpressionType.And => ("&", 6),
					ExpressionType.ExclusiveOr => ("^", 7),
					ExpressionType.Or => ("|", 8),
					ExpressionType.AndAlso => ("&&", c_andPrecedence),
					ExpressionType.OrElse => ("||", 10),
					ExpressionType.Coalesce => ("??", 11),
					ExpressionType.Assign => ("=", assignmentOperators),
					ExpressionType.AddAssign => ("+=", assignmentOperators),
					ExpressionType.AddAssignChecked => ("+=", assignmentOperators),
					ExpressionType.SubtractAssign => ("-=", assignmentOperators),
					ExpressionType.SubtractAssignChecked => ("-=", assignmentOperators),
					ExpressionType.DivideAssign => ("/=", assignmentOperators),
					ExpressionType.ModuloAssign => ("%=", assignmentOperators),
					ExpressionType.MultiplyAssign => ("*=", assignmentOperators),
					ExpressionType.MultiplyAssignChecked => ("*=", assignmentOperators),
					ExpressionType.LeftShiftAssign => ("<<=", assignmentOperators),
					ExpressionType.RightShiftAssign => (">>=", assignmentOperators),
					ExpressionType.AndAssign => (node.Type == typeof(bool) || node.Type == typeof(bool?) ? "&&=" : "&=", assignmentOperators),
					ExpressionType.OrAssign => (node.Type == typeof(bool) || node.Type == typeof(bool?) ? "||=" : "|=", assignmentOperators),
					ExpressionType.ExclusiveOrAssign => ("^=", assignmentOperators),
					ExpressionType.PowerAssign => ("**=", assignmentOperators),
					_ => throw new InvalidOperationException($"Unexpected NodeType {node.NodeType} on BinaryExpression {node}")
				};

				var associativity = precedence == assignmentOperators || node.NodeType == ExpressionType.Coalesce
					? Associativity.Right
					: Associativity.Left;

				var needsParentheses = parentPrecedence.HasValue && (precedence > parentPrecedence || (parentSide.HasValue && precedence == parentPrecedence && associativity != parentSide));
				if (needsParentheses)
					Out("(");

				if (node.Left is BinaryExpression left)
					VisitBinary(left, precedence, Associativity.Left);
				else
					Visit(node.Left);

				Out(" ");
				Out(op);
				Out(" ");

				if (node.Right is BinaryExpression right)
					VisitBinary(right, precedence, Associativity.Right);
				else
					Visit(node.Right);

				if (needsParentheses)
					Out(")");

				return node;
			}

			protected override Expression VisitConstant(ConstantExpression constant)
			{
				if (constant.Type == typeof(bool))
				{
					Out(((bool) constant.Value) ? "true" : "false");
					return constant;
				}

				return base.VisitConstant(constant);
			}

			protected override Expression VisitMethodCall(MethodCallExpression methodCall)
			{
				// Indexer methods
				if (methodCall.Arguments.Count == 1 && methodCall.Method.Name == "get_Item" && methodCall.Method.IsSpecialName)
				{
					var argument = methodCall.Arguments.Single();

					Visit(methodCall.Object);
					Out("[");
					Visit(argument);
					Out("]");

					return methodCall;
				}

				if (TryExtractDebugValue(methodCall, m_debugValues, out var expressionText))
				{
					Out(expressionText);

					return methodCall;
				}

				return base.VisitMethodCall(methodCall);
			}

			protected override Expression VisitMember(MemberExpression member)
			{
				if (TryExtractDebugValue(member, m_debugValues, out var name))
				{
					Out(name);

					return member;
				}

				return base.VisitMember(member);
			}

			public IReadOnlyCollection<(string Name, object Value)> DebugValues => m_debugValues.Select(kvp => (kvp.Key, kvp.Value)).ToList().AsReadOnly();

			private enum Associativity
			{
				Left,
				Right,
			}

			private readonly Dictionary<string, object> m_debugValues = new Dictionary<string, object>();
		}

		private static bool TryExtractDebugValue(Expression expression, Dictionary<string, object> debugValues, out string expressionText)
		{
			// If there is a chain of member-access expressions ending in a value, display the value of the root property.
			var current = expression;
			var chain = new List<(string Text, Expression Expression)>();
			var foundTerminator = false;
			var addedToChain = true;
			while (!foundTerminator && addedToChain)
			{
				addedToChain = false;
				while (current is MemberExpression me)
				{
					if (me.Member is FieldInfo fi && fi.IsPrivate && fi.IsStatic)
					{
						// Private static members need not be qualified
						chain.Add((fi.Name, current));
						foundTerminator = true;
						break;
					}

					if (me.Expression is ConstantExpression)
					{
						// Captured varaibles in lambdas are represented as members on an auto-generated <>c__DisplayClass
						// Captured members are also constants
						chain.Add((me.Member.Name, current));
						foundTerminator = true;
						break;
					}

					chain.Add(("." + me.Member.Name, me));
					current = me.Expression;
					addedToChain = true;
				}

				while (current is MethodCallExpression mce && mce.Method.IsStatic && mce.Method.DeclaringType == typeof(Enumerable) && s_debugValueEnumerableMethods.Contains(mce.Method.Name))
				{
					var sb = new StringBuilder();
					sb.Append('.');
					sb.Append(mce.Method.Name);
					sb.Append('(');

					for (var i = 1; i < mce.Arguments.Count; i++)
					{
						var (text, newDebugValues) = DebugValueExpressionVisitor.BodyToString(mce.Arguments[i]);

						if (i != 1)
							sb.Append(", ");

						sb.Append(text);

						foreach (var (newName, newValue) in newDebugValues)
						{
							if (!debugValues.ContainsKey(newName))
								debugValues[newName] = newValue;
						}
					}

					sb.Append(')');

					chain.Add((sb.ToString(), mce));
					current = mce.Arguments[0];
					addedToChain = true;
				}
			}

			if (!foundTerminator)
			{
				expressionText = null;
				return false;
			}

			expressionText = chain.Select(c => c.Text).Reverse().Join("");
			var name = expressionText;

			while (true)
			{
				// Don't compile stuff we've already outpat
				if (debugValues.ContainsKey(name))
					return true;

				try
				{
					var value = Expression.Lambda(chain[0].Expression).Compile().DynamicInvoke();

					debugValues.Add(name, value);
					return true;
				}
				catch (Exception e)
				{
					while (e is TargetInvocationException tie && tie.InnerException != null)
						e = tie.InnerException;

					if (!(e is NullReferenceException) || !chain.Any())
					{
						debugValues.Add(name, e);
						return true;
					}
				}

				// Pop the top off our chain to find the source of the NRE
				chain.RemoveAt(0);
				name = chain.Select(c => c.Text).Reverse().Join("");
			}
		}

		private static string ToString(object obj)
		{
			while (obj is TargetInvocationException tie && tie.InnerException != null)
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
					return $"({Property("Item1")})";
				if (genericType == typeof(ValueTuple<,>))
					return $"({Property("Item1")}, {Property("Item2")})";
				if (genericType == typeof(ValueTuple<,,>))
					return $"({Property("Item1")}, {Property("Item2")}, {Property("Item3")})";
				if (genericType == typeof(ValueTuple<,,,>))
					return $"({Property("Item1")}, {Property("Item2")}, {Property("Item3")}, {Property("Item4")})";
				if (genericType == typeof(ValueTuple<,,,,>))
					return $"({Property("Item1")}, {Property("Item2")}, {Property("Item3")}, {Property("Item4")}, {Property("Item5")})";
				if (genericType == typeof(ValueTuple<,,,,,>))
					return $"({Property("Item1")}, {Property("Item2")}, {Property("Item3")}, {Property("Item4")}, {Property("Item5")}, {Property("Item6")})";
				if (genericType == typeof(ValueTuple<,,,,,,>))
					return $"({Property("Item1")}, {Property("Item2")}, {Property("Item3")}, {Property("Item4")}, {Property("Item5")}, {Property("Item6")}, {Property("Item7")})";
				if (genericType == typeof(ValueTuple<,,,,,,,>))
					return $"({Property("Item1")}, {Property("Item2")}, {Property("Item3")}, {Property("Item4")}, {Property("Item5")}, {Property("Item6")}, {Property("Item7")}, {Property("Rest").TrimStart('(')}";

				string Property(string name) => ToString(type.GetProperty(name).GetValue(obj));
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
			if (token.Type == JTokenType.Array)
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

			if (token.Type == JTokenType.Object)
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

			if (token.Type == JTokenType.Property)
			{
				var prop = token as JProperty;
				return indent + '"' + prop.Name + "\": " + ToPrettyJson(prop.Value, tabLevel).Trim();
			}

			return indent + JsonUtility.ToJson(token);
		}

		private const int c_maxJsonLength = 100;

		// Includes Enumerable methods that (subjectively)
		// (a) are more about *transforming* an enumerable than making *assertions* about the content of the enumerable, and
		// (b) only have one "primary" argument being transformed.
		private static readonly HashSet<string> s_debugValueEnumerableMethods = new HashSet<string> { "Select", "Where", "SelectMany", "Take", "Skip", "TakeWhile", "SkipWhile", "OrderBy", "ThenBy", "OrderByDescending", "ThenByDescending", "Distinct", "Reverse", "AsEnumerable", "ToArray", "ToList", "ToDictionary", "ToLookup", "ToHashSet" };

		private static readonly AsyncLocal<ImmutableStack<(string Name, object Value)>> s_contextStack = new AsyncLocal<ImmutableStack<(string Name, object Value)>>();
	}
}
