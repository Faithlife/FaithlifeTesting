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

		public static MessageAwaiter<TMessage> FirstMatch(IEnumerable<MessageAwaiter<TMessage>> awaiters, string messageJson)
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
						return potentialAwaiter;
				}
			}

			return null;
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

			if (m_messageCount < c_messageLimit)
				m_messages.Add(message);

			var foundMatch = m_predicate(message);

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

		public void AssertTimeoutFailure()
		{
			var messages = m_messages;
			var assert = AssertEx.HasValue(() => messages)
				.Context(m_context)
				.Context(new
				{
					messageCount = m_messageCount,
				});

			// Perhaps we missed the message because we could not deserialize it.
			if (m_malformedMessages.Any())
				assert = assert.Context(new { malformedMessages = m_malformedMessages });

			var param = Expression.Parameter(typeof(IReadOnlyCollection<TMessage>), "m");
			var body = Expression.Call(s_first.MakeGenericMethod(typeof(TMessage)), param, m_predicateExpression);
			assert.HasValue(Expression.Lambda<Func<List<TMessage>, TMessage>>(body, param));
		}

		private const int c_messageLimit = 10;
		private static readonly JsonSettings s_jsonInputSettings = new() { RejectsExtraProperties = false };
		private static readonly MethodInfo s_first = typeof(Enumerable).GetMethods(BindingFlags.Static | BindingFlags.Public).Single(
			m =>
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
		private int m_messageCount;
	}
}
