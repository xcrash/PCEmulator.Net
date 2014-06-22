namespace PCEmulator.Net
{
	public partial class CPU_X86_Impl
	{
		public partial class Executor
		{
			private void Inc(RegsSingleOpContext ctx)
			{
				IncDecInit();
				ctx.setX = u_dst = (ctx.readX() + 1) >> 0;
				_op = 27;
			}

			private void Dec(RegsSingleOpContext ctx)
			{
				IncDecInit();
				ctx.setX = u_dst = (ctx.readX() - 1) >> 0;
				_op = 30;
			}

			private void IncDecInit()
			{
				reg_idx1 = (int)(OPbyteRegIdx0);
				if (_op < 25)
				{
					_op2 = _op;
					_dst2 = (int) u_dst;
				}
			}
		}
	}
}