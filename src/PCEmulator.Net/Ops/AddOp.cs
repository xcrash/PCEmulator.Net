using PCEmulator.Net.Operands;

namespace PCEmulator.Net
{
	public class AddOp : Op
	{
		private readonly EbOperand eb;
		private readonly GbOperand gb;

		public AddOp(EbOperand eb, GbOperand gb)
			: base(eb.e)
		{
			this.eb = eb;
			this.gb = gb;
		}

		public override void Exec()
		{
			e.y = e.readY(eb);
			e.x = e.readX(gb);

			e.x = Add(e.x, e.y);

			e.setX(eb, gb, e.x);
		}

		private uint Add(uint x, uint a)
		{
			e.u_src = a;
			x = (((x + a) << 24) >> 24);
			e.u_dst = x;
			e._op = 0;
			return x;
		}
	}
}