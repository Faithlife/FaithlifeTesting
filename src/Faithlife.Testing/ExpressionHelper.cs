using System.Linq.Expressions;
using System.Reflection;

namespace Faithlife.Testing
{
	internal static class ExpressionHelper
	{
		public static Expression ReplaceParameters(LambdaExpression target, Expression replacement)
		{
			return new ReplaceParameterWithExpressionVisitor(target.Parameters, replacement)
				.Visit(target.Body);
		}

		public static (Expression ValueExpression, string AssertMessage) ReplaceParametersIfNotNull(Expression hasValueExpression, LambdaExpression remainingExpression, IEnumerable<(string Name, object Value)> context)
		{
			return ((Expression, string)) s_replaceParametersIfNotNull.MakeGenericMethod(hasValueExpression.Type)
				.Invoke(null, new object[] { hasValueExpression, remainingExpression, context });
		}

		private static (Expression ResultExpression, string AssertMessage) ReplaceParametersIfNotNull<TIntermediate>(Expression hasValueExpression, LambdaExpression remainingExpression, IEnumerable<(string Name, object Value)> context)
		{
			var (value, message) = AssertEx.GetValueOrNullMessage(Expression.Lambda<Func<TIntermediate>>(hasValueExpression), context);

			if (message != null)
				return (null, message);

			var resultExpression = ReplaceParameters(
				remainingExpression,
				DebugValueExpressionVisitor.GetDebugExpresssion(remainingExpression.Parameters.Single().Name, value));

			return (resultExpression, null);
		}

		private sealed class ReplaceParameterWithExpressionVisitor : ExpressionVisitor
		{
			public ReplaceParameterWithExpressionVisitor(IEnumerable<ParameterExpression> oldParameters, Expression newExpression)
			{
				m_parameterMap = oldParameters
					.ToDictionary(p => p, _ => newExpression);
			}

			protected override Expression VisitParameter(ParameterExpression node) =>
				m_parameterMap.TryGetValue(node, out var replacement)
					? replacement
					: base.VisitParameter(node);

			private readonly Dictionary<ParameterExpression, Expression> m_parameterMap;
		}

		private static readonly MethodInfo s_replaceParametersIfNotNull = typeof(ExpressionHelper).GetMethod(nameof(ReplaceParametersIfNotNull), BindingFlags.NonPublic | BindingFlags.Static);
	}
}
