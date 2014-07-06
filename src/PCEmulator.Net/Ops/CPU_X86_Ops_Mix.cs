using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public partial class CPU_X86_Impl
	{
		public partial class Executor
		{
			private void Mix(EbOperand eb, GbOperand gb)
			{
				mem8 = eb.readX();
				conditional_var = (int)(OPbyte >> 3);
				reg_idx1 = regIdx1(mem8);
				y = (regs[reg_idx1 & 3] >> ((reg_idx1 & 4) << 1));
				if (isRegisterAddressingMode)
				{
					reg_idx0 = regIdx0(mem8);
					set_word_in_register(reg_idx0, do_8bit_math(conditional_var, (regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1)), y));
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

			internal void setX(EbOperand eb, GbOperand gb, uint yb)
			{
				if (isRegisterAddressingMode)
				{
					gb.set_word_in_register(yb);
				}
				else
				{
					eb.setX = (byte) yb;
				}
			}

			internal uint readX(GbOperand gb)
			{
				if (isRegisterAddressingMode)
				{
					reg_idx0 = gb.regIdx;
					return regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1);
				}
				segment_translation();
				return ld_8bits_mem8_write();
			}

			internal uint readY(EbOperand eb)
			{
				mem8 = eb.readX();
				conditional_var = (int)(OPbyte >> 3);
				reg_idx1 = eb.regIdx;
				var readY = eb.readY();
				return readY;
			}
		}
	}
}