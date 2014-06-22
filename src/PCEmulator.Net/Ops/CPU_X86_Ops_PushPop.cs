using System;

namespace PCEmulator.Net
{
	public partial class CPU_X86_Impl
	{
		public partial class Executor
		{
			private void Push(RegsSingleOpContext ctx)
			{
				x = ctx.readX();

				if (FS_usage_flag)
				{
					mem8_loc = (regs[4] - 4) >> 0;
					last_tlb_val = _tlb_write_[mem8_loc >> 12];
					if (((last_tlb_val | mem8_loc) & 3) != 0)
						__st32_mem8_write(x);
					else
						phys_mem32[(mem8_loc ^ last_tlb_val) >> 2] = (int)x;

					regs[4] = mem8_loc;
				}
				else
				{
					push_dword_to_stack(x);
				}
			}

			private void Push(SegmentSingleOpContext ctx)
			{
				var x = ctx.readX();
				push_dword_to_stack(x);
			}

			private void Push(IvContext ctx)
			{
				x = ctx.readX();
				if (FS_usage_flag)
				{
					mem8_loc = (regs[4] - 4) >> 0;
					st32_mem8_write(x);
					regs[4] = mem8_loc;
				}
				else
				{
					push_dword_to_stack(x);
				}
			}

			private void Push(IbContext ctx)
			{
				x = ctx.readX();
				if (FS_usage_flag)
				{
					mem8_loc = (regs[4] - 4) >> 0;
					st32_mem8_write(x);
					regs[4] = mem8_loc;
				}
				else
				{
					push_dword_to_stack(x);
				}
			}

			private void Pop(RegsSingleOpContext ctx)
			{
				if (FS_usage_flag)
				{
					mem8_loc = regs[4];
					x = ((((last_tlb_val = _tlb_read_[mem8_loc >> 12]) | mem8_loc) & 3) != 0
						? __ld_32bits_mem8_read()
						: (uint) phys_mem32[(mem8_loc ^ last_tlb_val) >> 2]);
					regs[4] = (mem8_loc + 4) >> 0;
				}
				else
				{
					x = pop_dword_from_stack_read();
					pop_dword_from_stack_incr_ptr();
				}

				ctx.setX = x;
			}

			private void Pop(SegmentSingleOpContext ctx)
			{
				var x = pop_dword_from_stack_read() & 0xffff;
				ctx.setX = x;
			}

			private void Pop(EvContext ctx)
			{
				x = PopEvValue();
				ctx.setX = x;
			}

			private uint PopEvValue()
			{
				mem8 = phys_mem8[physmem8_ptr++];
				var x = pop_dword_from_stack_read();
				if (isRegisterAddressingMode)
					pop_dword_from_stack_incr_ptr();

				return x;
			}
		}
	}
}