using System;
using PCEmulator.Net.Utils;

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
		private uint mem_size;
		private byte[] phys_mem;
		private Uint8Array phys_mem8;
		private Uint16Array phys_mem16;
		private Int32Array phys_mem32;

		/// <summary>
		/// Allocates a memory chunnk new_mem_size bytes long and makes 8,16,32 bit array references into it
		/// </summary>
		/// <param name="new_mem_size"></param>
		public void phys_mem_resize(uint new_mem_size)
		{
			this.mem_size = new_mem_size;
			new_mem_size += ((15 + 3) & ~3);
			this.phys_mem = new byte[new_mem_size];
			this.phys_mem8 = new Uint8Array(this.phys_mem, 0, new_mem_size);
			this.phys_mem16 = new Uint16Array(this.phys_mem, 0, new_mem_size / 2);
			this.phys_mem32 = new Int32Array(this.phys_mem, 0, new_mem_size / 4);
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