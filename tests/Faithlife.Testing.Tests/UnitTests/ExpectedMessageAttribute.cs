using System;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Commands;

namespace Faithlife.Testing.Tests.UnitTests
{
	/// <summary>
	/// Specifies that a test method should be rerun on failure up to the specified maximum number of times.
	/// Copied from https://github.com/nunit/nunit/blob/master/src/NUnitFramework/framework/Attributes/RetryAttribute.cs
	/// `RetryAttribute` retries on **assertion failures**; this attribute retries on **thrown exceptions**, such as those thrown by browser automation.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
	public sealed class ExpectedMessageAttribute : NUnitAttribute, IWrapSetUpTearDown
	{
		public ExpectedMessageAttribute(string expectedMessage, bool expectStackTrace = false)
		{
			m_expectStackTrace = expectStackTrace;
			m_expectedMessage = Normalize(expectedMessage);
		}

		public static void AssertAreMostlyEqual(string expected, string actual, string message = null)
			=> Assert.AreEqual(Normalize(expected), Normalize(actual), message);

		public TestCommand Wrap(TestCommand command) => new OnFailureCommand(command, AssertMessageIsExpected);

		public void AssertMessageIsExpected(string message)
		{
			message = Normalize(message);

			if (m_expectStackTrace && m_expectedMessage.Length <= message.Length)
			{
				Assert.AreEqual(m_expectedMessage, message[..m_expectedMessage.Length], message);
				var stackTrace = message[m_expectedMessage.Length..];

				Assert.IsNotEmpty(stackTrace, "Expected stack trace, got: " + message);

				foreach (var line in stackTrace.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0))
					Assert.AreEqual("at ", line[..3], line);
			}
			else
			{
				Assert.AreEqual(m_expectedMessage, message);
			}
		}

		private static string Normalize(string value) => value?.Replace("\r\n", "\n", StringComparison.InvariantCultureIgnoreCase);

		private sealed class OnFailureCommand : DelegatingTestCommand
		{
			public OnFailureCommand(TestCommand innerCommand, Action<string> onAssertionFailure)
				: base(innerCommand)
			{
				m_onAssertionFailure = onAssertionFailure;
			}

			public override TestResult Execute(TestExecutionContext context)
			{
				Exception actual;
				try
				{
					context.CurrentResult = innerCommand.Execute(context);
					context.CurrentResult.SetResult(ResultState.Failure, "Expected AssertionException, instead got nothing.");
					return context.CurrentResult;
				}
				catch (NUnitException ex) when (ex.InnerException is AssertionException ae)
				{
					actual = ae;
				}
				catch (NUnitException ex) when (ex.InnerException is MultipleAssertException mae)
				{
					actual = mae;
				}
				catch (Exception e) when (e is AssertionException or MultipleAssertException)
				{
					actual = e;
				}

				try
				{
					m_onAssertionFailure(actual.Message);
					context.CurrentResult = context.CurrentTest.MakeTestResult();
					context.CurrentResult.SetResult(ResultState.Success);
				}
				catch (AssertionException ex)
				{
					context.CurrentResult.RecordAssertion(AssertionStatus.Failed, ex.Message);
				}

				return context.CurrentResult;
			}

			private readonly Action<string> m_onAssertionFailure;
		}

		private readonly string m_expectedMessage;
		private readonly bool m_expectStackTrace;
	}
}
