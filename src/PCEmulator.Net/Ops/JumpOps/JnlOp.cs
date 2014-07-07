using PCEmulator.Net.Operands;

namespace PCEmulator.Net.JumpOps
{
	public class JnlOp : JumpOps
	{
		public JnlOp(JbOperand o)
			: base(o)
		{
		}

		public override void Exec()
		{
			Jump(!o.check_less_than());
		}
	}
}