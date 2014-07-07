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

		public override uint ReadOpValue0()
		{
			throw new NotImplementedException();
		}

		public override uint ReadOpValue1()
		{
			throw new NotImplementedException();
		}

		public override void ProceedResult(uint r)
		{
			throw new NotImplementedException();
		}
	}
}