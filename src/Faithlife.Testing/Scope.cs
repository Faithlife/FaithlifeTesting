using System;

namespace Faithlife.Testing
{
	internal sealed class Scope : IDisposable
	{
		public static IDisposable NoOp { get; } = new Scope(() => { });
		public static IDisposable Create(Action dispose) => new Scope(dispose);

		private Scope(Action dispose)
		{
			m_dispose = dispose;
		}

		public void Dispose()
		{
			if (m_dispose is not null)
			{
				m_dispose();
				m_dispose = null;
			}
		}

		private Action m_dispose;
	}
}
