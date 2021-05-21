using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Faithlife.Reflection;
using Faithlife.Testing.TestFrameworks;
using Faithlife.WebRequests;
using Faithlife.WebRequests.Json;

namespace Faithlife.Testing
{
	public static class WebServiceResponseExtensions
	{
		public static async Task<Assertable<TResponse>> AssertResponse<TResponse>(this Task<TResponse> response)
			where TResponse : AutoWebServiceResponse
		{
			if (response == null)
				throw new ArgumentNullException(nameof(response));

			return (await response).AssertResponse();
		}

		public static WaitUntilAssertable<TResponse> AssertResponse<TResponse>(this WaitUntilAssertable<TResponse> response)
			where TResponse : AutoWebServiceResponse
		{
			if (response == null)
				throw new ArgumentNullException(nameof(response));

			return response.Apply(a => a.Value.AssertResponse());
		}

		public static Assertable<TResponse> AssertResponse<TResponse>(this TResponse response)
			where TResponse : AutoWebServiceResponse
		{
			if (response == null)
				throw new ArgumentNullException(nameof(response));

			var exception = response.CreateException();
			var statusCodeString = exception.ResponseStatusCode?.ToString();

			var properties = DtoInfo.GetInfo(typeof(TResponse))
				.Properties
				.Select(p => (p.Name, IsContent: IsContentProperty(p.Name), Value: p.GetValue(response)))
				.Where(p => !IsDefault(p.Value))
				.ToList();

			var assertable = AssertEx.HasValue(response, "response")
				.Context(GetContext(exception, properties));

			if (properties.Any(p => p.IsContent && p.Value.GetType().IsClass))
				assertable = assertable.WithExtrator(TryExtractStatusProperty);

			return assertable;

			bool TryExtractStatusProperty(
				LambdaExpression sourceExpression,
				out LambdaExpression hasValueExpression,
				out LambdaExpression remainingExpression)
			{
				hasValueExpression = null;
				remainingExpression = null;

				var sourceParameter = sourceExpression.Parameters.Single();
				var visitor = new MemberReplacingExpressionVisitor(sourceParameter, "response");
				var replacedBody = visitor.Visit(sourceExpression.Body);

				if (!visitor.TryGetSingleClassParameter(out var memberInfo, out var responseParameter) || !IsContentProperty(memberInfo.Name))
					return false;

				hasValueExpression = Expression.Lambda(Expression.MakeMemberAccess(sourceParameter, memberInfo), sourceParameter);
				remainingExpression = Expression.Lambda(replacedBody, responseParameter);

				return true;
			}

			// Logic matches https://github.com/Faithlife/FaithlifeWebRequests/blob/5d04e85c62ae0ccea2ca7e45f5d40d650a7acd0b/src/Faithlife.WebRequests/Json/AutoWebServiceRequest.cs#L142
			bool IsContentProperty(string propertyName)
				=> string.Equals(statusCodeString, propertyName, StringComparison.OrdinalIgnoreCase);
		}

		private static IEnumerable<(string Name, object Value)> GetContext(WebServiceException exception, IEnumerable<(string Name, bool IsContent, object Value)> properties)
		{
			yield return ("request", $"{exception.RequestMethod} {exception.RequestUri.AbsoluteUri} (status {exception.ResponseStatusCode})");

			var contextByIsContent = properties
				.ToLookup(p => p.IsContent);

			foreach (var (name, _, value) in contextByIsContent[false])
				yield return ("response." + name, value);

			if (contextByIsContent[true].All(p => p.Value is bool))
			{
				if (exception.ResponseContentPreview != null)
				{
					yield return ($"response.{exception.ResponseStatusCode}", exception.ResponseContentPreview);
				}
				else if (exception.Response?.Content != null)
				{
					string content;
					try
					{
						content = exception.Response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
					}
					catch (Exception ex)
					{
						content = ex.ToString();
					}

					yield return ($"response.{exception.ResponseStatusCode}", content);
				}
			}
		}

		private static bool IsDefault(object obj)
		{
			if (obj == null)
				return true;

			var type = obj.GetType();
			return type.IsValueType && Activator.CreateInstance(type).Equals(obj);
		}

		private sealed class MemberReplacingExpressionVisitor : ExpressionVisitor
		{
			public MemberReplacingExpressionVisitor(ParameterExpression parameterExpression, string parameterName)
			{
				m_newParameters = new Dictionary<MemberInfo, ParameterExpression>();
				m_parameterExpression = parameterExpression;
				m_parameterName = parameterName;
			}

			protected override Expression VisitMember(MemberExpression me)
			{
				if (me.Expression != m_parameterExpression)
					return base.VisitMember(me);

				if (m_newParameters.TryGetValue(me.Member, out var pe))
					return pe;

				return m_newParameters[me.Member] = Expression.Parameter(me.Type, m_parameterName);
			}

			public bool TryGetSingleClassParameter(out MemberInfo mi, out ParameterExpression pe)
			{
				mi = m_newParameters.Keys.FirstOrDefault();
				pe = m_newParameters.Values.FirstOrDefault();
				return m_newParameters.Count == 1 && pe.Type.IsClass;
			}

			private readonly Dictionary<MemberInfo, ParameterExpression> m_newParameters;

			private readonly ParameterExpression m_parameterExpression;
			private readonly string m_parameterName;
		}
	}
}
