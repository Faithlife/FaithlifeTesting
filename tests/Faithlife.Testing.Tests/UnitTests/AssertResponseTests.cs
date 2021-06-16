using System.Net;
using System.Net.Http;
using Faithlife.WebRequests;
using Faithlife.WebRequests.Json;
using NUnit.Framework;

namespace Faithlife.Testing.Tests.UnitTests
{
	[TestFixture]
	public sealed class AssertResponseTests
	{
		[Test]
		public void TestSuccessOK()
		{
			FooResponse.CreateOK()
				.AssertResponse()
				.HasValue(r => r.OK);
		}

		[Test]
		public void TestSuccessBadRequest()
		{
			FooResponse.CreateUnauthorized()
				.AssertResponse()
				.IsTrue(r => r.Unauthorized);
		}

		[Test, ExpectedMessage(@"Expected:
	response.OK != null

Actual:
	response.OK = null
	response = { ""badRequest"": { ""errorCode"": 1 }, ""unauthorized"": false }

Context:
	request = ""GET http://example.com/ (status BadRequest)""")]
		public void TestWrongStatus()
		{
			FooResponse.CreateBadRequest()
				.AssertResponse()
				.HasValue(r => r.OK);
		}

		[Test, ExpectedMessage(@"Expected:
	response.OK != null

Actual:
	response.OK = null
	response = { ""unauthorized"": true, ""wwwAuthenticate"": ""Testing realm=\""Narnia\"""" }

Context:
	response.Unauthorized = ""{ errorCode: 2 }""
	response.WWWAuthenticate = ""Testing realm=""Narnia""""
	request = ""GET http://example.com/ (status Unauthorized)""")]
		public void TestWrongStatusBooleanProperty()
		{
			FooResponse.CreateUnauthorized()
				.AssertResponse()
				.HasValue(r => r.OK);
		}

		[Test, ExpectedMessage(@"Expected:
	response.Bar == ""wrong""

Actual:
	response.Bar = ""baz""

Context:
	request = ""GET http://example.com/ (status OK)""")]
		public void TestWrongContentOneStatement()
		{
			FooResponse.CreateOK()
				.AssertResponse()
				.HasValue(r => r.OK)
				.IsTrue(r => r.Bar == "wrong" && r.Id == 1);
		}

		[Test, ExpectedMessage(@"Expected:
	response.Bar == ""wrong""

Actual:
	response.Bar = ""baz""

Context:
	request = ""GET http://example.com/ (status OK)""")]
		public void TestWrongContentTwoStatement()
		{
			FooResponse.CreateOK()
				.AssertResponse()
				.IsTrue(r => r.OK.Bar == "wrong" && r.OK.Id == 1);
		}

		[Test, ExpectedMessage(@"Expected:
	response.OK.Id == 3

Actual:
	response.OK.Id = 1

Context:
	request = ""GET http://example.com/ (status OK)""")]
		public void TestStatusInBooleanLogic()
		{
			// This **could** be nicer without the `OK.`
			FooResponse.CreateOK()
				.AssertResponse()
				.IsTrue(r => r.WWWAuthenticate == null && r.OK.Id == 3);
		}

		[Test, ExpectedMessage(@"Expected:
	response.Id == 5

Actual:
	response.Id = 1

Context:
	request = ""GET http://example.com/ (status OK)""")]
		public void TestSeparateHeaderCheck()
		{
			FooResponse.CreateOK()
				.AssertResponse()
				.IsTrue(r => r.WWWAuthenticate == null)
				.IsTrue(r => r.OK.Id == 5);
		}

		[Test, ExpectedMessage(@"Expected:
	response.Bar == ""wrong""

Actual:
	response.Bar = ""baz""

Context:
	request = ""GET http://example.com/ (status OK)""")]
		public void TestComplicatedIsOK()
		{
			FooResponse.CreateOK()
				.AssertResponse()
				.IsTrue(r => r.OK.Bar == "wrong");
		}

		[Test, ExpectedMessage(@"Expected:
	response.OK.Bar == ""wrong""

Actual:
	response.OK = null
	response = { ""badRequest"": { ""errorCode"": 1 }, ""unauthorized"": false }

Context:
	request = ""GET http://example.com/ (status BadRequest)""

System.NullReferenceException: Object reference not set to an instance of an object.", expectStackTrace: true)]
		public void TestComplicatedIsBadRequest()
		{
			FooResponse.CreateBadRequest()
				.AssertResponse()
				.IsTrue(r => r.OK.Bar == "wrong");
		}

		private sealed class FooResponse : AutoWebServiceResponse
		{
			public static FooResponse CreateOK() => new(HttpStatusCode.OK, "{ id: 1, bar:\"baz\" }") { OK = new FooDto { Id = 1, Bar = "baz" } };
			public static FooResponse CreateBadRequest() => new(HttpStatusCode.BadRequest, "{ errorCode: 1 }") { BadRequest = new ErrorDto { ErrorCode = 1 } };
			public static FooResponse CreateUnauthorized() => new(HttpStatusCode.Unauthorized, "{ errorCode: 2 }")
			{
				Unauthorized = true,
				WWWAuthenticate = "Testing realm=\"Narnia\"",
			};

			private FooResponse(HttpStatusCode status, string body)
			{
				OnResponseHandledCoreAsync(new WebServiceResponseHandlerInfo<FooResponse>(
					new HttpResponseMessage
					{
						StatusCode = status,
						Content = new StringContent(body),
						RequestMessage = new HttpRequestMessage(HttpMethod.Get, "http://example.com"),
					},
					default))
					.GetAwaiter().GetResult();
			}

			public FooDto OK { get; private init; }
			public ErrorDto BadRequest { get; private init; }
			public bool Unauthorized { get; private init; }
			public string WWWAuthenticate { get; set; }
		}

		private sealed class FooDto
		{
			public int Id { get; set; }
			public string Bar { get; set; }
		}

		private sealed class ErrorDto
		{
			public int ErrorCode { get; set; }
		}
	}
}
