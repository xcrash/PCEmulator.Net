using System.Collections.Generic;

namespace PCEmulator.Net.Utils
{
	public class Buffer<T> : Queue<T>
	{
		private int? maxCapacity { get; set; }

		public Buffer() { maxCapacity = null; }
		public Buffer(int capacity) { maxCapacity = capacity; }

		public void Add(T newElement)
		{
			if (Count == (maxCapacity ?? -1)) Dequeue();
			Enqueue(newElement);
		}
	}
}