using PCEmulator.Net.Utils;

namespace PCEmulator.Net
{
	public class OpContext
	{
		public readonly CPU_X86_Impl.Executor e;

		protected int mem8
		{
			//get { return e.mem8; }
			set { e.mem8 = value; }
		}

		protected Uint8Array phys_mem8
		{
			get { return e.phys_mem8; }
		}

		protected Int32Array phys_mem32
		{
			get { return e.phys_mem32; }
		}

		protected uint physmem8_ptr
		{
			get { return e.physmem8_ptr; }
			set { e.physmem8_ptr = value; }
		}

		protected uint x
		{
			get { return e.x; }
			set { e.x = value; }
		}

		protected uint y
		{
			get { return e.y; }
			set { e.y = value; }
		}

		protected int z
		{
			get { return e.z; }
			set { e.z = value; }
		}

		protected uint[] regs
		{
			get { return e.regs; }
		}

		protected bool FS_usage_flag
		{
			get { return e.FS_usage_flag; }
		}

		protected uint mem8_loc
		{
			get { return e.mem8_loc; }
			set { e.mem8_loc = value; }
		}

		protected int last_tlb_val
		{
			get { return e.last_tlb_val; }
			set { e.last_tlb_val = value; }

		}

		protected int[] _tlb_read_
		{
			get { return e._tlb_read_; }
		}

		protected int[] _tlb_write_
		{
			get { return e._tlb_write_; }
		}

		public OpContext(CPU_X86_Impl.Executor e)
		{
			this.e = e;
		}
	}
}