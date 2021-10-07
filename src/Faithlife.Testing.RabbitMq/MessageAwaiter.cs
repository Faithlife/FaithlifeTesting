using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Faithlife.Json;

namespace Faithlife.Testing.RabbitMq
{
	/// <summary>
	/// Waits for a message matching a `predicateExpression`
	/// and records messages observed while waiting.
	/// </summary>
	internal sealed class MessageAwaiter<TMessage>
		where TMessage : class
	{
		public MessageAwaiter(object context, Expression<Func<TMessage, bool>> predicateExpression)
		{
			m_context = context;
			m_predicateExpression = predicateExpression;
			m_predicate = predicateExpression.Compile();
		}

		public TaskCompletionSource<Assertable<TMessage>> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

		public TMessage Message { get; private set; }

		public static IEnumerable<MessageAwaiter<TMessage>> GetMatches(IEnumerable<MessageAwaiter<TMessage>> awaiters, string messageJson)
		{
			TMessage message;

			try
			{
				message = JsonUtility.FromJson<TMessage>(messageJson, s_jsonInputSettings);
			}
			catch
			{
				message = null;
			}

			if (message == null)
			{
				foreach (var potentialAwaiter in awaiters)
					potentialAwaiter.AddMalformed(messageJson);
			}
			else
			{
				foreach (var potentialAwaiter in awaiters)
				{
					if (potentialAwaiter.CheckMessage(message))
						yield return potentialAwaiter;
				}
			}
		}

		private void AddMalformed(string message)
		{
			if (Message != null)
				throw new InvalidOperationException("Message already found.");

			m_messageCount++;

			if (m_messageCount < c_messageLimit)
				m_malformedMessages.Add(message);
		}

		private bool CheckMessage(TMessage message)
		{
			if (Message != null)
				throw new InvalidOperationException("Message already found.");

			m_messageCount++;

			if (m_messageCount <= c_messageLimit)
				m_messages.Add(message);

			bool foundMatch;

			try
			{
				foundMatch = m_predicate(message);
			}
			catch (Exception e)
			{
				// If we added this message to `m_messages`, we'll get the exception again when we do our `assert.HasValue`.
				// Otherwise, log it separately.
				if (m_messageCount > c_messageLimit && m_messageExceptions.Count < c_messageLimit)
					m_messageExceptions.Add(e);

				foundMatch = false;
			}

			if (foundMatch)
				Message = message;

			return foundMatch;
		}

		public void Complete()
		{
			if (Message == null)
				throw new InvalidOperationException("Message not yet found.");

			var message = Message;
			Completion.TrySetResult(AssertEx.HasValue(() => message).Context(m_context));
		}

		public void AssertTimeoutFailure(int timeoutMilliseconds)
		{
			var messages = m_messages;
			var assert = AssertEx.HasValue(() => messages)
				.Context(m_context)
				.Context(new
				{
					messageCount = m_messageCount,
					timeout = HumanReadable(timeoutMilliseconds),
				});

			// Perhaps we missed the message because we could not deserialize it.
			if (m_malformedMessages.Any())
				assert = assert.Context(new { malformedMessages = m_malformedMessages });

			// Perhaps we missed the message because got an exception checking if it matches.
			if (m_messageExceptions.Any())
				assert = assert.Context(new { messageExceptions = m_messageExceptions });

			var param = Expression.Parameter(typeof(IReadOnlyCollection<TMessage>), "m");
			var body = Expression.Call(s_first.MakeGenericMethod(typeof(TMessage)), param, m_predicateExpression);
			assert.HasValue(Expression.Lambda<Func<List<TMessage>, TMessage>>(body, param));
		}

		public static string HumanReadable(int milliseconds)
		{
			const int second = 1000;
			const int minute = second * 60;
			const int hour = minute * 60;

			if (milliseconds == 1)
				return "1 millisecond";
			if (milliseconds < second * 10)
				return $"{milliseconds} milliseconds";
			if (milliseconds < minute * 10)
				return $"{milliseconds / second} seconds";
			if (milliseconds < hour * 10)
				return $"{milliseconds / minute} minutes";

			return $"{milliseconds / hour} hours";
		}

		private const int c_messageLimit = 10;
		private static readonly JsonSettings s_jsonInputSettings = new() { RejectsExtraProperties = false };
		private static readonly MethodInfo s_first = typeof(Enumerable).GetMethods(BindingFlags.Static | BindingFlags.Public).Single(m =>
		{
			var p = m.GetParameters();
			return m.Name == "First"
				&& p.Length == 2
				&& p[0].ParameterType.IsGenericType
				&& p[0].ParameterType.GetGenericTypeDefinition() == typeof(IEnumerable<>)
				&& p[1].ParameterType.IsGenericType
				&& p[1].ParameterType.GetGenericTypeDefinition() == typeof(Func<,>);
		});

		private readonly Func<TMessage, bool> m_predicate;
		private readonly object m_context;
		private readonly Expression<Func<TMessage, bool>> m_predicateExpression;

		private readonly List<TMessage> m_messages = new();
		private readonly List<string> m_malformedMessages = new();
		private readonly List<Exception> m_messageExceptions = new();
		private int m_messageCount;
	}
}
