using System.Text;

namespace Faithlife.Testing
{
	internal static class StringBuilderUtility
	{
		public static void AppendLineLf(this StringBuilder sb, string text)
		{
			sb.Append($"{text}\n");
		}

		public static void AppendLineLf(this StringBuilder sb)
		{
			sb.Append('\n');
		}
	}
}
