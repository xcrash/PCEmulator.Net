using System;
using PCEmulator.Net.Operands.Args;

namespace PCEmulator.Net.Operands
{
	/// <summary>
	/// G
	/// 
	/// The reg field of the ModR/M byte selects a general register.
	/// </summary>
	internal class GbOperand : Operand<byte>
	{
		private readonly CPU_X86_Impl.Executor e;

		public GbOperand(CPU_X86_Impl.Executor e)
			: base(new BArgument(e), e)
		{
			this.e = e;
		}

		public int regIdx { get { return CPU_X86_Impl.Executor.regIdx0(e.mem8); } }

		public void set_word_in_register(uint x)
		{
			e.set_word_in_register(regIdx, x);
		}

		public override byte PopValue()
		{
			throw new NotImplementedException();
		}
	}
}