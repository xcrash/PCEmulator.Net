namespace PCEmulator.Net
{
	public partial class CPU_X86_Impl
	{
		public partial class Executor
		{
			internal readonly OperandsHelper Operands;

			public Executor()
			{
				Operands = new OperandsHelper(this);
			}

			private static void ExecOp(Op op)
			{
				op.Exec();
			}


			internal uint ReadOpValue1()
			{
				if (isRegisterAddressingMode)
				{
					reg_idx0 = regIdx0(mem8);
					y = (regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1));
				}
				else
				{
					segment_translation();
					y = ld_8bits_mem8_read();
				}
				var o1 = y;
				return o1;
			}

			internal uint ReadOpValue0()
			{
				mem8 = phys_mem8[physmem8_ptr++];
				conditional_var = (int) (OPbyte >> 3);
				reg_idx1 = regIdx1(mem8);
				var o0 = (regs[reg_idx1 & 3] >> ((reg_idx1 & 4) << 1));
				return o0;
			}

			internal uint Calc(uint o0, uint o1)
			{
				var r = do_8bit_math(conditional_var, o0, o1);
				return r;
			}
		}
	}
}