using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Faithlife.Testing.RabbitMq;
using NUnit.Framework;

namespace Faithlife.Testing.Tests.RabbitMq
{
	[TestFixture]
	public sealed class AckTrackerTests
	{
		[Test]
		public void TestNothing() => new AckTrackerTracker().AssertCalculateNacks(0, "");

		[TestCase(false, null, 1, "")]
		[TestCase(true, null, 0, "")]
		[TestCase(true, ProcessResult.Acked, 0, "")]
		[TestCase(true, ProcessResult.ShouldNack, 1, "")]
		public void TestOneMessage(bool shouldProcess, ProcessResult? action, int expectedNackMultiple, string expectedNackSingle)
		{
			var tracker = new AckTrackerTracker();

			tracker.Observe(0, shouldProcess);

			if (action.HasValue)
				tracker.EndProcessing(0, action.Value);

			tracker.AssertCalculateNacks(expectedNackMultiple, expectedNackSingle);
		}

		[TestCase(null, 0, "")]
		[TestCase(ProcessResult.AlreadyNacked, 0, "")]
		[TestCase(ProcessResult.Acked, 0, "")]
		[TestCase(ProcessResult.ShouldNack, 1, "")]
		public void TestOneMessageTwoCancels(ProcessResult? action, int expectedNackMultiple, string expectedNackSingle)
		{
			var tracker = new AckTrackerTracker();

			tracker.Observe(0, startProcessing: true);

			tracker.CalculateNacks();

			if (action.HasValue)
				tracker.EndProcessing(0, action.Value);

			tracker.AssertCalculateNacks(expectedNackMultiple, expectedNackSingle);
		}

		[Test]
		public void TestShouldNackWithOutstandingWhenObserved()
		{
			var tracker = new AckTrackerTracker();

			tracker.Observe(0, startProcessing: true);
			tracker.CalculateNacks();

			tracker.Observe(1, startProcessing: true);
			tracker.EndProcessing(1, ProcessResult.ShouldNack);
			tracker.CalculateNacks();

			tracker.CalculateNacks();

			tracker.EndProcessing(0, ProcessResult.Acked);

			tracker.AssertAllAcked();
		}

		[Test]
		public void TestShouldNackWithOutstandingProcessing()
		{
			var tracker = new AckTrackerTracker();

			tracker.Observe(0, startProcessing: true);
			tracker.Observe(1, startProcessing: true);
			tracker.CalculateNacks();

			tracker.EndProcessing(1, ProcessResult.ShouldNack);

			tracker.CalculateNacks();

			tracker.EndProcessing(0, ProcessResult.Acked);

			tracker.AssertAllAcked();
		}

		[Test]
		public void TestMultipleShouldNackAfterCalculate()
		{
			var tracker = new AckTrackerTracker();

			tracker.Observe(0, startProcessing: true);
			tracker.EndProcessing(0, ProcessResult.Acked);
			tracker.CalculateNacks();

			tracker.Observe(1, startProcessing: true);
			tracker.Observe(2, startProcessing: true);
			tracker.CalculateNacks();

			tracker.EndProcessing(2, ProcessResult.ShouldNack);
			tracker.CalculateNacks();

			tracker.EndProcessing(1, ProcessResult.Acked);

			tracker.AssertAllAcked();
		}

		[Test, Explicit("Takes about a minute. Will output a specific test-case reproducing the problem when run to create as a separate test.")]
		public void GenerateFailingTestCases()
		{
			// Generate all possible scenarios with 5 messages and 3 calls to CalculateNacks
			const int messagesCount = 5;

			for (var firstCancelAfter = 0; firstCancelAfter < messagesCount - 2; firstCancelAfter++)
			{
				for (var secondCancelAfter = firstCancelAfter; secondCancelAfter < messagesCount - 1; secondCancelAfter++)
				{
					var startTime = Enumerable.Range(0, messagesCount)
						.Select(
							i => i <= firstCancelAfter ? 0
								: i <= secondCancelAfter ? 1
								: 2)
						.ToArray();
					var possibleProcessTimes = Enumerable.Range(0, messagesCount)
						.Select(
							i =>
							{
								var times = new List<int>();
								if (i <= firstCancelAfter)
									times.Add(0);

								if (i <= secondCancelAfter)
									times.Add(1);

								times.Add(2);
								times.Add(3);

								return times.ToArray();
							})
						.ToArray();

					foreach (var shouldStartProcessing in GetAllPermutations(messagesCount, _ => new[] { true, false }))
					{
						foreach (var processTime in GetAllPermutations(messagesCount, i => shouldStartProcessing[i] ? possibleProcessTimes[i] : new[] { -1 }))
						{
							foreach (var processResult in GetAllPermutations(
								messagesCount,
								i => !shouldStartProcessing[i] ? new[] { (ProcessResult) (-1) }
									: startTime[i] == processTime[i] ? new[] { ProcessResult.Acked, ProcessResult.ShouldNack }
									: processTime[i] == 3 ? new[] { ProcessResult.Acked, ProcessResult.AlreadyNacked }
									: new[] { ProcessResult.AlreadyNacked, ProcessResult.Acked, ProcessResult.ShouldNack }))
							{
								// Run the test and ensure nothing gets double-acked.
								var tracker = new AckTrackerTracker();

								void ProcessRound(int round)
								{
									for (var i = 0; i < messagesCount; i++)
									{
										if (startTime[i] == round)
											tracker.Observe(i, shouldStartProcessing[i]);

										if (processTime[i] == round)
											tracker.EndProcessing(i, processResult[i]);
									}
								}

								ProcessRound(0);

								tracker.CalculateNacks();

								ProcessRound(1);

								tracker.CalculateNacks();

								ProcessRound(2);

								tracker.CalculateNacks();

								ProcessRound(3);

								tracker.AssertAllAcked();
							}
						}
					}
				}
			}
		}

