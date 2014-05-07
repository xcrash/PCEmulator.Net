using System;

namespace PCEmulator.Net
{
	public class CPU_X86
	{
		public Action get_hard_intno;
		public int cycle_count;
		public Func<uint, byte> ld8_port;
		public Func<uint, ushort> ld16_port;
		public Func<uint, uint> ld32_port;
		public Action<uint, byte> st8_port;
		public Action<uint, ushort> st16_port;
		public Action<uint, uint> st32_port;
		public object eip;
		public object[] regs;

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

		public object load_binary(object url, object mem8Loc)
		{
			throw new NotImplementedException();
		}

		public int exec(int i)
		{
			throw new NotImplementedException();
		}

		public void write_string(object cmdlineAddr, string args)
		{
			throw new NotImplementedException();
		}
	}
}