namespace PCEmulator.Net
{
	public partial class CPU_X86_Impl
	{
		public partial class Executor
		{
			private readonly JbOpContext Jb;
			private readonly RegsOpContext RegsCtx;
			private readonly SegmentsContext SegmentsCtx;
			private readonly IbContext Ib;
			private readonly IvContext Iv;
			private readonly EvContext Ev;

			public Executor()
			{
				Jb = new JbOpContext(this);
				RegsCtx = new RegsOpContext(this);
				SegmentsCtx = new SegmentsContext(this);
				Iv = new IvContext(this);
				Ib = new IbContext(this);
				Ev = new EvContext(this);
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

				public virtual void Push(uint x)
				{
					if (e.FS_usage_flag)
					{
						e.mem8_loc = (e.regs[4] - 4) >> 0;
						e.st32_mem8_write(x);
						e.regs[4] = e.mem8_loc;
					}
					else
					{
						e.push_dword_to_stack(x);
					}
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