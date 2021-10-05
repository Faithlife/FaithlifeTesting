using System;
using System.Collections.Generic;
using System.Linq;

namespace Faithlife.Testing.RabbitMq
{
	internal sealed class AckTracker
	{
		public ulong LastObservedDeliveryTag => m_lastObservedDeliveryTag;

		public void Observe(ulong deliveryTag) => m_lastObservedDeliveryTag = deliveryTag;

		public void StartProcessing(ulong deliveryTag) => m_processingDeliveryTags.Add(deliveryTag);

		public void EndProcessing(ulong deliveryTag, ProcessResult result)
		{
			if (!m_processingDeliveryTags.Remove(deliveryTag))
				throw new InvalidOperationException("Must call StartProcessing before calling EndProcessing");

			if (result == ProcessResult.AlreadyAcked)
				m_ackedDeliveryTags.Add(deliveryTag);
			else if (result == ProcessResult.ShouldNack)
				m_shouldNackDeliveryTags.Add(deliveryTag);
		}

		public enum ProcessResult
		{
			AlreadyNacked,
			AlreadyAcked,
			ShouldNack,
		}

		public void CalculateNacks(out ulong nackMultiple, ref List<ulong> nackSingle)
		{
			if (!m_processingDeliveryTags.Any())
			{
				// If no delivery-tags are processing, nack the last non-acked one.
				nackMultiple = m_lastObservedDeliveryTag;
			}
			else
			{
				// Ensure we do not NACK a tag awaiting ACKing by an awaiter when doing our `nackMultiple`.
				// It's OK for there to be previously-ACKed messages *smaller* than this value, though.
				var firstNackSingle = m_processingDeliveryTags.Min();

				// nack all messages until the last non-acked message before the first message still processing.
				nackMultiple = firstNackSingle - 1ul;

				for (firstNackSingle++; firstNackSingle <= m_previouslyNackedThrough; firstNackSingle++)
				{
					if (m_shouldNackDeliveryTags.Contains(firstNackSingle))
						nackSingle.Add(firstNackSingle);
				}

				// NACK all the tags after our `firstNackSingle` RBAR
				for (var tag = firstNackSingle; tag <= m_lastObservedDeliveryTag; tag++)
				{
					if (!m_ackedDeliveryTags.Contains(tag) && !m_processingDeliveryTags.Contains(tag))
						nackSingle.Add(tag);
				}
			}

			// We'll get an error if we NACK something we've already ACKed.
			while (m_ackedDeliveryTags.Contains(nackMultiple))
				nackMultiple -= 1ul;

			// Never `nack` before `previouslyNackedThrough` **unless** it's been put in `shouldNack` since.
			if (nackMultiple > 0ul && nackMultiple <= m_previouslyNackedThrough)
			{
				ulong? maxShouldNack = null;

				foreach (var shouldNack in m_shouldNackDeliveryTags)
				{
					if (shouldNack <= nackMultiple && (maxShouldNack == null || maxShouldNack.Value < shouldNack))
						maxShouldNack = shouldNack;
				}

				nackMultiple = maxShouldNack.GetValueOrDefault(0ul);
			}

			// Ensure we don't nack anything twice when the next subscriber comes along
			m_previouslyNackedThrough = m_lastObservedDeliveryTag;

			// Cleanup tracking sets
			m_shouldNackDeliveryTags.Clear();

			if (!m_processingDeliveryTags.Any())
			{
				m_ackedDeliveryTags.Clear();
			}
			else if (m_ackedDeliveryTags.Any())
			{
				var min = m_processingDeliveryTags.Min();
				m_ackedDeliveryTags.RemoveWhere(dt => dt < min);
			}
		}

		private readonly HashSet<ulong> m_processingDeliveryTags = new();
		private readonly HashSet<ulong> m_ackedDeliveryTags = new();
		private readonly HashSet<ulong> m_shouldNackDeliveryTags = new();

		private ulong m_lastObservedDeliveryTag;
		private ulong m_previouslyNackedThrough;
	}
}
