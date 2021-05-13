using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Faithlife.Testing.Tests.UnitTests
{
	[TestFixture]
	public sealed class WaitUntilTests
	{
		[Test]
		public async Task TestSuccessFirstTry()
		{
			var fooService = new FooService();
			await AssertEx.WaitUntil(() => fooService.GetFooAsync())
				.IsTrue(f => f.Bar == "bar" && f.TryCount == 1);
		}

		[Test]
		public async Task TestSuccessSecondTry()
		{
			var fooService = new FooService();
			await AssertEx.WaitUntil(() => fooService.GetFooAsync())
				.IsTrue(f => f.Bar == "bar" && f.TryCount == 2);
		}

		[Test]
		public async Task TestSuccessSecondTrySmallTimeout()
		{
			var fooService = new FooService();
			await AssertEx.WaitUntil(() => fooService.GetFooAsync())
				.WithTimeout(TimeSpan.FromMilliseconds(1))
				.IsTrue(f => f.Bar == "bar" && f.TryCount == 2);
		}

		[Test, ExpectedMessage(@"Expected:
	value.TryCount == 3

Actual:
	value.TryCount = 2

Context:
	timeoutSeconds = 0.001
	totalRetries = 2")]
		public async Task TestFailThirdTrySmallTimeout()
		{
			var fooService = new FooService();
			await AssertEx.WaitUntil(() => fooService.GetFooAsync())
				.WithTimeout(TimeSpan.FromMilliseconds(1))
				.IsTrue(f => f.Bar == "bar" && f.TryCount == 3);
		}

		private sealed class FooService
		{
			public Task<FooDto> GetFooAsync()
			{
				m_tryCount++;
				return Task.FromResult(new FooDto
				{
					Bar = "bar",
					TryCount = m_tryCount,
				});
			}

			private int m_tryCount;
		}

		private sealed class FooDto
		{
			public string Bar { get; set; }
			public int TryCount { get; set; }
		}
	}
}
