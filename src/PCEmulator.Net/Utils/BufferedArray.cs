namespace PCEmulator.Net.Utils
{
	public class BufferedArray<T>
	{
		protected byte[] buffer;
		private int offset;
		private uint size;

		public BufferedArray(byte[] buffer, int offset, uint size)
		{
			this.buffer = buffer;
			this.offset = offset;
			this.size = size;
		}
	}
}