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
				y = readY(eb);
				x = readX(gb);

				x = Add(x, y);

				setX(eb, gb, x);
			}

			private void setX(EbOperand eb, GbOperand gb, uint yb)
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

			private uint readX(GbOperand gb)
			{
				if (isRegisterAddressingMode)
					reg_idx0 = gb.regIdx;

				if (isRegisterAddressingMode)
				{
					return regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1);
				}
				segment_translation();
				ld_8bits_mem8_write();
				return x;
			}

			private uint readY(EbOperand eb)
			{
				mem8 = eb.readX();

				conditional_var = 0;
				reg_idx1 = eb.regIdx;
				var readY = eb.readY();
				return readY;
			}

			private uint Add(uint x, uint a)
			{
				u_src = a;
				x = (((x + a) << 24) >> 24);
				u_dst = x;
				_op = 0;
				return x;
			}
		}
	}
}