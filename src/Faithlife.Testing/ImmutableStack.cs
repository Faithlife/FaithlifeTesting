using System;
using System.Collections;
using System.Collections.Generic;

namespace Faithlife.Testing
{
	internal sealed class ImmutableStack<T> : IEnumerable<T>
	{
		private ImmutableStack(T head, ImmutableStack<T> tail)
		{
			m_head = head;
			m_tail = tail;
		}

		private ImmutableStack()
		{
		}

		public static ImmutableStack<T> Empty { get; } = new();

		public bool IsEmpty => m_tail == null;

		public ImmutableStack<T> Push(T value) => new(value, this);

		IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(this);

		IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

		private sealed class Enumerator : IEnumerator<T>
		{
			public Enumerator(ImmutableStack<T> next)
			{
				m_next = next;
			}

			public bool MoveNext()
			{
				if (m_next.IsEmpty)
					return false;

				Current = m_next.m_head;
				m_next = m_next.m_tail;
				return true;
			}

			public void Reset() => throw new NotSupportedException();

			public T Current { get; private set; }

			object IEnumerator.Current => Current;

			void IDisposable.Dispose()
			{
			}

			private ImmutableStack<T> m_next;
		}

		private readonly T m_head;
		private readonly ImmutableStack<T> m_tail;
	}
}
