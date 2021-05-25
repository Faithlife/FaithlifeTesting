using System;
using System.Threading.Tasks;
using Faithlife.WebRequests.Json;

namespace Faithlife.Testing
{
	/// <summary>
	/// Entry point for retrying web-requests.
	/// </summary>
	public static class AssertExResponse
	{
		/// <summary>
		/// Retries <paramref name="getResponse"/> until all assertions chained after this method pass.
		/// </summary>
		public static WaitUntilAssertable<TResponse> WaitUntil<TResponse>(Func<TResponse> getResponse)
			where TResponse : AutoWebServiceResponse
			=> AssertEx.WaitUntil(getResponse).AssertResponse();

		/// <summary>
		/// Retries <paramref name="getResponseAsync"/> until all assertions chained after this method pass.
		/// </summary>
		public static WaitUntilAssertable<TResponse> WaitUntil<TResponse>(Func<Task<TResponse>> getResponseAsync)
			where TResponse : AutoWebServiceResponse
			=> AssertEx.WaitUntil(getResponseAsync).AssertResponse();
	}
}
