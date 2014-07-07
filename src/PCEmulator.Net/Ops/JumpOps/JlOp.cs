using PCEmulator.Net.Operands;

namespace PCEmulator.Net.JumpOps
{
	public class JlOp : JumpOps
	{
		public JlOp(JbOperand o)
			: base(o)
		{
		}

		public override void Exec()
		{
			Jump(o.check_less_than());
		}
	}
}