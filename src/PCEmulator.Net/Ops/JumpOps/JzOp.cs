using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public class JzOp : JumpOps
	{
		public JzOp(JbOperand o)
			: base(o)
		{
		}

		public override void Exec()
		{
			Jump(o.zeroEquals());
		}
	}
}