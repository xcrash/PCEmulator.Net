using PCEmulator.Net.Operands;

namespace PCEmulator.Net.PushPopOps
{
	public class PushOp<T> : SingleOperandOp<uint>
	{
		public PushOp(Operand<byte> ctx)
			: base(ctx.e, new ByteToUintOperandConverter(ctx))
		{
		}

		public PushOp(Operand<uint> o)
			: base(o.e, o)
		{
		}

		public override void Exec()
		{
			var x = o.readX();
			Push(x);
		}

		private void Push(uint x)
		{
			if (FS_usage_flag)
			{
				mem8_loc = (regs[4] - 4) >> 0;
				e.st32_mem8_write(x);
				regs[4] = mem8_loc;
			}
			else
			{
				e.push_dword_to_stack(x);
			}
		}
	}
}