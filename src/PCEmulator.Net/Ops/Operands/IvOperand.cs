using System;
using PCEmulator.Net.Operands.Args;

namespace PCEmulator.Net.Operands
{
	internal class IvOperand : Operand<uint>
	{
		private readonly CPU_X86_Impl.Executor e;

		public IvOperand(CPU_X86_Impl.Executor e)
			: base(new VArgument(e), e)
		{
			this.e = e;
		}

		public override uint PopValue()
		{
			throw new NotImplementedException();
		}
	}
}