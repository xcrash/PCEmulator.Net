using System;
using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public partial class CPU_X86_Impl
	{
		public partial class Executor
		{
			private void Mix(EbOperand eb, GbOperand gb)
			{
				if (OPbyte == 0x00)
				{
					Add(eb, gb);
					return;
				}

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

			private void Add(EbOperand eb, GbOperand gb)
			{
				mem8 = eb.readX();

				conditional_var = 0;
				reg_idx1 = eb.regIdx;
				y = eb.readY();

				if (isRegisterAddressingMode)
				{
					reg_idx0 = gb.regIdx;
					x = Add(regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1), y);
					gb.set_word_in_register(x);
				}
				else
				{
					segment_translation();
					x = ld_8bits_mem8_write();
					x = Add(x, y);
					eb.setX = (byte)x;
				}
			}

			private uint Add(uint yb, uint y)
			{
				u_src = y;
				yb = (((yb + y) << 24) >> 24);
				u_dst = yb;
				_op = 0;
				return yb;
			}
		}
	}
}