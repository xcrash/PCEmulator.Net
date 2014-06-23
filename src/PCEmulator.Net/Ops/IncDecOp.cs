namespace PCEmulator.Net
{
	public abstract class IncDecOp : SingleOperandOp<uint>
	{
		public enum MagicEnum
		{
			Inc = 27,
			Dec = 30
		}

		protected IncDecOp(CPU_X86_Impl.Executor e, CPU_X86_Impl.Executor.IOperand<uint> ctx)
			: base(e, ctx)
		{
		}

		public override void Exec()
		{
			Init();
			ExecInternal();
		}

		protected void Init()
		{
			e.reg_idx1 = (int) (e.OPbyteRegIdx0);
			if (e._op < 25)
			{
				e._op2 = e._op;
				e._dst2 = (int) e.u_dst;
			}
		}

		protected abstract void ExecInternal();
	}
}