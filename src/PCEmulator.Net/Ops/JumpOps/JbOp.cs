using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public class JbOp : JumpOps
	{
		public JbOp(JbOperand o)
			: base(o)
		{
		}

		public override void Exec()
		{
			Jump(o.check_carry());
		}
	}
}