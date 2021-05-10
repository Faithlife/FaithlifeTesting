using System;
using System.Runtime.Serialization;

namespace Faithlife.Testing.TestFrameworks
{
	/// <summary>
	/// Represents the default exception in case no test framework is configured.
	/// </summary>
	[Serializable]

#pragma warning disable CA1032, RCS1194 // AssertionFailedException should never be constructed with an empty message
	public class AssertionFailedException : Exception
#pragma warning restore CA1032, RCS1194
	{
		/// <inheritdoc />
		public AssertionFailedException(string message)
			: base(message)
		{
		}

		/// <inheritdoc />
		protected AssertionFailedException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
		}
	}
}
