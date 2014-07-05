using System;
using PCEmulator.Net.Operands.Args;

namespace PCEmulator.Net.Operands
{
	internal class EbOperand : Operand<byte>
	{
		public EbOperand(CPU_X86_Impl.Executor e)
			: base(new BArgument(e), e)
		{
		}

		public int regIdx { get { return CPU_X86_Impl.Executor.regIdx1(e.mem8); } }

		public uint readY()
		{
			return (e.regs[regIdx & 3] >> ((regIdx & 4) << 1));
		}

		public override byte PopValue()
		{
			throw new NotImplementedException();
		}
	}
}