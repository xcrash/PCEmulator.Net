using System;
using PCEmulator.Net.Operands.Args;

namespace PCEmulator.Net.Operands
{
	public class IvOperand : Operand<uint>
	{
		public IvOperand(CPU_X86_Impl.Executor e)
			: base(new VArgument(e), e)
		{
		}

		public override uint PopValue()
		{
			throw new NotImplementedException();
		}
	}
}