using PCEmulator.Net.Operands;

namespace PCEmulator.Net.JumpOps
{
	public class JnbOp : JumpOps
	{
		public JnbOp(JbOperand o)
			: base(o)
		{
		}

		public override void Exec()
		{
			Jump(!o.check_carry());
		}
	}
}