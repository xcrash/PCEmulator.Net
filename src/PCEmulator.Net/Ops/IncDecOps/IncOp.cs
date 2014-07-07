using PCEmulator.Net.Operands;

namespace PCEmulator.Net.IncDecOps
{
	public class IncOp : IncDecOpBase
	{
		public IncOp(Operand<uint> o) : base(o.e, o)
		{
		}

		protected override void ExecInternal()
		{
			o.setX = e.u_dst = (o.readX() + 1) >> 0;
			e._op = (int) MagicEnum.Inc;
		}
	}
}