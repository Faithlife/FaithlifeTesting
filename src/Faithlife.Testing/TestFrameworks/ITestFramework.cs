using System;

namespace Faithlife.Testing.TestFrameworks
{
	internal interface ITestFramework
	{
		bool IsAvailable { get; }
		void Fail(string message);
		IDisposable GetIsolatedContext();
	}
}
