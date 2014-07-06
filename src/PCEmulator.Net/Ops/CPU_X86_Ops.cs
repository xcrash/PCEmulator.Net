namespace PCEmulator.Net
{
	public partial class CPU_X86_Impl
	{
		public partial class Executor
		{
			internal readonly OperandsHelper Operands;

			public Executor()
			{
				Operands = new OperandsHelper(this);
			}

			private static void ExecOp(Op op)
			{
				op.Exec();
			}
		}
	}
}