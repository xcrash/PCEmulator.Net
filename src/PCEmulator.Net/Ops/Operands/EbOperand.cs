using System;
using PCEmulator.Net.Operands.Args;

namespace PCEmulator.Net.Operands
{
	internal class EbOperand : Operand<byte>
	{
		private readonly CPU_X86_Impl.Executor e;

		public EbOperand(CPU_X86_Impl.Executor e)
			: base(new BArgument(e), e)
		{
			this.e = e;
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

		public override void PushValue(byte x)
		{
			throw new NotImplementedException();
		}
	}
}