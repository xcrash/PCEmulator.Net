namespace PCEmulator.Net.Utils
{
	public abstract class BufferedArray<T>
	{
		protected byte[] buffer;
		protected int offset;
		private uint size;

		public BufferedArray(byte[] buffer, int offset, uint size)
		{
			this.buffer = buffer;
			this.offset = offset;
			this.size = size;
		}

		public abstract T this[uint mem8Loc] { get; set; }
		public abstract T this[long mem8Loc] { get; set; }
	}
}