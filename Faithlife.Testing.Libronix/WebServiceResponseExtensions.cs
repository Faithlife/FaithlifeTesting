using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Faithlife.Reflection;
using Faithlife.Testing.TestFrameworks;
using Faithlife.WebRequests.Json;

namespace Faithlife.Testing.WebRequests
{
	public static class WebServiceResponseExtensions
	{
		public static async Task<Assertable<TResponse>> AssertResponseIs<TResponse>(this Task<TResponse> webServiceResponse, Expression<Func<TResponse, bool>> predicateExpression)
			where TResponse : AutoWebServiceResponse
			=> (await webServiceResponse).AssertResponseIs(predicateExpression);
		
		public static WaitUntilAssertable<TResponse> AssertResponseIs<TResponse>(this WaitUntilAssertable<TResponse> webServiceResponse, Expression<Func<TResponse, bool>> predicateExpression)
			where TResponse : AutoWebServiceResponse
		{
			if (webServiceResponse == null)
				throw new ArgumentNullException(nameof(webServiceResponse));

			return webServiceResponse.Apply(a => a.Value.AssertResponseIs(predicateExpression));
		}

		public static Assertable<TResponse> AssertResponseIs<TResponse>(this TResponse response, Expression<Func<TResponse, bool>> predicateExpression)
			where TResponse : AutoWebServiceResponse
		{
			if (response == null)
				throw new ArgumentNullException(nameof(response));
			if (predicateExpression == null)
				throw new ArgumentNullException(nameof(predicateExpression));

			return AssertEx.HasValue(() => response)
				.Context(GetContext(response))
				.IsTrue(predicateExpression);
		}

		public static async Task<Assertable<TStatus>> AssertResponseHas<TResponse, TStatus>(this Task<TResponse> webServiceResponse, Expression<Func<TResponse, TStatus>> getStatusProperty)
			where TResponse : AutoWebServiceResponse
			where TStatus : class
			=> (await webServiceResponse).AssertResponseHas(getStatusProperty);

		public static WaitUntilAssertable<TStatus> AssertResponseHas<TResponse, TStatus>(this WaitUntilAssertable<TResponse> webServiceResponse, Expression<Func<TResponse, TStatus>> getStatusProperty)
			where TResponse : AutoWebServiceResponse
			where TStatus : class
		{
			if (webServiceResponse == null)
				throw new ArgumentNullException(nameof(webServiceResponse));

			return webServiceResponse.Apply(a => a.Value.AssertResponseHas(getStatusProperty));
		}

		public static Assertable<TStatus> AssertResponseHas<TResponse, TStatus>(this TResponse webServiceResponse, Expression<Func<TResponse, TStatus>> mapExpression)
			where TResponse : AutoWebServiceResponse
			where TStatus : class
		{
			if (webServiceResponse == null)
				throw new ArgumentNullException(nameof(webServiceResponse));
			if (mapExpression == null)
				throw new ArgumentNullException(nameof(mapExpression));

			var context = GetContext(webServiceResponse).ToList();

			if (TryGetAssertableRequest(webServiceResponse, mapExpression, context, GenericParameterAttributes.ReferenceTypeConstraint, out var builder))
				return (Assertable<TStatus>) builder;

			return AssertResponse(webServiceResponse, context).HasValue(mapExpression);
		}

		public static async Task<TStatus> AssertResponseHas<TResponse, TStatus>(this Task<TResponse> webServiceResponse, Expression<Func<TResponse, TStatus?>> getStatusProperty)
			where TResponse : AutoWebServiceResponse
			where TStatus : struct
			=> (await webServiceResponse).AssertResponseHas(getStatusProperty);

		public static async Task<TStatus> AssertResponseHas<TResponse, TStatus>(this WaitUntilAssertable<TResponse> webServiceResponse, Expression<Func<TResponse, TStatus?>> getStatusProperty)
			where TResponse : AutoWebServiceResponse
			where TStatus : struct
		{
			if (webServiceResponse == null)
				throw new ArgumentNullException(nameof(webServiceResponse));

			return await webServiceResponse
				.Apply(a => AssertEx.HasValue(a.Value).Context(GetContext(a.Value)))
				.HasValue(getStatusProperty);
		}

		public static TStatus AssertResponseHas<TResponse, TStatus>(this TResponse webServiceResponse, Expression<Func<TResponse, TStatus?>> mapExpression)
			where TResponse : AutoWebServiceResponse
			where TStatus : struct
		{
			if (webServiceResponse == null)
				throw new ArgumentNullException(nameof(webServiceResponse));
			if (mapExpression == null)
				throw new ArgumentNullException(nameof(mapExpression));

			var context = GetContext(webServiceResponse).ToList();

			if (TryGetAssertableRequest(webServiceResponse, mapExpression, context, GenericParameterAttributes.NotNullableValueTypeConstraint, out var builder))
				return (TStatus) builder;

			return AssertResponse(webServiceResponse, context).HasValue(mapExpression);
		}

