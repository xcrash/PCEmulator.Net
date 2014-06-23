using PCEmulator.Net.Operands.Args;

namespace PCEmulator.Net.Operands
{
	public class RegsOperand : Operand<uint>
	{
		public RegsOperand(CPU_X86_Impl.Executor e)
			: base(new RegsSpecialArgument(e), e)
		{
		}

		public override uint PopValue()
		{
			if (FS_usage_flag)
			{
				mem8_loc = regs[4];
				x = ((((last_tlb_val = _tlb_read_[mem8_loc >> 12]) | mem8_loc) & 3) != 0
					? e.__ld_32bits_mem8_read()
					: (uint)phys_mem32[(mem8_loc ^ last_tlb_val) >> 2]);
				regs[4] = (mem8_loc + 4) >> 0;
			}
			else
			{
				x = e.pop_dword_from_stack_read();
				e.pop_dword_from_stack_incr_ptr();
			}
			return x;
		}
	}
}