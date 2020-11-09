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

#pragma warning disable CA1000 // Do not declare static members on generic types
		public static ImmutableStack<T> Empty { get; } = new ImmutableStack<T>();
#pragma warning restore CA1000 // Do not declare static members on generic types

		public bool IsEmpty => m_tail == null;

		public ImmutableStack<T> Push(T value) => new ImmutableStack<T>(value, this);

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
