using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Faithlife.Utility;

namespace Faithlife.Testing
{
	internal sealed class DebugValueExpressionVisitor : ExpressionStringBuilder
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
				if (current is BinaryExpression { NodeType: ExpressionType.AndAlso } be)
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
				var (isNegated, mce) = branch is UnaryExpression { NodeType: ExpressionType.Not } ue
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
				_ => throw new InvalidOperationException($"Unexpected NodeType {node.NodeType} on BinaryExpression {node}"),
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
					if (me.Member is FieldInfo { IsPrivate: true, IsStatic: true } fi)
					{
						// Private static members need not be qualified
						chain.Add((fi.Name, current));
						foundTerminator = true;
						break;
					}

					// Identifiers containing __ are "reserved for use by the implementation" -- e.g. auto-generated and not interesting.
					if (me.Expression is ConstantExpression || (me.Expression is MemberExpression metoo && metoo.Member.Name.Contains("__")))
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
					while (e is TargetInvocationException { InnerException: { } } tie)
						e = tie.InnerException;

					if (e is not NullReferenceException || !chain.Any())
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


		// Includes Enumerable methods that (subjectively)
		// (a) are more about *transforming* an enumerable than making *assertions* about the content of the enumerable, and
		// (b) only have one "primary" argument being transformed.
		private static readonly HashSet<string> s_debugValueEnumerableMethods = new() { "Select", "Where", "SelectMany", "Take", "Skip", "TakeWhile", "SkipWhile", "OrderBy", "ThenBy", "OrderByDescending", "ThenByDescending", "Distinct", "Reverse", "AsEnumerable", "ToArray", "ToList", "ToDictionary", "ToLookup", "ToHashSet" };

		private enum Associativity
		{
			Left,
			Right,
		}

		private readonly Dictionary<string, object> m_debugValues = new();
	}
}
