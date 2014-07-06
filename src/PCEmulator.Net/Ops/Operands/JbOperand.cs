using PCEmulator.Net.Operands.Args;

namespace PCEmulator.Net.Operands
{
	/// <summary>
	/// The instruction contains a relative offset to be added to the address of the subsequent instruction. Applicable, e.g., to short JMP (opcode EB), or LOOP.
	/// </summary>
	public class JbOperand : Operand<byte>
	{
		private readonly CPU_X86_Impl.Executor e;

		public JbOperand(CPU_X86_Impl.Executor e)
			: base(new BArgument(e), e)
		{
			this.e = e;
		}

		public bool check_overflow()
		{
			return e.check_overflow() != 0;
		}

		public bool check_carry()
		{
			return e.check_carry() != 0;
		}

		public bool zeroEquals()
		{
			return e.u_dst == 0;
		}

		public bool check_below_or_equal()
		{
			return e.check_below_or_equal();
		}

		public bool check_sign()
		{
			return (e._op == 24 ? (int)((e._src >> 7) & 1) : ((int)e._dst < 0 ? 1 : 0)) != 0;
		}

		public bool check_parity()
		{
			return e.check_parity() != 0;
		}

		public bool check_less_than()
		{
			return e.check_less_than();
		}

		public bool check_less_or_equal()
		{
			return e.check_less_or_equal();
		}

		public new uint readX()
		{
			return (uint)((base.readX() << 24) >> 24);
		}

		public override byte PopValue()
		{
			throw new System.NotImplementedException();
		}
	}
}