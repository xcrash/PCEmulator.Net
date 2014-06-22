namespace PCEmulator.Net
{
	public partial class CPU_X86_Impl
	{
		public partial class Executor
		{
			private JbOpContext Jb;
			private RegsOpContext RegsCtx;

			public Executor()
			{
				Jb = new JbOpContext(this);
				RegsCtx = new RegsOpContext(this);
			}

			public class OpContext
			{
				protected readonly Executor e;

				public OpContext(Executor e)
				{
					this.e = e;
				}

				public bool zeroEquals()
				{
					return e.u_dst == 0;
				}

				public bool check_overflow()
				{
					return e.check_overflow() != 0;
				}
			}

			public class BOpContext : OpContext
			{
				public BOpContext(Executor e)
					: base(e)
				{
				}

				public byte readByteX()
				{
					return e.phys_mem8[e.physmem8_ptr++];
				}

				public uint readUintByteX()
				{
					return (uint)((readByteX() << 24) >> 24);
				}
			}
		}
	}
}