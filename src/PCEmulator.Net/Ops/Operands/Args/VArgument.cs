namespace PCEmulator.Net.Operands.Args
{
	public class VArgument : CPU_X86_Impl.Executor.OpContext, IArgumentOperandCodes<uint>
	{
		private readonly CPU_X86_Impl.Executor e;

		public VArgument(CPU_X86_Impl.Executor e) : base(e)
		{
			this.e = e;
		}

		public uint readX()
		{
			return e.phys_mem8_uint();
		}

		public uint setX
		{
			set
			{
				var x = value;
				if (e.isRegisterAddressingMode)
				{
					e.regs[CPU_X86_Impl.Executor.regIdx0(e.mem8)] = x;
				}
				else
				{
					z = (int)e.regs[4];
					e.segment_translation();
					regs[4] = y;
					e.st32_mem8_write(x);
					regs[4] = (uint)z;
				}
			}
		}
	}
}