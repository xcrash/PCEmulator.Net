using System;
using PCEmulator.Net.Operands.Args;

namespace PCEmulator.Net.Operands
{
	/// <summary>
	/// G         - The reg field of the ModR/M byte selects a general register
	/// b         - Byte, regardless of operand-size attribute.
	/// </summary>
	public class GbOperand : Operand<byte>
	{
		public GbOperand(CPU_X86_Impl.Executor e)
			: base(new BArgument(e), e)
		{
		}

		public override byte PopValue()
		{
			throw new NotImplementedException();
		}

		public override uint ReadOpValue0()
		{
			e.mem8 = e.phys_mem8[e.physmem8_ptr++];
			e.conditional_var = (int) (e.OPbyte >> 3);
			e.reg_idx1 = CPU_X86_Impl.Executor.regIdx1(e.mem8);
			var o0 = (e.regs[e.reg_idx1 & 3] >> ((e.reg_idx1 & 4) << 1));
			return o0;
		}

		public override uint ReadOpValue1()
		{
			y = (regs[e.reg_idx1 & 3] >> ((e.reg_idx1 & 4) << 1));
			return y;
		}

		public override void ProceedResult(uint r)
		{
			e.set_word_in_register(e.reg_idx1, r);
		}
	}
}