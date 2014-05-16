using System;
using PCEmulator.Net.Utils;

namespace PCEmulator.Net
{
	public class CPU_X86_Impl : CPU_X86
	{
		private CPU_X86_Impl cpu;
		private uint CS_base;
		private uint SS_base;
		private int SS_mask;
		private bool FS_usage_flag;
		private uint init_CS_flags;

		protected override int exec_internal(uint N_cycles, IntNoException interrupt)
		{
			/*
			  x,y,z,v are either just general non-local values or their exact specialization is unclear,
			  esp. x,y look like they're used for everything

			  I don't know what 'v' should be called, it's not clear yet
			*/
			uint mem8_loc;
			//object regs;
			int _src;
			int _dst;
			int _op;
			int _op2;
			int _dst2;
			uint CS_flags;
			int mem8;
			int reg_idx0;
			uint OPbyte;
			int reg_idx1;
			uint x;
			uint y;
			object z;
			int conditional_var;
			int exit_code;
			object v;
			object iopl; //io privilege level
			Uint8Array phys_mem8;
			int last_tlb_val;
			object phys_mem16;
			object phys_mem32;
			int[] tlb_read_kernel;
			object tlb_write_kernel;
			int[] tlb_read_user;
			object tlb_write_user;
			int[] _tlb_read_;
			object _tlb_write_;

			uint eip;
			uint physmem8_ptr;
			int eip_tlb_val;
			uint initial_mem_ptr;
			uint eip_offset;

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

			OUTER_LOOP:
			do
			{
				//All the below is solely to determine what the next instruction is before re-entering the main EXEC_LOOP

				eip = (eip + physmem8_ptr - initial_mem_ptr) >> 0;
				eip_offset = (eip + CS_base) >> 0;
				eip_tlb_val = _tlb_read_[eip_offset >> 12];
				if (((eip_tlb_val | eip_offset) & 0xfff) >= (4096 - 15 + 1))
				{
					//what does this condition mean? operation straddling page boundary?
					if (eip_tlb_val == -1)
						do_tlb_set_page(eip_offset, false, cpu.cpl == 3);
					eip_tlb_val = _tlb_read_[eip_offset >> 12];
					initial_mem_ptr = physmem8_ptr = (uint)(eip_offset ^ eip_tlb_val);
					OPbyte = phys_mem8[physmem8_ptr++];
					var Cg = eip_offset & 0xfff;
					if (Cg >= (4096 - 15 + 1))
					{
						//again, WTF does this do?
						x = operation_size_function(eip_offset, OPbyte);
						if ((Cg + x) > 4096)
						{
							initial_mem_ptr = physmem8_ptr = mem_size;
							for (y = 0; y < x; y++)
							{
								mem8_loc = (eip_offset + y) >> 0;
								phys_mem8[physmem8_ptr + y] = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
									? __ld_8bits_mem8_read()
									: phys_mem8[mem8_loc ^ last_tlb_val]);
							}
							physmem8_ptr++;
						}
					}
				}
				else
				{
					initial_mem_ptr = physmem8_ptr = (uint)(eip_offset ^ eip_tlb_val);
					OPbyte = phys_mem8[physmem8_ptr++];
				}

				OPbyte |= (CS_flags = init_CS_flags) & 0x0100; //Are we running in 16bit compatibility mode? if so, ops look like 0x1XX instead of 0xXX
				//TODO: implement EXEC_LOOP
//				EXEC_LOOP:
//				for (;;)
//				{
//				}
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

		private uint ld_8bits_mem8_read()
		{
			throw new NotImplementedException();
		}

		private void st8_mem8_write(uint u)
		{
			throw new NotImplementedException();
		}

		private uint ld_8bits_mem8_write()
		{
			throw new NotImplementedException();
		}

		private uint segment_translation(int mem8)
		{
			throw new NotImplementedException();
		}

		private void set_word_in_register(int regIdx0, uint do_8BitMath)
		{
			throw new NotImplementedException();
		}

		private uint do_8bit_math(object conditionalVar, uint p1, uint p2)
		{
			throw new NotImplementedException();
		}

		private byte __ld_8bits_mem8_read()
		{
			throw new NotImplementedException();
		}

		private uint operation_size_function(uint eipOffset, object oPbyte)
		{
			//throw new NotImplementedException();
			//TODO: implement operation_size_function
			return 0;
		}

		/// <summary>
		/// Typically, the upper 20 bits of CR3 become the page directory base register (PDBR),
		/// which stores the physical address of the first page directory entry.
		/// </summary>
		private void do_tlb_set_page(uint Gd, bool Hd, bool ja)
		{
			int error_code;
			bool Od;
			if ((cpu.cr0 & (1 << 31)) == 0)
			{ //CR0: bit31 PG Paging If 1, enable paging and use the CR3 register, else disable paging
				cpu.tlb_set_page((int) (Gd & -4096), (int) (Gd & -4096), true);
			}
			else
			{
				var Id = (uint) ((cpu.cr3 & -4096) + ((Gd >> 20) & 0xffc));
				var Jd = cpu.ld32_phys(Id);
				if ((Jd & 0x00000001) == 0)
				{
					error_code = 0;
				}
				else
				{
					if ((Jd & 0x00000020) == 0)
					{
						Jd |= 0x00000020;
						cpu.st32_phys(Id, Jd);
					}
					var Kd = (uint) ((Jd & -4096) + ((Gd >> 10) & 0xffc));
					var Ld = cpu.ld32_phys(Kd);
					if ((Ld & 0x00000001) == 0)
					{
						error_code = 0;
					}
					else
					{
						var Md = Ld & Jd;
						if (ja && (Md & 0x00000004) == 0)
						{
							error_code = 0x01;
						}
						else if (Hd && (Md & 0x00000002) == 0)
						{
							error_code = 0x01;
						}
						else
						{
							var Nd = (Hd && (Ld & 0x00000040) == 0);
							if ((Ld & 0x00000020) == 0 || Nd)
							{
								Ld |= 0x00000020;
								if (Nd)
									Ld |= 0x00000040;
								cpu.st32_phys(Kd, Ld);
							}
							var ud = false;
							if ((Ld & 0x00000040) != 0 && (Md & 0x00000002) != 0)
								ud = true;
							Od = false;
							if ((Md & 0x00000004) != 0)
								Od = true;
							cpu.tlb_set_page((int) (Gd & -4096), Ld & -4096, ud, Od);
							return;
						}
					}
				}
				error_code |= (Hd ? 1 : 0) << 1;
				if (ja)
					error_code |= 0x04;
				cpu.cr2 = (int) Gd;
				abort_with_error_code(14, error_code);
			}
		}

		private void abort_with_error_code(int i, int errorCode)
		{
			throw new NotImplementedException();
		}

		private void tlb_set_page(int mem8_loc, int page_val, bool set_write_tlb, bool set_user_tlb = false)
		{
			page_val &= -4096; // only top 20bits matter
			mem8_loc &= -4096; // only top 20bits matter
			var x = mem8_loc ^ page_val; // XOR used to simulate hashing 
			var i = mem8_loc >> 12;
			if (this.tlb_read_kernel[i] == -1)
			{
				if (this.tlb_pages_count >= 2048)
				{
					this.tlb_flush_all1((i - 1) & 0xfffff);
				}
				this.tlb_pages[this.tlb_pages_count++] = i;
			}
			this.tlb_read_kernel[i] = x;
			if (set_write_tlb)
			{
				this.tlb_write_kernel[i] = x;
			}
			else
			{
				this.tlb_write_kernel[i] = -1;
			}
			if (set_user_tlb)
			{
				this.tlb_read_user[i] = x;
				if (set_write_tlb)
				{
					this.tlb_write_user[i] = x;
				}
				else
				{
					this.tlb_write_user[i] = -1;
				}
			}
			else
			{
				this.tlb_read_user[i] = -1;
				this.tlb_write_user[i] = -1;
			}
		}

		private void tlb_flush_all1(long l)
		{
			throw new NotImplementedException();
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