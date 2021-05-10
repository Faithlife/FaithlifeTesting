using System;

namespace Faithlife.Testing
{
	internal sealed class Scope : IDisposable
	{
		public static Scope Create(Action dispose) => new(dispose);

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
