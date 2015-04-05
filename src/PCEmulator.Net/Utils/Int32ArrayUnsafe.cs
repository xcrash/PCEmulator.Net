using System.Runtime.CompilerServices;

namespace PCEmulator.Net.Utils
{
	public unsafe class Int32ArrayUnsafe : BufferedArray<int>
	{
		private struct IntAccess
		{
			public int Data;
		}

		public Int32ArrayUnsafe(byte[] buffer, int offset, uint size)
			: base(buffer, offset, size)
		{
		}

		public override int this[uint mem8Loc]
		{
			get
			{
				fixed (byte* bufferp = buffer)
				{
					return ((IntAccess*) (bufferp + offset + mem8Loc*4))->Data;
				}
			}
			set
			{
				fixed (byte* bufferp = buffer)
				{
					((IntAccess*) (bufferp + offset + mem8Loc*4))->Data = value;
				}
			}
		}

		public override int this[long mem8Loc]
		{
			get { return this[(uint) mem8Loc]; }
			set { this[(uint) mem8Loc] = value; }
		}
	}
}