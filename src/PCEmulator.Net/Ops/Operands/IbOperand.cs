using System;
using PCEmulator.Net.Operands.Args;

namespace PCEmulator.Net.Operands
{
	public class IbOperand : Operand<byte>
	{
		public IbOperand(CPU_X86_Impl.Executor e)
			: base(new BArgument(e), e)
		{
		}

		public override byte PopValue()
		{
			throw new NotImplementedException();
		}
	}
}