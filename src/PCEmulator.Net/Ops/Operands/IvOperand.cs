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

		public uint readX()
		{
			return ops.readX();
		}

		public override uint PopValue()
		{
			throw new NotImplementedException();
		}

		public override void PushValue(uint _x)
		{
			x = _x;

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