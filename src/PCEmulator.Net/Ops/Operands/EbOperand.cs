using System;
using PCEmulator.Net.Operands.Args;

namespace PCEmulator.Net.Operands
{
	/// <summary>
	/// E         - A ModR/M byte follows the opcode and specifies the operand
	/// b         - Byte, regardless of operand-size attribute.
	/// </summary>
	public class EbOperand : Operand<byte>
	{
		public EbOperand(CPU_X86_Impl.Executor e)
			: base(new BArgument(e), e)
		{
		}

		public override byte PopValue()
		{
			throw new NotImplementedException();
		}

		public override uint ReadOpValue0()
		{
			mem8 = readX();
			e.conditional_var = (int) (e.OPbyte >> 3);
			e.reg_idx1 = CPU_X86_Impl.Executor.regIdx1(e.mem8);

			uint o0;
			if (e.isRegisterAddressingMode)
			{
				e.reg_idx0 = CPU_X86_Impl.Executor.regIdx0(e.mem8);
				o0 = (regs[e.reg_idx0 & 3] >> ((e.reg_idx0 & 4) << 1));
			}
			else
			{
				e.segment_translation();
				if (e.conditional_var != 7)
				{
					x = e.ld_8bits_mem8_write();
				}
				else
				{
					x = e.ld_8bits_mem8_read();
				}
				o0 = x;
			}
			return o0;
		}

		public override uint ReadOpValue1()
		{
			return e.ReadOpValue1();
		}

		public override void ProceedResult(uint r)
		{
			if (e.isRegisterAddressingMode)
			{
				e.set_word_in_register(e.reg_idx0, r);
			}
			else if (e.conditional_var != 7)
			{
				x = r;
				e.st8_mem8_write(x);
			}
		}
	}
}