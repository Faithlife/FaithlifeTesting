using System.Linq;
using NUnit.Framework;

namespace Faithlife.Testing.Tests.UnitTests
{
	[TestFixture]
	public sealed class ImmutableStackTests
	{
		[Test]
		public void TestEnumerate()
		{
			var stack = ImmutableStack<string>.Empty.Push("foo");

			Assert.AreEqual("foo", stack.Single());
		}
	}
}
