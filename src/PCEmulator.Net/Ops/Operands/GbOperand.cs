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

		public uint ReadOpValue1()
		{
			y = (regs[e.reg_idx1 & 3] >> ((e.reg_idx1 & 4) << 1));
			return y;
		}
	}
}