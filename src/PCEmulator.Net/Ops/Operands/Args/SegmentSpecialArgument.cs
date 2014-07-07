namespace PCEmulator.Net.Operands.Args
{
	public class SegmentSpecialArgument : OpContext, ISpecialArgumentCodes<uint>
	{
		public SegmentSpecialArgument(CPU_X86_Impl.Executor e) : base(e)
		{
		}

		private uint segIdx
		{
			get { return e.OPbyte >> 3; }
		}

		public uint readX()
		{
			return (uint)e.cpu.segs[segIdx].selector;
		}

		public uint setX
		{
			set
			{
				var x = value;
				e.set_segment_register((int)(segIdx), (int) x);
				e.pop_dword_from_stack_incr_ptr();
			}
		}
	}
}