		public enum ProcessResult
		{
			AlreadyNacked,
			Acked,
			ShouldNack,
		}

		private static IEnumerable<T[]> GetAllPermutations<T>(int count, Func<int, T[]> getPossibilities)
		{
			var possibilities = Enumerable.Range(0, count)
				.Select(getPossibilities)
				.ToArray();

			return GetAllPermutations(possibilities.Select(a => a.Length).ToArray())
				.Select(permutation => permutation.Select((p, i) => possibilities[i][p]).ToArray());
		}

		private static IEnumerable<int[]> GetAllPermutations(int[] possibilityCounts)
		{
			var permutationCount = possibilityCounts.Aggregate(1, (a, b) => a * b);

			for (var permutationIndex = 0; permutationIndex < permutationCount; permutationIndex++)
			{
				var permuation = GC.AllocateUninitializedArray<int>(possibilityCounts.Length);
				var remainder = permutationIndex;
				for (var i = 0; i < possibilityCounts.Length; i++)
				{
					permuation[i] = remainder % possibilityCounts[i];
					remainder /= possibilityCounts[i];
				}

				yield return permuation;
			}
		}

		private sealed class AckTrackerTracker
		{
			public void Observe(int index, bool startProcessing)
			{
				m_history.AppendLine($"tracker.Observe({index}, startProcessing: {startProcessing.ToString().ToLowerInvariant()});");

				var deliveryTag = (ulong) index + 1;
				m_tracker.Observe(deliveryTag);

				if (startProcessing)
				{
					m_tracker.StartProcessing(deliveryTag);
				}
			}

			public void EndProcessing(int index, ProcessResult action)
			{
				m_history.AppendLine($"tracker.EndProcessing({index}, ProcessResult.{action});");

				var deliveryTag = (ulong) index + 1;

				if (action == ProcessResult.ShouldNack)
				{
					m_tracker.EndProcessing(deliveryTag, shouldNack: true);
					return;
				}

				if (action == ProcessResult.Acked)
					m_tracker.EndProcessing(deliveryTag, acked: true);

				if (action == ProcessResult.AlreadyNacked)
					m_tracker.EndProcessing(deliveryTag);

				if (!m_observedDeliveryTags.Add(deliveryTag))
					Assert.Fail($"Multiple Nacks for {deliveryTag}\r\n{m_history}");
			}

			public void CalculateNacks()
			{
				var nackSingle = new List<ulong>();
				m_tracker.CalculateNacks(out var nackMultiple, ref nackSingle);

				m_history.AppendLine($"tracker.CalculateNacks(); // nackMultiple: {nackMultiple} nackSingle: {string.Join("", nackSingle)}");

				foreach (var nack in nackSingle)
				{
					if (!m_observedDeliveryTags.Add(nack))
						Assert.Fail($"Multiple Nacks for {nack}\r\n{m_history}");
				}

				if (nackMultiple > 0ul)
				{
					if (!m_observedDeliveryTags.Add(nackMultiple))
						Assert.Fail($"Multiple Nacks for {nackMultiple}\r\n{m_history}");

					for (var dt = 1ul; dt < nackMultiple; dt++)
						m_observedDeliveryTags.Add(dt);
				}
			}

			public void AssertCalculateNacks(int expectedNackMultiple, string expectedNackSingle)
			{
				var actualNackSingle = new List<ulong>();

				m_tracker.CalculateNacks(
					out var actualNackMultiple,
					ref actualNackSingle);

				using (AssertEx.Context("history", m_history.ToString()))
					AssertEx.IsTrue(() => actualNackMultiple == (ulong) expectedNackMultiple);

				// Assert.AreEqual still has better set-equality semantics than AssertEx. :(
				Assert.AreEqual(Cast(expectedNackSingle).ToList(), actualNackSingle, m_history.ToString());

				// Strings to facilitate test-case names.
				static HashSet<ulong> Cast(string source) => (source ?? Enumerable.Empty<char>()).Select(i => (ulong) (i - '1' + 1)).ToHashSet();
			}

			public void AssertAllAcked()
			{
				using (AssertEx.Context(new { history = m_history.ToString(), observed = m_observedDeliveryTags }))
					AssertEx.IsTrue(() => (ulong) m_observedDeliveryTags.Count == m_tracker.LastObservedDeliveryTag);
			}

			private readonly HashSet<ulong> m_observedDeliveryTags = new();
			private readonly AckTracker m_tracker = new();
			private readonly StringBuilder m_history = new();
		}
	}
}
