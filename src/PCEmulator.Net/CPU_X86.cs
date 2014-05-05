using System;

namespace PCEmulator.Net
{
	internal class CPU_X86
	{
		public Action ld8_port;
		public Action ld16_port;
		public Action ld32_port;
		public Action st8_port;
		public Action st16_port;
		public Action st32_port;
		public Action get_hard_intno;

		public void phys_mem_resize(object memSize)
		{
			throw new System.NotImplementedException();
		}

		public object set_hard_irq_wrapper()
		{
			throw new System.NotImplementedException();
		}

		public object return_cycle_count()
		{
			throw new NotImplementedException();
		}
	}
}