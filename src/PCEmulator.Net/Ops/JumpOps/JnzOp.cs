using PCEmulator.Net.Operands;

namespace PCEmulator.Net.JumpOps
{
	public class JnzOp : JumpOps
	{
		public JnzOp(JbOperand o)
			: base(o)
		{
		}

		public override void Exec()
		{
			Jump(!o.zeroEquals());
		}
	}
}