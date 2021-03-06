using PCEmulator.Net.Operands.Args;

namespace PCEmulator.Net.Operands
{
	/// <summary>
	/// E
	/// 
	/// A ModR/M byte follows the opcode and specifies the operand. The operand is either a general-purpose register or a memory address. If it is a memory address, the address is computed from a segment register and any of the following values: a base register, an index register, a displacement.
	/// </summary>
	public class EvOperand : Operand<uint>
	{
		private readonly CPU_X86_Impl.Executor e;

		public EvOperand(CPU_X86_Impl.Executor e)
			: base(new VArgument(e), e)
		{
			this.e = e;
		}

		public override uint PopValue()
		{
			mem8 = phys_mem8[physmem8_ptr++];
			x = e.pop_dword_from_stack_read();
			if (!e.isRegisterAddressingMode)
				y = regs[4];

			e.pop_dword_from_stack_incr_ptr();

			return x;
		}

		public override uint ReadOpValue0()
		{
			throw new System.NotImplementedException();
		}

		public override uint ReadOpValue1()
		{
			throw new System.NotImplementedException();
		}

		public override void ProceedResult(uint r)
		{
			throw new System.NotImplementedException();
		}
	}
}