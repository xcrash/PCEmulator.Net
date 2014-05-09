using System;

namespace PCEmulator.Net
{
	public class CPU_X86_Impl : CPU_X86
	{
		private CPU_X86_Impl cpu;
		private int CS_base;
		private int SS_base;
		private int SS_mask;
		private bool FS_usage_flag;
		private int init_CS_flags;

		protected override int exec_internal(uint N_cycles, IntNoException interrupt)
		{
			/*
			  x,y,z,v are either just general non-local values or their exact specialization is unclear,
			  esp. x,y look like they're used for everything

			  I don't know what 'v' should be called, it's not clear yet
			*/
			object mem8_loc;
			object regs;
			int _src;
			int _dst;
			int _op;
			int _op2;
			int _dst2;
			object CS_flags;
			object mem8;
			object reg_idx0;
			object OPbyte;
			object reg_idx1;
			object x;
			object y;
			object z;
			object conditional_var;
			int exit_code;
			object v;
			object iopl; //io privilege level
			object phys_mem8;
			object last_tlb_val;
			object phys_mem16;
			object phys_mem32;
			object tlb_read_kernel;
			object tlb_write_kernel;
			object tlb_read_user;
			object tlb_write_user;
			object _tlb_read_;
			object _tlb_write_;

			uint eip;
			uint physmem8_ptr;
			object eip_tlb_val;
			uint initial_mem_ptr;
			object eip_offset;

			cpu = this;
			phys_mem8 = this.phys_mem8;
			phys_mem16 = this.phys_mem16;
			phys_mem32 = this.phys_mem32;
			tlb_read_user = this.tlb_read_user;
			tlb_write_user = this.tlb_write_user;
			tlb_read_kernel = this.tlb_read_kernel;
			tlb_write_kernel = this.tlb_write_kernel;

			if (cpu.cpl == 3)
			{
				//current privilege level
				_tlb_read_ = tlb_read_user;
				_tlb_write_ = tlb_write_user;
			}
			else
			{
				_tlb_read_ = tlb_read_kernel;
				_tlb_write_ = tlb_write_kernel;
			}

			if (cpu.halted)
			{
				if (cpu.hard_irq != 0 && (cpu.eflags & 0x00000200) != 0)
				{
					cpu.halted = false;
				}
				else
				{
					return 257;
				}
			}

			regs = this.regs;
			_src = cc_src;
			_dst = cc_dst;
			_op = cc_op;
			_op2 = cc_op2;
			_dst2 = cc_dst2;

			eip = this.eip;
			init_segment_local_vars();
			exit_code = 256;
			var cycles_left = N_cycles;

			if (interrupt != null)
			{
				do_interrupt(interrupt.intno, 0, interrupt.error_code, 0, 0);
			}
			if (cpu.hard_intno >= 0)
			{
				do_interrupt(cpu.hard_intno, 0, 0, 0, 1);
				cpu.hard_intno = -1;
			}
			if (cpu.hard_irq != 0 && (cpu.eflags & 0x00000200) != 0)
			{
				cpu.hard_intno = cpu.get_hard_intno();
				do_interrupt(cpu.hard_intno, 0, 0, 0, 1);
				cpu.hard_intno = -1;
			}

			physmem8_ptr = 0;
			initial_mem_ptr = 0;

			do
			{
			} while (--cycles_left != 0); //End Giant Core DO WHILE Execution Loop
			cycle_count += (N_cycles - cycles_left);
			this.eip = (eip + physmem8_ptr - initial_mem_ptr);
			cc_src = _src;
			cc_dst = _dst;
			cc_op = _op;
			cc_op2 = _op2;
			cc_dst2 = _dst2;
			return exit_code;
		}

		private void do_interrupt(int intno, int p1, int errorCode, int p3, int p4)
		{
			throw new NotImplementedException();
		}

		private void init_segment_local_vars()
		{
			CS_base = cpu.segs[1].@base; //CS
			SS_base = cpu.segs[2].@base; //SS
			if ((cpu.segs[2].flags & (1 << 22)) != 0)
				SS_mask = -1;
			else
				SS_mask = 0xffff;
			FS_usage_flag = (((CS_base | SS_base | cpu.segs[3].@base | cpu.segs[0].@base) == 0) && SS_mask == -1);
			if ((cpu.segs[1].flags & (1 << 22)) != 0)
				init_CS_flags = 0;
			else
				init_CS_flags = 0x0100 | 0x0080;
		}
	}
}