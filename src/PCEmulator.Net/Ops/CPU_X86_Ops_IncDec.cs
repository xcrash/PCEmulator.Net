namespace PCEmulator.Net
{
	public partial class CPU_X86_Impl
	{
		public partial class Executor
		{
			private class RegsOpContext : ISpecialArgumentCodes<uint>
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
						e.regs[reg_idx1] = value;
					}
				}

				public void Push(uint x)
				{
					if (e.FS_usage_flag)
					{
						e.mem8_loc = (e.regs[4] - 4) >> 0;
						e.last_tlb_val = e._tlb_write_[e.mem8_loc >> 12];
						if (((e.last_tlb_val | e.mem8_loc) & 3) != 0)
							e.__st32_mem8_write(x);
						else
							e.phys_mem32[(e.mem8_loc ^ e.last_tlb_val) >> 2] = (int)x;

						e.regs[4] = e.mem8_loc;
					}
					else
					{
						e.push_dword_to_stack(x);
					}
				}

				public uint Pop()
				{
					uint x;
					if (e.FS_usage_flag)
					{
						e.mem8_loc = e.regs[4];
						x = ((((e.last_tlb_val = e._tlb_read_[e.mem8_loc >> 12]) | e.mem8_loc) & 3) != 0
							? e.__ld_32bits_mem8_read()
							: (uint)e.phys_mem32[(e.mem8_loc ^ e.last_tlb_val) >> 2]);
						e.regs[4] = (e.mem8_loc + 4) >> 0;
						return x;
					}
					else
					{
						x = e.pop_dword_from_stack_read();
						e.pop_dword_from_stack_incr_ptr();
						return x;
					}
				}

				public uint readX()
				{
					return reg;
				}
			}

			private interface ISpecialArgumentCodes<T> : IArgumentOperand<T>
			{
			}

			private void Inc(RegsOpContext ctx)
			{
				IncDecInit(ctx);
				ctx.reg = u_dst = (ctx.reg + 1) >> 0;
				_op = 27;
			}

			private void Dec(RegsOpContext ctx)
			{
				IncDecInit(ctx);
				ctx.reg = u_dst = (ctx.reg - 1) >> 0;
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