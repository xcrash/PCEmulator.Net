using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public class JpOp : JumpOps
	{
		public JpOp(JbOperand o)
			: base(o)
		{
		}

		public override void Exec()
		{
			Jump(o.check_parity());
		}
	}
}