using PCEmulator.Net.Operands;

namespace PCEmulator.Net.JumpOps
{
	public class JnpOp : JumpOps
	{
		public JnpOp(JbOperand o)
			: base(o)
		{
		}

		public override void Exec()
		{
			Jump(!o.check_parity());
		}
	}
}