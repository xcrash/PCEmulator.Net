using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public partial class CPU_X86_Impl
	{
		public partial class Executor
		{
			private void Cmp(EbOperand eb, GbOperand gb)
			{
				mem8 = eb.readX();
				conditional_var = (int)(OPbyte >> 3);
				reg_idx1 = regIdx1(mem8);
				y = (regs[reg_idx1 & 3] >> ((reg_idx1 & 4) << 1));
				if (isRegisterAddressingMode)
				{
					reg_idx0 = regIdx0(mem8);
					var o00 = (regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1));
					var o01 = y;

					var r0 = do_8bit_math(conditional_var, o00, o01);

					set_word_in_register(reg_idx0, r0);
				}
				else
				{
					segment_translation();
					if (conditional_var != 7)
					{
						x = ld_8bits_mem8_write();
						x = do_8bit_math(conditional_var, x, y);
						st8_mem8_write(x);
					}
					else
					{
						x = ld_8bits_mem8_read();
						do_8bit_math(7, x, y);
					}
				}
			}
		}
	}
}