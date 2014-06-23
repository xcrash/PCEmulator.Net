using System;
using PCEmulator.Net.Operands.Args;

namespace PCEmulator.Net.Operands
{
	internal class IbOperand : Operand<byte>
	{
		public IbOperand(CPU_X86_Impl.Executor e)
			: base(new BArgument(e), e)
		{
		}

		public override byte PopValue()
		{
			throw new NotImplementedException();
		}

		public override void PushValue(byte _x)
		{
			x = (uint)((_x << 24) >> 24);

			if (FS_usage_flag)
			{
				mem8_loc = (regs[4] - 4) >> 0;
				e.st32_mem8_write(x);
				regs[4] = mem8_loc;
			}
			else
			{
				e.push_dword_to_stack(x);
			}
		}
	}
}