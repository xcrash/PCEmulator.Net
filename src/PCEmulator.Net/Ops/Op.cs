namespace PCEmulator.Net
{
	public abstract class Op : CPU_X86_Impl.Executor.OpContext
	{
		protected Op(CPU_X86_Impl.Executor e) : base(e)
		{
		}

		public abstract void Exec();
	}
}