		private static bool TryGetAssertableRequest<TResponse, TStatus>(
			TResponse webServiceResponse,
			Expression<Func<TResponse, TStatus>> mapExpression,
			List<(string Name, object Value)> context,
			GenericParameterAttributes genericParameterAttributes,
			out object builder)
			where TResponse : AutoWebServiceResponse
		{
			builder = null;

			if (mapExpression.Body is MemberExpression)
				return false;

			var visitor = new MemberReplacingExpressionVisitor(mapExpression.Parameters.Single());
			var replacedBody = visitor.Visit(mapExpression.Body);

			if (!visitor.TryGetSingleClassParameter(out var mi, out var pe))
				return false;

			var property = DtoInfo.GetInfo(typeof(TResponse)).TryGetProperty(mi.Name);
			if (property == null)
				return false;

			var value = property.GetValue(webServiceResponse);
			if (IsDefault(value))
			{
				AssertResponse(webServiceResponse, context)
					.IsTrue(Expression.Lambda<Func<TResponse, bool>>(Expression.NotEqual(Expression.MakeMemberAccess(pe, mi), Expression.Constant(null)), pe));
				return false;
			}

			var replacedLambda = Expression.Lambda(replacedBody, pe);

			var select = typeof(Assertable<>).MakeGenericType(pe.Type)
				.GetMethods(BindingFlags.Public | BindingFlags.Instance)
				.Single(selectMethod => selectMethod.Name == nameof(Assertable<object>.HasValue) && selectMethod.GetGenericArguments().Single().GenericParameterAttributes.HasFlag(genericParameterAttributes))
				.MakeGenericMethod(typeof(TStatus));

			try
			{
				var valueBuilder = s_response.MakeGenericMethod(pe.Type).Invoke(null, new[] { value, context });
				builder = select.Invoke(valueBuilder, new object[] { replacedLambda });
			}
			catch (TargetInvocationException tie)
			{
				ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
			}

			return true;
		}

		private static Assertable<T> AssertResponse<T>(T response, IEnumerable<(string Name, object Value)> context)
			where T : class
		{
			return AssertEx.HasValue(() => response).Context(context);
		}

		private static IEnumerable<(string Name, object Value)> GetContext<TResponse>(TResponse response)
			where TResponse : AutoWebServiceResponse
		{
			var exception = response.CreateException();
			var statusCodeString = exception.ResponseStatusCode?.ToString();

			yield return ("request", $"{exception.RequestMethod} {exception.RequestUri.AbsoluteUri} (status {exception.ResponseStatusCode})");

			var contextByIsContent = DtoInfo.GetInfo(typeof(TResponse))
				.Properties
				.Select(p => (p.Name, Value: p.GetValue(response)))
				.Where(p => !IsDefault(p.Value))
				.ToLookup(p => p.Name == statusCodeString);

			foreach (var (name, value) in contextByIsContent[false])
				yield return ("response." + name, value);

			if (contextByIsContent[true].All(p => p.Value is bool))
			{
				if (exception.ResponseContentPreview != null)
				{
					yield return ("response.Content", exception.ResponseContentPreview);
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

					yield return ("response.Content", content);
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
			public MemberReplacingExpressionVisitor(ParameterExpression parameterExpression)
			{
				m_newParameters = new Dictionary<MemberInfo, ParameterExpression>();
				m_parameterExpression = parameterExpression;
			}

			protected override Expression VisitMember(MemberExpression me)
			{
				if (me.Expression != m_parameterExpression)
					return base.VisitMember(me);

				if (m_newParameters.TryGetValue(me.Member, out var pe))
					return pe;

				return m_newParameters[me.Member] = Expression.Parameter(me.Type, m_parameterExpression.Name);
			}

			public bool TryGetSingleClassParameter(out MemberInfo mi, out ParameterExpression pe)
			{
				mi = m_newParameters.Keys.FirstOrDefault();
				pe = m_newParameters.Values.FirstOrDefault();
				return m_newParameters.Count == 1 && pe.Type.IsClass;
			}

			private readonly Dictionary<MemberInfo, ParameterExpression> m_newParameters;

			private readonly ParameterExpression m_parameterExpression;
		}

		private static readonly MethodInfo s_response = typeof(WebServiceResponseExtensions).GetMethod(nameof(AssertResponse), BindingFlags.NonPublic | BindingFlags.Static);
	}
}
