namespace PCEmulator.Net
{
	public partial class CPU_X86_Impl
	{
		public partial class Executor
		{
			private class RegsOpContext
			{
				private readonly Executor e;

				public RegsOpContext(Executor e)
				{
					this.e = e;
				}

				public int reg_idx1
				{
					get { return (int) (e.OPbyte & 7); }
				}

				public uint reg
				{
					get { return e.regs[reg_idx1]; }
					set
					{
						e.regs[reg_idx1] = e.u_dst = value;
					}
				}
			}

			private void Inc(RegsOpContext ctx)
			{
				IncDecInit(ctx);
				ctx.reg = (ctx.reg + 1) >> 0;
				_op = 27;
			}

			private void Dec(RegsOpContext ctx)
			{
				IncDecInit(ctx);
				ctx.reg = (ctx.reg - 1) >> 0;
				_op = 30;
			}

			private void IncDecInit(RegsOpContext ctx)
			{
				reg_idx1 = ctx.reg_idx1;
				if (_op < 25)
				{
					_op2 = _op;
					_dst2 = (int) u_dst;
				}
			}
		}
	}
}