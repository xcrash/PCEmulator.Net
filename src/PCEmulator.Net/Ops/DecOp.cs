using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public class DecOp : IncDecOp
	{
		public DecOp(Operand<uint> o)
			: base(o.e, o)
		{
		}

		protected override void ExecInternal()
		{
			o.setX = e.u_dst = (o.readX() - 1) >> 0;
			e._op = (int)MagicEnum.Dec;
		}
	}
}