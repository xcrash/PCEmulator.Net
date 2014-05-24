using System;
using System.Text;
using PCEmulator.Net.Utils;

namespace PCEmulator.Net
{
	public class CPU_X86_Impl : CPU_X86
	{
		private CPU_X86_Impl cpu;
		private uint mem8_loc;

		private uint _src;
		private uint _dst;
		private int _op;

		private uint CS_base;
		private uint SS_base;
		private int SS_mask;
		private bool FS_usage_flag;
		private uint init_CS_flags;
		private int[] _tlb_write_;
		private uint CS_flags;
		int last_tlb_val;

		protected override int exec_internal(uint N_cycles, IntNoException interrupt)
		{
			/*
			  x,y,z,v are either just general non-local values or their exact specialization is unclear,
			  esp. x,y look like they're used for everything

			  I don't know what 'v' should be called, it's not clear yet
			*/
			int _op2;
			int _dst2;
			int mem8;
			int reg_idx0;
			uint OPbyte;
			int reg_idx1;
			uint x;
			uint y;
			bool z;
			int conditional_var;
			int exit_code;
			object v;
			int iopl; //io privilege level
			Uint8Array phys_mem8;
			object phys_mem16;
			Int32Array phys_mem32;
			int[] tlb_read_kernel;
			int[] tlb_write_kernel;
			int[] tlb_read_user;
			int[] tlb_write_user;

			int eip_tlb_val;
			uint initial_mem_ptr;

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

			regs = regs;
			_src = (uint) cc_src;
			_dst = (uint) cc_dst;
			_op = cc_op;
			_op2 = cc_op2;
			_dst2 = cc_dst2;

			eip = (uint) this.eip;
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

			#region OUTER_LOOP
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

				OPbyte |= (CS_flags = init_CS_flags) & 0x0100;
			//Are we running in 16bit compatibility mode? if so, ops look like 0x1XX instead of 0xXX
			//TODO: implement EXEC_LOOP
			EXEC_LOOP:
				for (; ; )
				{
					switch (OPbyte)
					{
						case 0x00: //ADD Gb Eb Add
						case 0x08: //OR Gb Eb Logical Inclusive OR
						case 0x10: //ADC Gb Eb Add with Carry
						case 0x18: //SBB Gb Eb Integer Subtraction with Borrow
						case 0x20: //AND Gb Eb Logical AND
						case 0x28: //SUB Gb Eb Subtract
						case 0x30: //XOR Gb Eb Logical Exclusive OR
						case 0x38: //CMP Eb  Compare Two Operands
							mem8 = phys_mem8[physmem8_ptr++];
							conditional_var = (int)(OPbyte >> 3);
							reg_idx1 = (mem8 >> 3) & 7;
							y = (regs[reg_idx1 & 3] >> ((reg_idx1 & 4) << 1));
							if ((mem8 >> 6) == 3)
							{
								reg_idx0 = mem8 & 7;
								set_word_in_register(reg_idx0, do_8bit_math(conditional_var, (regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1)), y));
							}
							else
							{
								mem8_loc = segment_translation(mem8);
								if (conditional_var != 7)
								{
									x = ld_8bits_mem8_write();
									x = do_8bit_math(conditional_var, x, y);
									st8_mem8_write(x);
								}
								else
								{
									x = ld_8bits_mem8_read();
									do_8bit_math(7, x, y);
								}
							}
							goto EXEC_LOOP_END;
						case 0x01: //ADD Gvqp Evqp Add
							mem8 = phys_mem8[physmem8_ptr++];
							y = regs[(mem8 >> 3) & 7];
							if ((mem8 >> 6) == 3)
							{
								reg_idx0 = mem8 & 7;
								{
									_src = y;
									_dst = regs[reg_idx0] = (regs[reg_idx0] + _src) >> 0;
									_op = 2;
								}
							}
							else
							{
								mem8_loc = segment_translation(mem8);
								x = ld_32bits_mem8_write();
								{
									_src = y;
									_dst = x = (x + _src) >> 0;
									_op = 2;
								}
								st32_mem8_write(x);
							}
							goto EXEC_LOOP_END;
						case 0x02: //ADD Eb Gb Add
						case 0x0a: //OR Eb Gb Logical Inclusive OR
						case 0x12: //ADC Eb Gb Add with Carry
						case 0x1a: //SBB Eb Gb Integer Subtraction with Borrow
						case 0x22: //AND Eb Gb Logical AND
						case 0x2a: //SUB Eb Gb Subtract
						case 0x32: //XOR Eb Gb Logical Exclusive OR
						case 0x3a: //CMP Gb  Compare Two Operands
							mem8 = phys_mem8[physmem8_ptr++];
							conditional_var = (int) (OPbyte >> 3);
							reg_idx1 = (mem8 >> 3) & 7;
							if ((mem8 >> 6) == 3)
							{
								reg_idx0 = mem8 & 7;
								y = (regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1));
							}
							else
							{
								mem8_loc = segment_translation(mem8);
								y = ld_8bits_mem8_read();
							}
							set_word_in_register(reg_idx1, do_8bit_math(conditional_var, (regs[reg_idx1 & 3] >> ((reg_idx1 & 4) << 1)), y));
							goto EXEC_LOOP_END;
						case 0x04: //ADD Ib AL Add
						case 0x0c: //OR Ib AL Logical Inclusive OR
						case 0x14: //ADC Ib AL Add with Carry
						case 0x1c: //SBB Ib AL Integer Subtraction with Borrow
						case 0x24: //AND Ib AL Logical AND
						case 0x2c: //SUB Ib AL Subtract
						case 0x34: //XOR Ib AL Logical Exclusive OR
						case 0x3c: //CMP AL  Compare Two Operands
							y = phys_mem8[physmem8_ptr++];
							conditional_var = (int)(OPbyte >> 3);
							set_word_in_register(0, do_8bit_math(conditional_var, regs[0] & 0xff, y));
							goto EXEC_LOOP_END;
						case 0x05: //ADD Ivds rAX Add
							{
								y = (uint)(phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
											(phys_mem8[physmem8_ptr + 3] << 24));
								physmem8_ptr += 4;
							}
							{
								_src = y;
								_dst = regs[0] = (regs[0] + _src) >> 0;
								_op = 2;
							}
							goto EXEC_LOOP_END;
						case 0x09: //OR Gvqp Evqp Logical Inclusive OR
						case 0x11: //ADC Gvqp Evqp Add with Carry
						case 0x19: //SBB Gvqp Evqp Integer Subtraction with Borrow
						case 0x21: //AND Gvqp Evqp Logical AND
						case 0x29: //SUB Gvqp Evqp Subtract
						case 0x31: //XOR Gvqp Evqp Logical Exclusive OR
							mem8 = phys_mem8[physmem8_ptr++];
							conditional_var = (int) (OPbyte >> 3);
							y = regs[(mem8 >> 3) & 7];
							if ((mem8 >> 6) == 3)
							{
								reg_idx0 = mem8 & 7;
								regs[reg_idx0] = do_32bit_math(conditional_var, regs[reg_idx0], y);
							}
							else
							{
								mem8_loc = segment_translation(mem8);
								x = ld_32bits_mem8_write();
								x = do_32bit_math(conditional_var, x, y);
								st32_mem8_write(x);
							}
							goto EXEC_LOOP_END;
						case 0x0d: //OR Ivds rAX Logical Inclusive OR
						case 0x15: //ADC Ivds rAX Add with Carry
						case 0x1d: //SBB Ivds rAX Integer Subtraction with Borrow
						case 0x25: //AND Ivds rAX Logical AND
						case 0x2d: //SUB Ivds rAX Subtract
						{
							y = (uint) (phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
							            (phys_mem8[physmem8_ptr + 3] << 24));
							physmem8_ptr += 4;
						}
							conditional_var = (int) (OPbyte >> 3);
							regs[0] = do_32bit_math(conditional_var, regs[0], y);
							goto EXEC_LOOP_END;
						case 0x2f: //DAS  AL Decimal Adjust AL after Subtraction
							op_DAS();
							goto EXEC_LOOP_END;
						case 0x39: //CMP Evqp  Compare Two Operands
							mem8 = phys_mem8[physmem8_ptr++];
							conditional_var = (int) (OPbyte >> 3);
							y = regs[(mem8 >> 3) & 7];
							if ((mem8 >> 6) == 3)
							{
								reg_idx0 = mem8 & 7;
								{
									_src = y;
									_dst = (regs[reg_idx0] - _src) >> 0;
									_op = 8;
								}
							}
							else
							{
								mem8_loc = segment_translation(mem8);
								x = ld_32bits_mem8_read();
								{
									_src = y;
									_dst = (x - _src) >> 0;
									_op = 8;
								}
							}
							goto EXEC_LOOP_END;
						case 0x3b: //CMP Gvqp  Compare Two Operands
							mem8 = phys_mem8[physmem8_ptr++];
							conditional_var = (int)(OPbyte >> 3);
							reg_idx1 = (mem8 >> 3) & 7;
							if ((mem8 >> 6) == 3)
							{
								y = regs[mem8 & 7];
							}
							else
							{
								mem8_loc = segment_translation(mem8);
								y = ld_32bits_mem8_read();
							}
							{
								_src = y;
								_dst = (regs[reg_idx1] - _src) >> 0;
								_op = 8;
							}
							goto EXEC_LOOP_END;
						case 0x3d: //CMP rAX  Compare Two Operands
						{
							y =
								(uint)
									(phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
									 (phys_mem8[physmem8_ptr + 3] << 24));
							physmem8_ptr += 4;
						}
						{
							_src = y;
							_dst = (regs[0] - _src) >> 0;
							_op = 8;
						}
							goto EXEC_LOOP_END;
						case 0x50: //PUSH Zv SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
						case 0x51:
						case 0x52:
						case 0x53:
						case 0x54:
						case 0x55:
						case 0x56:
						case 0x57:
							x = regs[OPbyte & 7];
							if (FS_usage_flag)
							{
								mem8_loc = (regs[4] - 4) >> 0;
								{
									last_tlb_val = _tlb_write_[mem8_loc >> 12];
									if (((last_tlb_val | mem8_loc) & 3) != 0)
									{
										__st32_mem8_write(x);
									}
									else
									{
										phys_mem32[(mem8_loc ^ last_tlb_val) >> 2] = (int)x;
									}
								}
								regs[4] = mem8_loc;
							}
							else
							{
								push_dword_to_stack(x);
							}
							goto EXEC_LOOP_END;
						case 0x58: //POP SS:[rSP] Zv Pop a Value from the Stack
						case 0x59:
						case 0x5a:
						case 0x5b:
						case 0x5c:
						case 0x5d:
						case 0x5e:
						case 0x5f:
							if (FS_usage_flag)
							{
								mem8_loc = regs[4];
								x = ((((last_tlb_val = _tlb_read_[mem8_loc >> 12]) | mem8_loc) & 3) != 0
									? __ld_32bits_mem8_read()
									: (uint)phys_mem32[(mem8_loc ^ last_tlb_val) >> 2]);
								regs[4] = (mem8_loc + 4) >> 0;
							}
							else
							{
								x = pop_dword_from_stack_read();
								pop_dword_from_stack_incr_ptr();
							}
							regs[OPbyte & 7] = x;
							goto EXEC_LOOP_END;
						case 0x63: //ARPL Ew  Adjust RPL Field of Segment Selector
							op_ARPL();
							goto EXEC_LOOP_END;
						case 0x64://FS FS  FS segment override prefix
						case 0x65://GS GS  GS segment override prefix
							if (CS_flags == init_CS_flags)
								operation_size_function(eip_offset, OPbyte);
							CS_flags = (uint) ((CS_flags & ~0x000f) | ((OPbyte & 7) + 1));
							OPbyte = phys_mem8[physmem8_ptr++];
							OPbyte |= (CS_flags & 0x0100);
							break;
						case 0x66://   Operand-size override prefix
							if (CS_flags == init_CS_flags)
								operation_size_function(eip_offset, OPbyte);
							if ((init_CS_flags & 0x0100) != 0)
								CS_flags = (uint) (CS_flags & ~0x0100);
							else
								CS_flags |= 0x0100;
							OPbyte = phys_mem8[physmem8_ptr++];
							OPbyte |= (CS_flags & 0x0100);
							break;
						case 0x67://   Address-size override prefix
							if (CS_flags == init_CS_flags)
								operation_size_function(eip_offset, OPbyte);
							if ((init_CS_flags & 0x0080) != 0)
								CS_flags = (uint) (CS_flags & ~0x0080);
							else
								CS_flags |= 0x0080;
							OPbyte = phys_mem8[physmem8_ptr++];
							OPbyte |= (CS_flags & 0x0100);
							break;
						case 0x6c: //INS DX (ES:)[rDI] Input from Port to String
							stringOp_INSB();
						{
							if (cpu.hard_irq != 0 && (cpu.eflags & 0x00000200) != 0)
								goto OUTER_LOOP_END;
						}
							goto EXEC_LOOP_END;
						case 0x6f: //OUTS DS:[SI] DX Output String to Port
							stringOp_OUTSD();
						{
							if (cpu.hard_irq != 0 && (cpu.eflags & 0x00000200) != 0)
								goto OUTER_LOOP_END;
						}
							goto EXEC_LOOP_END;
						case 0x70: //JO Jbs  Jump short if overflow (OF=1)
							if (check_overflow() != 0)
							{
								x = (uint) ((phys_mem8[physmem8_ptr++] << 24) >> 24);
								physmem8_ptr = (physmem8_ptr + x) >> 0;
							}
							else
							{
								physmem8_ptr = (physmem8_ptr + 1) >> 0;
							}
							goto EXEC_LOOP_END;
						case 0x71: //JNO Jbs  Jump short if not overflow (OF=0)
							if (check_overflow() == 0)
							{
								x = (uint) ((phys_mem8[physmem8_ptr++] << 24) >> 24);
								physmem8_ptr = (physmem8_ptr + x) >> 0;
							}
							else
							{
								physmem8_ptr = (physmem8_ptr + 1) >> 0;
							}
							goto EXEC_LOOP_END;
						case 0x72: //JB Jbs  Jump short if below/not above or equal/carry (CF=1)
							if (check_carry() != 0)
							{
								x = (uint) ((phys_mem8[physmem8_ptr++] << 24) >> 24);
								physmem8_ptr = (physmem8_ptr + x) >> 0;
							}
							else
							{
								physmem8_ptr = (physmem8_ptr + 1) >> 0;
							}
							goto EXEC_LOOP_END;
						case 0x73: //JNB Jbs  Jump short if not below/above or equal/not carry (CF=0)
							if (check_carry() == 0)
							{
								x = (uint) ((phys_mem8[physmem8_ptr++] << 24) >> 24);
								physmem8_ptr = (physmem8_ptr + x) >> 0;
							}
							else
							{
								physmem8_ptr = (physmem8_ptr + 1) >> 0;
							}
							goto EXEC_LOOP_END;
						case 0x74: //JZ Jbs  Jump short if zero/equal (ZF=0)
							if ((_dst == 0))
							{
								x = (uint)((phys_mem8[physmem8_ptr++] << 24) >> 24);
								physmem8_ptr = (physmem8_ptr + x) >> 0;
							}
							else
							{
								physmem8_ptr = (physmem8_ptr + 1) >> 0;
							}
							goto EXEC_LOOP_END;
						case 0x75: //JNZ Jbs  Jump short if not zero/not equal (ZF=1)
							if (_dst != 0)
							{
								x = (uint)((phys_mem8[physmem8_ptr++] << 24) >> 24);
								physmem8_ptr = (physmem8_ptr + x) >> 0;
							}
							else
							{
								physmem8_ptr = (physmem8_ptr + 1) >> 0;
							}
							goto EXEC_LOOP_END;
						case 0x76: //JBE Jbs  Jump short if below or equal/not above (CF=1 AND ZF=1)
							if (check_below_or_equal())
							{
								x = (uint) ((phys_mem8[physmem8_ptr++] << 24) >> 24);
								physmem8_ptr = (physmem8_ptr + x) >> 0;
							}
							else
							{
								physmem8_ptr = (physmem8_ptr + 1) >> 0;
							}
							goto EXEC_LOOP_END;
						case 0x77: //JNBE Jbs  Jump short if not below or equal/above (CF=0 AND ZF=0)
							if (!check_below_or_equal())
							{
								x = (uint)((phys_mem8[physmem8_ptr++] << 24) >> 24);
								physmem8_ptr = (physmem8_ptr + x) >> 0;
							}
							else
							{
								physmem8_ptr = (physmem8_ptr + 1) >> 0;
							}
							goto EXEC_LOOP_END;
						case 0x78: //JS Jbs  Jump short if sign (SF=1)
							if ((_op == 24 ? (int)((_src >> 7) & 1) : ((int)_dst < 0 ? 1 : 0)) != 0)
							{
								x = (uint)((phys_mem8[physmem8_ptr++] << 24) >> 24);
								physmem8_ptr = (physmem8_ptr + x) >> 0;
							}
							else
							{
								physmem8_ptr = (physmem8_ptr + 1) >> 0;
							}
							goto EXEC_LOOP_END;
						case 0x79: //JNS Jbs  Jump short if not sign (SF=0)
							if ((_op == 24 ? (int)((_src >> 7) & 1) : ((int)_dst < 0 ? 1 : 0)) == 0)
							{
								x = (uint) ((phys_mem8[physmem8_ptr++] << 24) >> 24);
								physmem8_ptr = (physmem8_ptr + x) >> 0;
							}
							else
							{
								physmem8_ptr = (physmem8_ptr + 1) >> 0;
							}
							goto EXEC_LOOP_END;
						case 0x7a: //JP Jbs  Jump short if parity/parity even (PF=1)
							if (check_parity() != 0)
							{
								x = (uint) ((phys_mem8[physmem8_ptr++] << 24) >> 24);
								physmem8_ptr = (physmem8_ptr + x) >> 0;
							}
							else
							{
								physmem8_ptr = (physmem8_ptr + 1) >> 0;
							}
							goto EXEC_LOOP_END;
						case 0x7b: //JNP Jbs  Jump short if not parity/parity odd
							if (check_parity() == 0)
							{
								x = (uint) ((phys_mem8[physmem8_ptr++] << 24) >> 24);
								physmem8_ptr = (physmem8_ptr + x) >> 0;
							}
							else
							{
								physmem8_ptr = (physmem8_ptr + 1) >> 0;
							}
							goto EXEC_LOOP_END;
						case 0x7c: //JL Jbs  Jump short if less/not greater (SF!=OF)
							if (check_less_than())
							{
								x = (uint) ((phys_mem8[physmem8_ptr++] << 24) >> 24);
								physmem8_ptr = (physmem8_ptr + x) >> 0;
							}
							else
							{
								physmem8_ptr = (physmem8_ptr + 1) >> 0;
							}
							goto EXEC_LOOP_END;
						case 0x7d: //JNL Jbs  Jump short if not less/greater or equal (SF=OF)
							if (!check_less_than())
							{
								x = (uint) ((phys_mem8[physmem8_ptr++] << 24) >> 24);
								physmem8_ptr = (physmem8_ptr + x) >> 0;
							}
							else
							{
								physmem8_ptr = (physmem8_ptr + 1) >> 0;
							}
							goto EXEC_LOOP_END;
						case 0x7e: //JLE Jbs  Jump short if less or equal/not greater ((ZF=1) OR (SF!=OF))
							if (check_less_or_equal())
							{
								x = (uint)((phys_mem8[physmem8_ptr++] << 24) >> 24);
								physmem8_ptr = (physmem8_ptr + x) >> 0;
							}
							else
							{
								physmem8_ptr = (physmem8_ptr + 1) >> 0;
							}
							goto EXEC_LOOP_END;
						case 0x7f: //JNLE Jbs  Jump short if not less nor equal/greater ((ZF=0) AND (SF=OF))
							if (!check_less_or_equal())
							{
								x = (uint) ((phys_mem8[physmem8_ptr++] << 24) >> 24);
								physmem8_ptr = (physmem8_ptr + x) >> 0;
							}
							else
							{
								physmem8_ptr = (physmem8_ptr + 1) >> 0;
							}
							goto EXEC_LOOP_END;

						case 0x80: //ADD Ib Eb Add
						case 0x82: //ADD Ib Eb Add
							mem8 = phys_mem8[physmem8_ptr++];
							conditional_var = (mem8 >> 3) & 7;
							if ((mem8 >> 6) == 3)
							{
								reg_idx0 = mem8 & 7;
								y = phys_mem8[physmem8_ptr++];
								set_word_in_register(reg_idx0, do_8bit_math(conditional_var, (regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1)), y));
							}
							else
							{
								mem8_loc = segment_translation(mem8);
								y = phys_mem8[physmem8_ptr++];
								if (conditional_var != 7)
								{
									x = ld_8bits_mem8_write();
									x = do_8bit_math(conditional_var, x, y);
									st8_mem8_write(x);
								}
								else
								{
									x = ld_8bits_mem8_read();
									do_8bit_math(7, x, y);
								}
							}
							goto EXEC_LOOP_END;
						case 0x83: //ADD Ibs Evqp Add
							mem8 = phys_mem8[physmem8_ptr++];
							conditional_var = (mem8 >> 3) & 7;
							if (conditional_var == 7)
							{
								if ((mem8 >> 6) == 3)
								{
									x = regs[mem8 & 7];
								}
								else
								{
									mem8_loc = segment_translation(mem8);
									x = ld_32bits_mem8_read();
								}
								y = (uint)((phys_mem8[physmem8_ptr++] << 24) >> 24);
								{
									_src = y;
									_dst = ((x - _src) >> 0);
									_op = 8;
								}
							}
							else
							{
								if ((mem8 >> 6) == 3)
								{
									reg_idx0 = mem8 & 7;
									y = (uint)((phys_mem8[physmem8_ptr++] << 24) >> 24);
									regs[reg_idx0] = do_32bit_math(conditional_var, regs[reg_idx0], y);
								}
								else
								{
									mem8_loc = segment_translation(mem8);
									y = (uint)((phys_mem8[physmem8_ptr++] << 24) >> 24);
									x = ld_32bits_mem8_write();
									x = do_32bit_math(conditional_var, x, y);
									st32_mem8_write(x);
								}
							}
							goto EXEC_LOOP_END;
						case 0x84: //TEST Eb  Logical Compare
							mem8 = phys_mem8[physmem8_ptr++];
							if ((mem8 >> 6) == 3)
							{
								reg_idx0 = mem8 & 7;
								x = (regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1));
							}
							else
							{
								mem8_loc = segment_translation(mem8);
								x = ld_8bits_mem8_read();
							}
							reg_idx1 = (mem8 >> 3) & 7;
							y = (regs[reg_idx1 & 3] >> ((reg_idx1 & 4) << 1));
							{
								_dst = (((x & y) << 24) >> 24);
								_op = 12;
							}
							goto EXEC_LOOP_END;
						case 0x85: //TEST Evqp  Logical Compare
							mem8 = phys_mem8[physmem8_ptr++];
							if ((mem8 >> 6) == 3)
							{
								x = regs[mem8 & 7];
							}
							else
							{
								mem8_loc = segment_translation(mem8);
								x = ld_32bits_mem8_read();
							}
							y = regs[(mem8 >> 3) & 7];
						{
							_dst = x & y;
							_op = 14;
						}
							goto EXEC_LOOP_END;
						case 0x88: //MOV Gb Eb Move
							mem8 = phys_mem8[physmem8_ptr++];
							reg_idx1 = (mem8 >> 3) & 7;
							x = (regs[reg_idx1 & 3] >> ((reg_idx1 & 4) << 1));
							if ((mem8 >> 6) == 3)
							{
								reg_idx0 = mem8 & 7;
								last_tlb_val = (reg_idx0 & 4) << 1;
								regs[reg_idx0 & 3] = (uint) ((regs[reg_idx0 & 3] & ~(0xff << last_tlb_val)) | (((x) & 0xff) << last_tlb_val));
							}
							else
							{
								mem8_loc = segment_translation(mem8);
								{
									last_tlb_val = _tlb_write_[mem8_loc >> 12];
									if (last_tlb_val == -1)
									{
										__st8_mem8_write(x);
									}
									else
									{
										phys_mem8[mem8_loc ^ last_tlb_val] = (byte) x;
									}
								}
							}
							goto EXEC_LOOP_END;
						case 0x89: //MOV Gvqp Evqp Move
							mem8 = phys_mem8[physmem8_ptr++];
							x = regs[(mem8 >> 3) & 7];
							if ((mem8 >> 6) == 3)
							{
								regs[mem8 & 7] = x;
							}
							else
							{
								mem8_loc = segment_translation(mem8);
								{
									last_tlb_val = _tlb_write_[mem8_loc >> 12];
									if (((last_tlb_val | mem8_loc) & 3) != 0)
									{
										__st32_mem8_write(x);
									}
									else
									{
										phys_mem32[(mem8_loc ^ last_tlb_val) >> 2] = (int)x;
									}
								}
							}
							goto EXEC_LOOP_END;
						case 0x8b: //MOV Evqp Gvqp Move
							mem8 = phys_mem8[physmem8_ptr++];
							if ((mem8 >> 6) == 3)
							{
								x = regs[mem8 & 7];
							}
							else
							{
								mem8_loc = segment_translation(mem8);
								x = ((((last_tlb_val = _tlb_read_[mem8_loc >> 12]) | mem8_loc) & 3) != 0
									? __ld_32bits_mem8_read()
									: (uint)phys_mem32[(mem8_loc ^ last_tlb_val) >> 2]);
							}
							regs[(mem8 >> 3) & 7] = x;
							goto EXEC_LOOP_END;
						case 0x8d: //LEA M Gvqp Load Effective Address
							mem8 = phys_mem8[physmem8_ptr++];
							if ((mem8 >> 6) == 3)
								abort(6);
							CS_flags = (uint) ((CS_flags & ~0x000f) | (6 + 1));
							regs[(mem8 >> 3) & 7] = segment_translation(mem8);
							goto EXEC_LOOP_END;
						case 0x8e: //MOV Ew Sw Move
							mem8 = phys_mem8[physmem8_ptr++];
							reg_idx1 = (mem8 >> 3) & 7;
							if (reg_idx1 >= 6 || reg_idx1 == 1)
								abort(6);
							if ((mem8 >> 6) == 3)
							{
								x = regs[mem8 & 7] & 0xffff;
							}
							else
							{
								mem8_loc = segment_translation(mem8);
								x = ld_16bits_mem8_read();
							}
							set_segment_register(reg_idx1, (int) x);
							goto EXEC_LOOP_END;
						case 0x98: //CBW AL AX Convert Byte to Word
							regs[0] = (regs[0] << 16) >> 16;
							goto EXEC_LOOP_END;
						case 0xa0: //MOV Ob AL Move byte at (seg:offset) to AL
							mem8_loc = segmented_mem8_loc_for_MOV();
							x = ld_8bits_mem8_read();
							regs[0] = (uint) ((regs[0] & -256) | x);
							goto EXEC_LOOP_END;
						case 0xa1: //MOV Ovqp rAX Move dword at (seg:offset) to EAX
							mem8_loc = segmented_mem8_loc_for_MOV();
							x = ld_32bits_mem8_read();
							regs[0] = x;
							goto EXEC_LOOP_END;
						case 0xa2: //MOV AL Ob Move AL to (seg:offset)
							mem8_loc = segmented_mem8_loc_for_MOV();
							st8_mem8_write(regs[0]);
							goto EXEC_LOOP_END;
						case 0xa3: //MOV rAX Ovqp Move EAX to (seg:offset)
							mem8_loc = segmented_mem8_loc_for_MOV();
							st32_mem8_write(regs[0]);
							goto EXEC_LOOP_END;
						case 0xab: //STOS AX ES:[DI] Store String
							stringOp_STOSD();
							goto EXEC_LOOP_END;
						case 0xb8: //MOV Ivqp Zvqp Move
						case 0xb9:
						case 0xba:
						case 0xbb:
						case 0xbc:
						case 0xbd:
						case 0xbe:
						case 0xbf:
							{
								x = (uint)(phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
											(phys_mem8[physmem8_ptr + 3] << 24));
								physmem8_ptr += 4;
							}
							regs[OPbyte & 7] = x;
							goto EXEC_LOOP_END;
						case 0xc1: //ROL Ib Evqp Rotate
							mem8 = phys_mem8[physmem8_ptr++];
							conditional_var = (mem8 >> 3) & 7;
							if ((mem8 >> 6) == 3)
							{
								y = phys_mem8[physmem8_ptr++];
								reg_idx0 = mem8 & 7;
								regs[reg_idx0] = shift32(conditional_var, regs[reg_idx0], (int) y);
							}
							else
							{
								mem8_loc = segment_translation(mem8);
								y = phys_mem8[physmem8_ptr++];
								x = ld_32bits_mem8_write();
								x = shift32(conditional_var, x, (int) y);
								st32_mem8_write(x);
							}
							goto EXEC_LOOP_END;
						case 0xc3: //RETN SS:[rSP]  Return from procedure
							if (FS_usage_flag)
							{
								mem8_loc = regs[4];
								x = ld_32bits_mem8_read();
								regs[4] = (regs[4] + 4) >> 0;
							}
							else
							{
								x = pop_dword_from_stack_read();
								pop_dword_from_stack_incr_ptr();
							}
							eip = x;
							physmem8_ptr = initial_mem_ptr = 0;
							goto EXEC_LOOP_END;
						case 0xc6: //MOV Ib Eb Move
							mem8 = phys_mem8[physmem8_ptr++];
							if ((mem8 >> 6) == 3)
							{
								x = phys_mem8[physmem8_ptr++];
								set_word_in_register(mem8 & 7, x);
							}
							else
							{
								mem8_loc = segment_translation(mem8);
								x = phys_mem8[physmem8_ptr++];
								st8_mem8_write(x);
							}
							goto EXEC_LOOP_END;
						case 0xc7: //MOV Ivds Evqp Move
							mem8 = phys_mem8[physmem8_ptr++];
							if ((mem8 >> 6) == 3)
							{
								{
									x =
										(uint)(phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
												(phys_mem8[physmem8_ptr + 3] << 24));
									physmem8_ptr += 4;
								}
								regs[mem8 & 7] = x;
							}
							else
							{
								mem8_loc = segment_translation(mem8);
								{
									x =
										(uint)(phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
												(phys_mem8[physmem8_ptr + 3] << 24));
									physmem8_ptr += 4;
								}
								st32_mem8_write(x);
							}
							goto EXEC_LOOP_END;
						case 0xd0: //ROL 1 Eb Rotate
							mem8 = phys_mem8[physmem8_ptr++];
							conditional_var = (mem8 >> 3) & 7;
							if ((mem8 >> 6) == 3)
							{
								reg_idx0 = mem8 & 7;
								set_word_in_register(reg_idx0, shift8(conditional_var, (regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1)), 1));
							}
							else
							{
								mem8_loc = segment_translation(mem8);
								x = ld_8bits_mem8_write();
								x = shift8(conditional_var, x, 1);
								st8_mem8_write(x);
							}
							goto EXEC_LOOP_END;
						case 0xd1: //ROL 1 Evqp Rotate
							mem8 = phys_mem8[physmem8_ptr++];
							conditional_var = (mem8 >> 3) & 7;
							if ((mem8 >> 6) == 3)
							{
								reg_idx0 = mem8 & 7;
								regs[reg_idx0] = shift32(conditional_var, regs[reg_idx0], 1);
							}
							else
							{
								mem8_loc = segment_translation(mem8);
								x = ld_32bits_mem8_write();
								x = shift32(conditional_var, x, 1);
								st32_mem8_write(x);
							}
							goto EXEC_LOOP_END;
						case 0xe0: //LOOPNZ Jbs eCX Decrement count; Jump short if count!=0 and ZF=0
						case 0xe1: //LOOPZ Jbs eCX Decrement count; Jump short if count!=0 and ZF=1
						case 0xe2: //LOOP Jbs eCX Decrement count; Jump short if count!=0
							x = (uint)((phys_mem8[physmem8_ptr++] << 24) >> 24);
							if ((CS_flags & 0x0080) != 0)
								conditional_var = 0xffff;
							else
								conditional_var = -1;
							y = (uint)((regs[1] - 1) & conditional_var);
							regs[1] = (uint)((regs[1] & ~conditional_var) | y);
							OPbyte &= 3;
							if (OPbyte == 0)
								z = _dst != 0;
							else if (OPbyte == 1)
								z = (_dst == 0);
							else
								z = true;
							if (y != 0 && z)
							{
								if ((CS_flags & 0x0100) != 0)
								{
									eip = (eip + physmem8_ptr - initial_mem_ptr + x) & 0xffff;
									physmem8_ptr = initial_mem_ptr = 0;
								}
								else
								{
									physmem8_ptr = (physmem8_ptr + x) >> 0;
								}
							}
							goto EXEC_LOOP_END;
						case 0xe8: //CALL Jvds SS:[rSP] Call Procedure
							{
								x = (uint)(phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
											(phys_mem8[physmem8_ptr + 3] << 24));
								physmem8_ptr += 4;
							}
							y = (eip + physmem8_ptr - initial_mem_ptr);
							if (FS_usage_flag)
							{
								mem8_loc = (regs[4] - 4) >> 0;
								st32_mem8_write(y);
								regs[4] = mem8_loc;
							}
							else
							{
								push_dword_to_stack(y);
							}
							physmem8_ptr = (physmem8_ptr + x) >> 0;
							goto EXEC_LOOP_END;
						case 0xea: //JMPF Ap  Jump
							if ((((CS_flags >> 8) & 1) ^ 1) != 0)
							{
								{
									x = (uint) (phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
									            (phys_mem8[physmem8_ptr + 3] << 24));
									physmem8_ptr += 4;
								}
							}
							else
							{
								x = (uint) ld16_mem8_direct();
							}
							y = (uint) ld16_mem8_direct();
							op_JMPF(y, x);
							goto EXEC_LOOP_END;
						case 0xeb: //JMP Jbs  Jump
							x = (uint)((phys_mem8[physmem8_ptr++] << 24) >> 24);
							physmem8_ptr = (physmem8_ptr + x) >> 0;
							goto EXEC_LOOP_END;
						case 0xee: //OUT AL DX Output to Port
							iopl = (cpu.eflags >> 12) & 3;
							if (cpu.cpl > iopl)
								abort(13);
							cpu.st8_port(regs[2] & 0xffff, (byte) regs[0]);
						{
							if (cpu.hard_irq != 0 && (cpu.eflags & 0x00000200) != 0)
								goto OUTER_LOOP_END;
						}
							goto EXEC_LOOP_END;

						case 0xf3://REPZ  eCX Repeat String Operation Prefix
							if (CS_flags == init_CS_flags)
								operation_size_function(eip_offset, OPbyte);
							CS_flags |= 0x0010;
							OPbyte = phys_mem8[physmem8_ptr++];
							OPbyte |= (CS_flags & 0x0100);
							break;

						/*
						TWO BYTE CODE INSTRUCTIONS BEGIN WITH 0F :  0F xx
						=====================================================================================================
						*/
						case 0x0f:
							OPbyte = phys_mem8[physmem8_ptr++];
							switch (OPbyte)
							{
								case 0x01: //SGDT GDTR Ms Store Global Descriptor Table Register
									mem8 = phys_mem8[physmem8_ptr++];
									conditional_var = (mem8 >> 3) & 7;
									switch (conditional_var)
									{
										case 2:
										case 3:
											if ((mem8 >> 6) == 3)
												abort(6);
											if (cpl != 0)
												abort(13);
											mem8_loc = segment_translation(mem8);
											x = ld_16bits_mem8_read();
											mem8_loc += 2;
											y = ld_32bits_mem8_read();
											if (conditional_var == 2)
											{
												gdt.@base = y;
												gdt.limit = (int) x;
											}
											else
											{
												idt.@base = y;
												idt.limit = (int) x;
											}
											break;
										case 7:
											if (cpl != 0)
												abort(13);
											if ((mem8 >> 6) == 3)
												abort(6);
											mem8_loc = segment_translation(mem8);
											cpu.tlb_flush_page(mem8_loc & -4096);
											break;
										default:
											abort(6);
											break;
									}
									goto EXEC_LOOP_END;

								case 0xb6: //MOVZX Eb Gvqp Move with Zero-Extend
									mem8 = phys_mem8[physmem8_ptr++];
									reg_idx1 = (mem8 >> 3) & 7;
									if ((mem8 >> 6) == 3)
									{
										reg_idx0 = mem8 & 7;
										x = (regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1)) & 0xff;
									}
									else
									{
										mem8_loc = segment_translation(mem8);
										x = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
											? __ld_8bits_mem8_read()
											: phys_mem8[mem8_loc ^ last_tlb_val]);
									}
									regs[reg_idx1] = x;
									goto EXEC_LOOP_END;
								case 0xbe: //MOVSX Eb Gvqp Move with Sign-Extension
									mem8 = phys_mem8[physmem8_ptr++];
									reg_idx1 = (mem8 >> 3) & 7;
									if ((mem8 >> 6) == 3)
									{
										reg_idx0 = mem8 & 7;
										x = (regs[reg_idx0 & 3] >> ((reg_idx0 & 4) << 1));
									}
									else
									{
										mem8_loc = segment_translation(mem8);
										x = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
											? __ld_8bits_mem8_read()
											: phys_mem8[mem8_loc ^ last_tlb_val]);
									}
									regs[reg_idx1] = (((x) << 24) >> 24);
									goto EXEC_LOOP_END;

								default:
									throw new NotImplementedException(string.Format("OPbyte 0x0f 0x{0:X} not implemented", OPbyte));
							}
							break;
							/*
							 *  16bit Compatibility Mode Operator Routines
							    ==========================================================================================
							    0x1XX  corresponds to the 16-bit compat operator corresponding to the usual 0xXX
							 */
						default:
							switch (OPbyte)
							{
								case 0x190: //XCHG  Zvqp Exchange Register/Memory with Register
									goto EXEC_LOOP_END;
								case 0x1c7: //MOV Ivds Evqp Move
									mem8 = phys_mem8[physmem8_ptr++];
									if ((mem8 >> 6) == 3)
									{
										x = (uint)ld16_mem8_direct();
										set_lower_word_in_register(mem8 & 7, x);
									}
									else
									{
										mem8_loc = segment_translation(mem8);
										x = (uint)ld16_mem8_direct();
										st16_mem8_write(x);
									}
									goto EXEC_LOOP_END;
								default:
									throw new NotImplementedException(string.Format("OPbyte 0x{0:X} not implemented", OPbyte));
							}
					}
				}

			EXEC_LOOP_END:
				{
				}
			} while (--cycles_left != 0); //End Giant Core DO WHILE Execution Loop
			OUTER_LOOP_END:
			{
			}

			#endregion
			cycle_count += (N_cycles - cycles_left);
			this.eip = eip + physmem8_ptr - initial_mem_ptr;
			cc_src = (int) _src;
			cc_dst = (int) _dst;
			cc_op = _op;
			cc_op2 = _op2;
			cc_dst2 = _dst2;
			return exit_code;
		}

		#region Helpers

		private void stringOp_STOSD()
		{
			int Xf;
			if ((CS_flags & 0x0080) != 0)
				Xf = 0xffff;
			else
				Xf = -1;
			var Yf = regs[7];
			mem8_loc = (uint) ((Yf & Xf) + cpu.segs[0].@base) >> 0;
			if ((CS_flags & (0x0010 | 0x0020)) != 0)
			{
				var ag = regs[1];
				if ((ag & Xf) == 0)
					return;
				if (Xf == -1 && cpu.df == 1 && (mem8_loc & 3) == 0)
				{
					int i;
					var len = ag >> 0;
					var l = (4096 - (mem8_loc & 0xfff)) >> 2;
					if (len > l)
						len = l;
					var vg = do_tlb_lookup(regs[7], 1);
					var x = regs[0];
					vg >>= 2;
					for (i = 0; i < len; i++)
						phys_mem32[vg + i] = (int) x;
					var wg = len << 2;
					regs[7] = (Yf + wg) >> 0;
					regs[1] = ag = (ag - len) >> 0;
					if (ag != 0)
						physmem8_ptr = (uint) initial_mem_ptr;
				}
				else
				{
					st32_mem8_write(regs[0]);
					regs[7] = (uint) ((Yf & ~Xf) | ((Yf + (cpu.df << 2)) & Xf));
					regs[1] = ag = (uint) ((ag & ~Xf) | ((ag - 1) & Xf));
					if ((ag & Xf) != 0)
						physmem8_ptr = (uint) initial_mem_ptr;
				}
			}
			else
			{
				st32_mem8_write(regs[0]);
				regs[7] = (uint) ((Yf & ~Xf) | ((Yf + (cpu.df << 2)) & Xf));
			}
		}

		private int do_tlb_lookup(uint mem8_loc, int ud)
		{
			int tlb_lookup;
			if (ud != 0)
			{
				tlb_lookup = _tlb_write_[mem8_loc >> 12];
			}
			else
			{
				tlb_lookup = _tlb_read_[mem8_loc >> 12];
			}
			if (tlb_lookup == -1)
			{
				do_tlb_set_page(mem8_loc, ud != 0, cpu.cpl == 3);
				if (ud != 0)
				{
					tlb_lookup = _tlb_write_[mem8_loc >> 12];
				}
				else
				{
					tlb_lookup = _tlb_read_[mem8_loc >> 12];
				}
			}
			return (int) (tlb_lookup ^ mem8_loc);
		}

		private void set_segment_register(int register, int selector)
		{
			selector &= 0xffff;
			if ((cpu.cr0 & (1 << 0)) == 0)
			{
				//CR0.PE (0 == real mode)
				var descriptor_table = cpu.segs[register];
				descriptor_table.selector = selector;
				descriptor_table.@base = (uint) (selector << 4);
			}
			else if ((cpu.eflags & 0x00020000) != 0)
			{
				//EFLAGS.VM (1 == v86 mode)
				init_segment_vars_with_selector(register, selector);
			}
			else
			{
				//protected mode
				set_protected_mode_segment_register(register, selector);
			}
		}

		private void set_protected_mode_segment_register(int register, int selector)
		{
			int selector_index;
			var cpl_var = cpu.cpl;
			if ((selector & 0xfffc) == 0)
			{
				//null selector
				if (register == 2) //(SS == null) => #GP(0)
					abort_with_error_code(13, 0);
				set_segment_vars(register, selector, 0, 0, 0);
			}
			else
			{
				Segment descriptor_table;
				if ((selector & 0x4) != 0)
					descriptor_table = cpu.ldt;
				else
					descriptor_table = cpu.gdt;
				selector_index = selector & ~7;
				if ((selector_index + 7) > descriptor_table.limit)
					abort_with_error_code(13, selector & 0xfffc);
				mem8_loc = (uint) ((descriptor_table.@base + selector_index) & -1);
				var descriptor_low4bytes = ld32_mem8_kernel_read();
				mem8_loc += 4;
				var descriptor_high4bytes = ld32_mem8_kernel_read();
				if ((descriptor_high4bytes & (1 << 12)) == 0)
					abort_with_error_code(13, selector & 0xfffc);
				var rpl = selector & 3;
				var dpl = (descriptor_high4bytes >> 13) & 3;
				if (register == 2)
				{
					if ((descriptor_high4bytes & (1 << 11)) != 0 || (descriptor_high4bytes & (1 << 9)) == 0)
						abort_with_error_code(13, selector & 0xfffc);
					if (rpl != cpl_var || dpl != cpl_var)
						abort_with_error_code(13, selector & 0xfffc);
				}
				else
				{
					if ((descriptor_high4bytes & ((1 << 11) | (1 << 9))) == (1 << 11))
						abort_with_error_code(13, selector & 0xfffc);
					if ((descriptor_high4bytes & (1 << 11)) == 0 || (descriptor_high4bytes & (1 << 10)) == 0)
					{
						if (dpl < cpl_var || dpl < rpl)
							abort_with_error_code(13, selector & 0xfffc);
					}
				}
				if ((descriptor_high4bytes & (1 << 15)) == 0)
				{
					if (register == 2)
						abort_with_error_code(12, selector & 0xfffc);
					else
						abort_with_error_code(11, selector & 0xfffc);
				}
				if ((descriptor_high4bytes & (1 << 8)) == 0)
				{
					descriptor_high4bytes |= (1 << 8);
					st32_mem8_kernel_write(descriptor_high4bytes);
				}
				set_segment_vars(register, selector, (uint) calculate_descriptor_base(descriptor_low4bytes, descriptor_high4bytes),
					calculate_descriptor_limit(descriptor_low4bytes, descriptor_high4bytes), descriptor_high4bytes);
			}
		}

		private void st32_mem8_kernel_write(int x)
		{
			var tlb_lookup = tlb_write_kernel[mem8_loc >> 12];
			if (((tlb_lookup | mem8_loc) & 3) != 0)
			{
				__st32_mem8_kernel_write(x);
			}
			else
			{
				phys_mem32[(mem8_loc ^ tlb_lookup) >> 2] = x;
			}
		}

		private void __st32_mem8_kernel_write(object o)
		{
			throw new NotImplementedException();
		}

		private void init_segment_vars_with_selector(int register, int selector)
		{
			throw new NotImplementedException();
		}

		private void op_DAS()
		{
			var flag_bits = get_conditional_flags();
			var Ef = flag_bits & 0x0001;
			var Bf = flag_bits & 0x0010;
			var wf = regs[0] & 0xff;
			flag_bits = 0;
			var Gf = wf;
			if (((wf & 0x0f) > 9) || Bf != 0)
			{
				flag_bits |= 0x0010;
				if (wf < 6 || Ef != 0)
					flag_bits |= 0x0001;
				wf = (wf - 6) & 0xff;
			}
			if ((Gf > 0x99) || Ef != 0)
			{
				wf = (wf - 0x60) & 0xff;
				flag_bits |= 0x0001;
			}
			regs[0] = (uint) ((regs[0] & ~0xff) | wf);
			flag_bits = (uint) (flag_bits | (wf == 0 ? 1 : 0) << 6);
			flag_bits = (uint) (flag_bits | parity_LUT[wf] << 2);
			flag_bits |= (wf & 0x80);
			_src = flag_bits;
			_dst = ((_src >> 6) & 1) ^ 1;
			_op = 24;
		}

		private void stringOp_OUTSD()
		{
			int Xf;
			int x;
			var iopl = (cpu.eflags >> 12) & 3;
			if (cpu.cpl > iopl)
				abort(13);
			if ((CS_flags & 0x0080) != 0)
				Xf = 0xffff;
			else
				Xf = -1;
			var Sb = CS_flags & 0x000f;
			if (Sb == 0)
				Sb = 3;
			else
				Sb--;
			var cg = regs[6];
			var Zf = regs[2] & 0xffff;
			if ((CS_flags & (0x0010 | 0x0020)) != 0)
			{
				var ag = regs[1];
				if ((ag & Xf) == 0)
					return;
				mem8_loc = (uint) (((cg & Xf) + cpu.segs[Sb].@base) >> 0);
				x = (int) ld_32bits_mem8_read();
				cpu.st32_port(Zf, (uint) x);
				regs[6] = (uint) ((cg & ~Xf) | ((cg + (cpu.df << 2)) & Xf));
				regs[1] = ag = (uint) ((ag & ~Xf) | ((ag - 1) & Xf));
				if ((ag & Xf) != 0)
					physmem8_ptr = (uint) initial_mem_ptr;
			}
			else
			{
				mem8_loc = (uint) (((cg & Xf) + cpu.segs[Sb].@base) >> 0);
				x = (int) ld_32bits_mem8_read();
				cpu.st32_port(Zf, (uint) x);
				regs[6] = (uint) ((cg & ~Xf) | ((cg + (cpu.df << 2)) & Xf));
			}
		}

		private void stringOp_INSB()
		{
			int Xf;
			int x;
			var iopl = (cpu.eflags >> 12) & 3;
			if (cpu.cpl > iopl)
				abort(13);
			if ((CS_flags & 0x0080) != 0)
				Xf = 0xffff;
			else
				Xf = -1;
			var Yf = regs[7];
			var Zf = regs[2] & 0xffff;
			if ((CS_flags & (0x0010 | 0x0020)) != 0)
			{
				var ag = regs[1];
				if ((ag & Xf) == 0)
					return;
				x = cpu.ld8_port(Zf);
				mem8_loc = (uint) (((Yf & Xf) + cpu.segs[0].@base) >> 0);
				st8_mem8_write((uint) x);
				regs[7] = (uint) ((Yf & ~Xf) | ((Yf + (cpu.df << 0)) & Xf));
				regs[1] = ag = (uint) ((ag & ~Xf) | ((ag - 1) & Xf));
				if ((ag & Xf) != 0)
					physmem8_ptr = (uint) initial_mem_ptr;
			}
			else
			{
				x = cpu.ld8_port(Zf);
				mem8_loc = (uint) (((Yf & Xf) + cpu.segs[0].@base) >> 0);
				st8_mem8_write((uint) x);
				regs[7] = (uint) ((Yf & ~Xf) | ((Yf + (cpu.df << 0)) & Xf));
			}
		}

		private bool check_less_than()
		{
			throw new NotImplementedException();
		}

		private bool check_below_or_equal()
		{
			bool result;
			switch (_op)
			{
				case 6:
					result = ((_dst + _src) & 0xff) <= (_src & 0xff);
					break;
				case 7:
					result = ((_dst + _src) & 0xffff) <= (_src & 0xffff);
					break;
				case 8:
					result = ((_dst + _src) >> 0) <= (_src >> 0);
					break;
				case 24:
					result = (_src & (0x0040 | 0x0001)) != 0;
					break;
				default:
					result = check_carry() != 0 || (_dst == 0);
					break;
			}
			return result;
		}

		private void op_ARPL()
		{
			int mem8;
			int x;
			int y;
			int reg_idx0 = 0;
			if ((cpu.cr0 & (1 << 0)) == 0 || (cpu.eflags & 0x00020000) != 0)
				abort(6);
			mem8 = phys_mem8[physmem8_ptr++];
			if ((mem8 >> 6) == 3)
			{
				reg_idx0 = mem8 & 7;
				x = (int) (regs[reg_idx0] & 0xffff);
			}
			else
			{
				mem8_loc = segment_translation(mem8);
				x = ld_16bits_mem8_write();
			}
			y = (int) regs[(mem8 >> 3) & 7];
			_src = get_conditional_flags();
			if ((x & 3) < (y & 3))
			{
				x = (x & ~3) | (y & 3);
				if ((mem8 >> 6) == 3)
				{
					set_lower_word_in_register(reg_idx0, (uint) x);
				}
				else
				{
					st16_mem8_write((uint) x);
				}
				_src |= 0x0040;
			}
			else
			{
				_src = (uint) (_src & ~0x0040);
			}
			_dst = ((_src >> 6) & 1) ^ 1;
			_op = 24;
		}

		private uint get_conditional_flags()
		{
			return (uint) ((check_carry() << 0) | (check_parity() << 2) | ((_dst == 0) ? 1 : 0 << 6) | ((_op == 24 ? (int)((_src >> 7) & 1) : ((int)_dst < 0 ? 1 : 0)) << 7) | (check_overflow() << 11) | check_adjust_flag());
		}

		private uint check_adjust_flag()
		{
			uint Yb;
			uint result;
			switch (_op)
			{
				case 0:
				case 1:
				case 2:
					Yb = (_dst - _src) >> 0;
					result = (_dst ^ Yb ^ _src) & 0x10;
					break;
				case 3:
				case 4:
				case 5:
					Yb = (_dst - _src - 1) >> 0;
					result = (_dst ^ Yb ^ _src) & 0x10;
					break;
				case 6:
				case 7:
				case 8:
					Yb = (_dst + _src) >> 0;
					result = (_dst ^ Yb ^ _src) & 0x10;
					break;
				case 9:
				case 10:
				case 11:
					Yb = (_dst + _src + 1) >> 0;
					result = (_dst ^ Yb ^ _src) & 0x10;
					break;
				case 12:
				case 13:
				case 14:
					result = 0;
					break;
				case 15:
				case 18:
				case 16:
				case 19:
				case 17:
				case 20:
				case 21:
				case 22:
				case 23:
					result = 0;
					break;
				case 24:
					result = _src & 0x10;
					break;
				case 25:
				case 26:
				case 27:
					result = (_dst ^ (_dst - 1)) & 0x10;
					break;
				case 28:
				case 29:
				case 30:
					result = (_dst ^ (_dst + 1)) & 0x10;
					break;
				default:
					throw new Exception("AF: unsupported cc_op=" + _op);
			}
			return result;
		}

		private long check_parity()
		{
			if (_op == 24)
			{
				return (_src >> 2) & 1;
			}
			else
			{
				return parity_LUT[_dst & 0xff];
			}
		}

		private int ld_16bits_mem8_write()
		{
			int tlb_lookup;
			return (((tlb_lookup = _tlb_write_[mem8_loc >> 12]) | mem8_loc) & 1) != 0 ? __ld_16bits_mem8_write() : (int)phys_mem16[(mem8_loc ^ tlb_lookup) >> 1];
		}

		private int __ld_16bits_mem8_write()
		{
			throw new NotImplementedException();
		}

		private void op_JMPF(uint selector, uint Le)
		{
			if ((cpu.cr0 & (1 << 0)) == 0 || (cpu.eflags & 0x00020000) != 0)
			{
				do_JMPF_virtual_mode(selector, Le);
			}
			else
			{
				do_JMPF(selector, Le);
			}
		}

		private void do_JMPF(uint selector, uint Le)
		{
			uint Ne;
			uint ie;
			int descriptor_low4bytes;
			int descriptor_high4bytes;
			int cpl_var;
			int dpl;
			uint rpl;
			int limit;
			int[] e;
			if ((selector & 0xfffc) == 0)
				abort_with_error_code(13, 0);
			e = load_from_descriptor_table(selector);
			if (e == null)
				abort_with_error_code(13, (int) (selector & 0xfffc));
			descriptor_low4bytes = e[0];
			descriptor_high4bytes = e[1];
			cpl_var = cpu.cpl;
			if ((descriptor_high4bytes & (1 << 12)) != 0)
			{
				if ((descriptor_high4bytes & (1 << 11)) == 0)
					abort_with_error_code(13, (int) (selector & 0xfffc));
				dpl = (descriptor_high4bytes >> 13) & 3;
				if ((descriptor_high4bytes & (1 << 10)) != 0)
				{
					if (dpl > cpl_var)
						abort_with_error_code(13, (int) (selector & 0xfffc));
				}
				else
				{
					rpl = selector & 3;
					if (rpl > cpl_var)
						abort_with_error_code(13, (int) (selector & 0xfffc));
					if (dpl != cpl_var)
						abort_with_error_code(13, (int) (selector & 0xfffc));
				}
				if ((descriptor_high4bytes & (1 << 15)) == 0)
					abort_with_error_code(11, (int) (selector & 0xfffc));
				limit = calculate_descriptor_limit(descriptor_low4bytes, descriptor_high4bytes);
				if ((Le >> 0) > (uint)(limit >> 0))
					abort_with_error_code(13, (int) (selector & 0xfffc));
				set_segment_vars(1, (int) ((selector & 0xfffc) | cpl_var), (uint) calculate_descriptor_base(descriptor_low4bytes, descriptor_high4bytes), (int) limit, (int) descriptor_high4bytes);
				eip = Le;
				physmem8_ptr = 0;
				initial_mem_ptr = 0;
			}
			else
			{
				cpu_abort("unsupported jump to call or task gate");
			}
		}

		private void cpu_abort(string unsupportedJumpToCallOrTaskGate)
		{
			throw new NotImplementedException();
		}

		private void set_segment_vars(int ee, int selector, uint @base, int limit, int flags)
		{
			cpu.segs[ee] = new Segment {selector = selector, @base = @base, limit = limit, flags = flags};
			init_segment_local_vars();
		}

		private int calculate_descriptor_base(int descriptor_low4bytes, int descriptor_high4bytes)
		{
			return (int) ((((descriptor_low4bytes >> 16) | ((descriptor_high4bytes & 0xff) << 16) | (descriptor_high4bytes & 0xff000000))) & -1);
		}

		private int calculate_descriptor_limit(int descriptor_low4bytes, int descriptor_high4bytes)
		{
			var limit = (descriptor_low4bytes & 0xffff) | (descriptor_high4bytes & 0x000f0000);
			if ((descriptor_high4bytes & (1 << 23)) != 0)
				limit = (limit << 12) | 0xfff;
			return limit;
		}

		private int[] load_from_descriptor_table(uint selector)
		{
			Segment descriptor_table;
			if ((selector & 0x4) != 0)
				descriptor_table = cpu.ldt;
			else
				descriptor_table = cpu.gdt;
			var Rb = selector & ~7;
			if ((Rb + 7) > descriptor_table.limit)
				return null;
			mem8_loc = (uint) (descriptor_table.@base + Rb);
			int descriptor_low4bytes = ld32_mem8_kernel_read();
			mem8_loc += 4;
			int descriptor_high4bytes = ld32_mem8_kernel_read();
			return new[] {descriptor_low4bytes, descriptor_high4bytes};
		}

		private int ld32_mem8_kernel_read()
		{
			uint tlb_lookup;
			return (((tlb_lookup = (uint) tlb_read_kernel[mem8_loc >> 12]) | mem8_loc) & 3) != 0
				? __ld32_mem8_kernel_read()
				: phys_mem32[(mem8_loc ^ tlb_lookup) >> 2];
		}

		private int __ld32_mem8_kernel_read()
		{
			throw new NotImplementedException();
		}

		private void do_JMPF_virtual_mode(uint selector, uint le)
		{
			throw new NotImplementedException();
		}

		private void tlb_flush_page(long l)
		{
			throw new NotImplementedException();
		}

		private uint ld_16bits_mem8_read()
		{
			uint last_tlb_val;
			return ((((last_tlb_val = (uint) _tlb_read_[mem8_loc >> 12]) | mem8_loc) & 1) != 0
				? __ld_16bits_mem8_read()
				: phys_mem16[(mem8_loc ^ last_tlb_val) >> 1]);
		}

		private uint __ld_16bits_mem8_read()
		{
			var x = ld_8bits_mem8_read();
			mem8_loc++;
			x |= ld_8bits_mem8_read() << 8;
			mem8_loc--;
			return x;
		}

		private bool check_less_or_equal()
		{
			bool result;
			switch (_op)
			{
				case 6:
					result = ((_dst + _src) << 24) <= (_src << 24);
					break;
				case 7:
					result = ((_dst + _src) << 16) <= (_src << 16);
					break;
				case 8:
					result = ((_dst + _src) >> 0) <= _src;
					break;
				case 12:
				case 25:
				case 28:
				case 13:
				case 26:
				case 29:
				case 14:
				case 27:
				case 30:
					result = _dst <= 0;
					break;
				case 24:
					result = ((((_src >> 7) ^ (_src >> 11)) | (_src >> 6)) & 1) != 0;
					break;
				default:
					result = ((_op == 24 ? (_src >> 7) & 1 : (uint)((int)_dst < 0 ? 1 : 0)) ^ check_overflow()) != 0 | (_dst == 0);
					break;
			}
			return result;
		}

		private int check_overflow()
		{
			long result;
			uint Yb;
			switch (_op)
			{
				case 0:
					Yb = (_dst - _src) >> 0;
					result = (((Yb ^ _src ^ -1) & (Yb ^ _dst)) >> 7) & 1;
					break;
				case 1:
					Yb = (_dst - _src) >> 0;
					result = (((Yb ^ _src ^ -1) & (Yb ^ _dst)) >> 15) & 1;
					break;
				case 2:
					Yb = (_dst - _src) >> 0;
					result = (((Yb ^ _src ^ -1) & (Yb ^ _dst)) >> 31) & 1;
					break;
				case 3:
					Yb = (_dst - _src - 1) >> 0;
					result = (((Yb ^ _src ^ -1) & (Yb ^ _dst)) >> 7) & 1;
					break;
				case 4:
					Yb = (_dst - _src - 1) >> 0;
					result = (((Yb ^ _src ^ -1) & (Yb ^ _dst)) >> 15) & 1;
					break;
				case 5:
					Yb = (_dst - _src - 1) >> 0;
					result = (((Yb ^ _src ^ -1) & (Yb ^ _dst)) >> 31) & 1;
					break;
				case 6:
					Yb = (_dst + _src) >> 0;
					result = (((Yb ^ _src) & (Yb ^ _dst)) >> 7) & 1;
					break;
				case 7:
					Yb = (_dst + _src) >> 0;
					result = (((Yb ^ _src) & (Yb ^ _dst)) >> 15) & 1;
					break;
				case 8:
					Yb = (_dst + _src) >> 0;
					result = (((Yb ^ _src) & (Yb ^ _dst)) >> 31) & 1;
					break;
				case 9:
					Yb = (_dst + _src + 1) >> 0;
					result = (((Yb ^ _src) & (Yb ^ _dst)) >> 7) & 1;
					break;
				case 10:
					Yb = (_dst + _src + 1) >> 0;
					result = (((Yb ^ _src) & (Yb ^ _dst)) >> 15) & 1;
					break;
				case 11:
					Yb = (_dst + _src + 1) >> 0;
					result = (((Yb ^ _src) & (Yb ^ _dst)) >> 31) & 1;
					break;
				case 12:
				case 13:
				case 14:
					result = 0;
					break;
				case 15:
				case 18:
					result = ((_src ^ _dst) >> 7) & 1;
					break;
				case 16:
				case 19:
					result = ((_src ^ _dst) >> 15) & 1;
					break;
				case 17:
				case 20:
					result = ((_src ^ _dst) >> 31) & 1;
					break;
				case 21:
				case 22:
				case 23:
					result = _src != 0 ? 1 : 0;
					break;
				case 24:
					result = (_src >> 11) & 1;
					break;
				case 25:
					result = (_dst & 0xff) == 0x80 ? 1 : 0;
					break;
				case 26:
					result = (_dst & 0xffff) == 0x8000 ? 1 : 0;
					break;
				case 27:
					result = (_dst == -2147483648) ? 1 : 0;
					break;
				case 28:
					result = (_dst & 0xff) == 0x7f ? 1 : 0;
					break;
				case 29:
					result = (_dst & 0xffff) == 0x7fff ? 1 : 0;
					break;
				case 30:
					result = _dst == 0x7fffffff ? 1 : 0;
					break;
				default:
					throw new Exception("JO: unsupported cc_op=" + _op);
			}
			return (int) result;
		}

		private void st16_mem8_write(uint x)
		{
			{
				int last_tlb_val = _tlb_write_[mem8_loc >> 12];
				if (((last_tlb_val | mem8_loc) & 1) != 0)
				{
					__st16_mem8_write(x);
				}
				else
				{
					phys_mem16[(mem8_loc ^ last_tlb_val) >> 1] = (ushort) x;
				}
			}
		}

		private void __st16_mem8_write(uint u)
		{
			throw new NotImplementedException();
		}

		private void set_lower_word_in_register(int i, uint u)
		{
			throw new NotImplementedException();
		}

		private uint segmented_mem8_loc_for_MOV()
		{
			uint mem8_loc;
			int Sb;
			if ((CS_flags & 0x0080) != 0)
			{
				mem8_loc = (uint) ld16_mem8_direct();
			}
			else
			{
				{
					mem8_loc = (uint) (phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
					                   (phys_mem8[physmem8_ptr + 3] << 24));
					physmem8_ptr += 4;
				}
			}
			Sb = (int) (CS_flags & 0x000f);
			if (Sb == 0)
				Sb = 3;
			else
				Sb--;
			mem8_loc = (mem8_loc + cpu.segs[Sb].@base) >>0;
			return mem8_loc;
		}

		/// <summary>
		/// used only for errors 0, 5, 6, 7, 13
		/// </summary>
		/// <param name="i"></param>
		private void abort(int intno)
		{
			abort_with_error_code(intno, 0);
		}

		private uint shift32(int conditional_var, uint Yb, int Zb)
		{
			uint kc;
			uint ac;
			switch (conditional_var)
			{
				case 0:
					Zb &= 0x1f;
					if (Zb != 0)
					{
						kc = Yb;
						Yb = (Yb << Zb) | (Yb >> (32 - Zb));
						_src = conditional_flags_for_rot_shift_ops();
						_src |= (Yb & 0x0001) | (((kc ^ Yb) >> 20) & 0x0800);
						_dst = ((_src >> 6) & 1) ^ 1;
						_op = 24;
					}
					break;
				case 1:
					Zb &= 0x1f;
					if (Zb != 0)
					{
						kc = Yb;
						Yb = (Yb >> Zb) | (Yb << (32 - Zb));
						_src = conditional_flags_for_rot_shift_ops();
						_src |= ((Yb >> 31) & 0x0001) | (((kc ^ Yb) >> 20) & 0x0800);
						_dst = ((_src >> 6) & 1) ^ 1;
						_op = 24;
					}
					break;
				case 2:
					Zb &= 0x1f;
					if (Zb != 0)
					{
						kc = Yb;
						ac = check_carry();
						Yb = (uint)((Yb << Zb) | (ac << (Zb - 1)));
						if (Zb > 1)
							Yb |= (uint)kc >> (33 - Zb);
						_src = conditional_flags_for_rot_shift_ops();
						_src |= (uint)((((kc ^ Yb) >> 20) & 0x0800) | ((kc >> (32 - Zb)) & 0x0001));
						_dst = ((_src >> 6) & 1) ^ 1;
						_op = 24;
					}
					break;
				case 3:
					Zb &= 0x1f;
					if (Zb != 0)
					{
						kc = Yb;
						ac = check_carry();
						Yb = (uint)((Yb >> Zb) | (ac << (32 - Zb)));
						if (Zb > 1)
							Yb |= (uint)kc << (33 - Zb);
						_src = conditional_flags_for_rot_shift_ops();
						_src |= (uint)((((kc ^ Yb) >> 20) & 0x0800) | ((kc >> (Zb - 1)) & 0x0001));
						_dst = ((_src >> 6) & 1) ^ 1;
						_op = 24;
					}
					break;
				case 4:
				case 6:
					Zb &= 0x1f;
					if (Zb != 0)
					{
						_src = Yb << (Zb - 1);
						_dst = Yb = Yb << Zb;
						_op = 17;
					}
					break;
				case 5:
					Zb &= 0x1f;
					if (Zb != 0)
					{
						_src = Yb >> (Zb - 1);
						_dst = Yb = Yb >> Zb;
						_op = 20;
					}
					break;
				case 7:
					Zb &= 0x1f;
					if (Zb != 0)
					{
						_src = Yb >> (Zb - 1);
						_dst = Yb = Yb >> Zb;
						_op = 20;
					}
					break;
				default:
					throw new Exception("unsupported shift32=" + conditional_var);
			}
			return Yb;
		}

		private uint shift8(int conditional_var, uint Yb, int Zb)
		{
			uint kc;
			uint ac;
			switch (conditional_var)
			{
				case 0:
					if ((Zb & 0x1f) != 0)
					{
						Zb &= 0x7;
						Yb &= 0xff;
						kc = Yb;
						Yb = (Yb << Zb) | (Yb >> (8 - Zb));
						_src = conditional_flags_for_rot_shift_ops();
						_src |= (Yb & 0x0001) | (((kc ^ Yb) << 4) & 0x0800);
						_dst = ((_src >> 6) & 1) ^ 1;
						_op = 24;
					}
					break;
				case 1:
					if ((Zb & 0x1f) != 0)
					{
						Zb &= 0x7;
						Yb &= 0xff;
						kc = Yb;
						Yb = (Yb >> Zb) | (Yb << (8 - Zb));
						_src = conditional_flags_for_rot_shift_ops();
						_src |= ((Yb >> 7) & 0x0001) | (((kc ^ Yb) << 4) & 0x0800);
						_dst = ((_src >> 6) & 1) ^ 1;
						_op = 24;
					}
					break;
				case 2:
					Zb = shift8_LUT[Zb & 0x1f];
					if (Zb != 0)
					{
						Yb &= 0xff;
						kc = Yb;
						ac = check_carry();
						Yb = (uint)((Yb << Zb) | (ac << (Zb - 1)));
						if (Zb > 1)
							Yb |= (uint)kc >> (9 - Zb);
						_src = conditional_flags_for_rot_shift_ops();
						_src |= (uint)((((kc ^ Yb) << 4) & 0x0800) | ((kc >> (8 - Zb)) & 0x0001));
						_dst = ((_src >> 6) & 1) ^ 1;
						_op = 24;
					}
					break;
				case 3:
					Zb = shift8_LUT[Zb & 0x1f];
					if (Zb != 0)
					{
						Yb &= 0xff;
						kc = Yb;
						ac = check_carry();
						Yb = (uint)((Yb >> Zb) | (ac << (8 - Zb)));
						if (Zb > 1)
							Yb |= (uint)kc << (9 - Zb);
						_src = conditional_flags_for_rot_shift_ops();
						_src |= (uint)((((kc ^ Yb) << 4) & 0x0800) | ((kc >> (Zb - 1)) & 0x0001));
						_dst = ((_src >> 6) & 1) ^ 1;
						_op = 24;
					}
					break;
				case 4:
				case 6:
					Zb &= 0x1f;
					if (Zb != 0)
					{
						_src = Yb << (Zb - 1);
						_dst = Yb = (((Yb << Zb) << 24) >> 24);
						_op = 15;
					}
					break;
				case 5:
					Zb &= 0x1f;
					if (Zb != 0)
					{
						Yb &= 0xff;
						_src = Yb >> (Zb - 1);
						_dst = Yb = (((Yb >> Zb) << 24) >> 24);
						_op = 18;
					}
					break;
				case 7:
					Zb &= 0x1f;
					if (Zb != 0)
					{
						Yb = (Yb << 24) >> 24;
						_src = Yb >> (Zb - 1);
						_dst = Yb = (((Yb >> Zb) << 24) >> 24);
						_op = 18;
					}
					break;
				default:
					throw new Exception("unsupported shift8=" + conditional_var);
			}
			return Yb;
		}

		private uint conditional_flags_for_rot_shift_ops()
		{
			throw new NotImplementedException();
		}

		private void pop_dword_from_stack_incr_ptr()
		{
			regs[4] = (uint) ((regs[4] & ~SS_mask) | ((regs[4] + 4) & SS_mask));
		}

		private uint pop_dword_from_stack_read()
		{
			mem8_loc = (uint) (((regs[4] & SS_mask) + SS_base) >> 0);
			return ld_32bits_mem8_read();
		}

		private uint __ld_32bits_mem8_read()
		{
			throw new NotImplementedException();
		}

		private uint ld_32bits_mem8_write()
		{
			var x = ld_8bits_mem8_write();
			mem8_loc++;
			x |= ld_8bits_mem8_write() << 8;
			mem8_loc++;
			x |= ld_8bits_mem8_write() << 16;
			mem8_loc++;
			x |= ld_8bits_mem8_write() << 24;
			mem8_loc -= 3;
			return x;
		}

		private uint do_32bit_math(int conditional_var, uint Yb, uint Zb)
		{
			uint ac = 0;
			switch (conditional_var)
			{
				case 0:
					_src = Zb;
					Yb = (Yb + Zb) >> 0;
					_dst = Yb;
					_op = 2;
					break;
				case 1:
					Yb = Yb | Zb;
					_dst = Yb;
					_op = 14;
					break;
				case 2:
					ac = check_carry();
					_src = Zb;
					Yb = (Yb + Zb + ac) >> 0;
					_dst = Yb;
					_op = ac != 0 ? 5 : 2;
					break;
				case 3:
					ac = check_carry();
					_src = Zb;
					Yb = (Yb - Zb - ac) >> 0;
					_dst = Yb;
					_op = ac != 0 ? 11 : 8;
					break;
				case 4:
					Yb = Yb & Zb;
					_dst = Yb;
					_op = 14;
					break;
				case 5:
					_src = Zb;
					Yb = (Yb - Zb) >> 0;
					_dst = Yb;
					_op = 8;
					break;
				case 6:
					Yb = Yb ^ Zb;
					_dst = Yb;
					_op = 14;
					break;
				case 7:
					_src = Zb;
					_dst = (Yb - Zb) >> 0;
					_op = 8;
					break;
				default:
					throw new Exception("arith" + ac + ": invalid op");
			}
			return Yb;
		}

		/// <summary>
		/// Status bits and Flags Routines
		/// </summary>
		/// <returns></returns>
		private uint check_carry()
		{
			int Yb;
			bool result;
			int current_op;
			int relevant_dst;
			if (_op >= 25)
			{
				current_op = _op2;
				relevant_dst = _dst2;
			}
			else
			{
				current_op = _op;
				relevant_dst = (int) _dst;
			}
			switch (current_op)
			{
				case 0:
					result = (relevant_dst & 0xff) < (_src & 0xff);
					break;
				case 1:
					result = (relevant_dst & 0xffff) < (_src & 0xffff);
					break;
				case 2:
					result = (relevant_dst >> 0) < (_src >> 0);
					break;
				case 3:
					result = (relevant_dst & 0xff) <= (_src & 0xff);
					break;
				case 4:
					result = (relevant_dst & 0xffff) <= (_src & 0xffff);
					break;
				case 5:
					result = (relevant_dst >> 0) <= (_src >> 0);
					break;
				case 6:
					result = ((relevant_dst + _src) & 0xff) < (_src & 0xff);
					break;
				case 7:
					result = ((relevant_dst + _src) & 0xffff) < (_src & 0xffff);
					break;
				case 8:
					result = ((relevant_dst + _src) >> 0) < (_src >> 0);
					break;
				case 9:
					Yb = (int) ((relevant_dst + _src + 1) & 0xff);
					result = Yb <= (_src & 0xff);
					break;
				case 10:
					Yb = (int) ((relevant_dst + _src + 1) & 0xffff);
					result = Yb <= (_src & 0xffff);
					break;
				case 11:
					Yb = (int) ((relevant_dst + _src + 1) >> 0);
					result = Yb <= (_src >> 0);
					break;
				case 12:
				case 13:
				case 14:
					result = false;
					break;
				case 15:
					result = ((_src >> 7) & 1) != 0;
					break;
				case 16:
					result = ((_src >> 15) & 1) != 0;
					break;
				case 17:
					result = ((_src >> 31) & 1) != 0;
					break;
				case 18:
				case 19:
				case 20:
					result = (_src & 1) != 0;
					break;
				case 21:
				case 22:
				case 23:
					result = _src != 0;
					break;
				case 24:
					result = (_src & 1) != 0;
					break;
				default:
					throw new Exception("GET_CARRY: unsupported cc_op=" + _op);
			}
			return (uint) (result ? 1 : 0);
		}

		private uint ld_32bits_mem8_read()
		{
			int last_tlb_val;
			return ((((last_tlb_val = _tlb_read_[mem8_loc >> 12]) | mem8_loc) & 3) != 0 ? __ld_32bits_mem8_read() : (uint) phys_mem32[(mem8_loc ^ last_tlb_val) >> 2]);
		}

		private void st32_mem8_write(uint x)
		{
			int last_tlb_val;
			{
				last_tlb_val = _tlb_write_[mem8_loc >> 12];
				if (((last_tlb_val | mem8_loc) & 3) != 0)
				{
					__st32_mem8_write(x);
				}
				else
				{
					phys_mem32[(mem8_loc ^ last_tlb_val) >> 2] = (int)x;
				}
			}
		}

		private void push_dword_to_stack(uint x)
		{
			var wd = regs[4] - 4;
			mem8_loc = (uint) (((wd & SS_mask) + SS_base) >> 0);
			st32_mem8_write(x);
			regs[4] = (uint) ((regs[4] & ~SS_mask) | ((wd) & SS_mask));
		}

		private void __st32_mem8_write(uint x)
		{
			st8_mem8_write(x);
			mem8_loc++;
			st8_mem8_write(x >> 8);
			mem8_loc++;
			st8_mem8_write(x >> 16);
			mem8_loc++;
			st8_mem8_write(x >> 24);
			mem8_loc -= 3;
		}

		private uint ld_8bits_mem8_read()
		{
			var last_tlb_val = _tlb_read_[mem8_loc >> 12];
			return ((last_tlb_val == -1) ? __ld_8bits_mem8_read() : phys_mem8[mem8_loc ^ last_tlb_val]);
		}

		private void st8_mem8_write(uint x)
		{
			int last_tlb_val;
			{
				last_tlb_val = _tlb_write_[mem8_loc >> 12];
				if (last_tlb_val == -1)
				{
					__st8_mem8_write(x);
				}
				else
				{
					phys_mem8[mem8_loc ^ last_tlb_val] = (byte)x;
				}
			}
		}

		private void __st8_mem8_write(uint x)
		{
			do_tlb_set_page(mem8_loc, true, cpu.cpl == 3);
			var tlb_lookup = _tlb_write_[mem8_loc >> 12] ^ mem8_loc;
			phys_mem8[tlb_lookup] = (byte)x;
		}

		private uint ld_8bits_mem8_write()
		{
			var tlb_lookup = _tlb_write_[mem8_loc >> 12];
			return ((tlb_lookup) == -1) ? __ld_8bits_mem8_write() : phys_mem8[mem8_loc ^ tlb_lookup];
		}

		private uint __ld_8bits_mem8_write()
		{
			do_tlb_set_page(mem8_loc, true, cpu.cpl == 3);
			var tlb_lookup = _tlb_write_[mem8_loc >> 12] ^ mem8_loc;
			return phys_mem8[tlb_lookup];
		}

		private uint physmem8_ptr;
		private object eip_tlb_val;
		private uint initial_mem_ptr;
		private uint eip_offset;
		private int[] _tlb_read_;
		private int _op2;
		private int _dst2;
		/* Parity Check by LUT:
			static const bool ParityTable256[256] = {
			#   define P2(n) n, n^1, n^1, n
			#   define P4(n) P2(n), P2(n^1), P2(n^1), P2(n)
			#   define P6(n) P4(n), P4(n^1), P4(n^1), P4(n)
			P6(0), P6(1), P6(1), P6(0) };
			unsigned char b;  // byte value to compute the parity of
			bool parity = ParityTable256[b];
			// OR, for 32-bit words:    unsigned int v; v ^= v >> 16; v ^= v >> 8; bool parity = ParityTable256[v & 0xff];
			// Variation:               unsigned char * p = (unsigned char *) &v; parity = ParityTable256[p[0] ^ p[1] ^ p[2] ^ p[3]];
		*/
		private int[] parity_LUT = {1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1};

		/// <summary>
		/// segment translation routine (I believe):
		/// Translates Logical Memory Address to Linear Memory Address
		/// </summary>
		private uint segment_translation(int mem8)
		{
			int @base;
			uint mem8_loc = 0;
			int Qb;
			int Rb;
			int Sb;
			object Tb;
			if (FS_usage_flag && (CS_flags & (0x000f | 0x0080)) == 0)
			{
				switch ((mem8 & 7) | ((mem8 >> 3) & 0x18))
				{
					case 0x04:
						Qb = phys_mem8[physmem8_ptr++];
						@base = Qb & 7;
						if (@base == 5)
						{
							{
								mem8_loc = (uint)(phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
												   (phys_mem8[physmem8_ptr + 3] << 24));
								physmem8_ptr += 4;
							}
						}
						else
						{
							mem8_loc = regs[@base];
						}
						Rb = (Qb >> 3) & 7;
						if (Rb != 4)
						{
							mem8_loc = ((mem8_loc + (regs[Rb] << (Qb >> 6))) >> 0);
						}
						break;
					case 0x0c:
						Qb = phys_mem8[physmem8_ptr++];
						mem8_loc = (uint)((phys_mem8[physmem8_ptr++] << 24) >> 24);
						@base = Qb & 7;
						mem8_loc = ((mem8_loc + regs[@base]) >> 0);
						Rb = (Qb >> 3) & 7;
						if (Rb != 4)
						{
							mem8_loc = ((mem8_loc + (regs[Rb] << (Qb >> 6))) >> 0);
						}
						break;
					case 0x14:
						Qb = phys_mem8[physmem8_ptr++];
						{
							mem8_loc = (uint)(phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
											   (phys_mem8[physmem8_ptr + 3] << 24));
							physmem8_ptr += 4;
						}
						@base = Qb & 7;
						mem8_loc = ((mem8_loc + regs[@base]) >> 0);
						Rb = (Qb >> 3) & 7;
						if (Rb != 4)
						{
							mem8_loc = ((mem8_loc + (regs[Rb] << (Qb >> 6))) >> 0);
						}
						break;
					case 0x05:
						{
							mem8_loc = (uint)(phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
											   (phys_mem8[physmem8_ptr + 3] << 24));
							physmem8_ptr += 4;
						}
						break;
					case 0x00:
					case 0x01:
					case 0x02:
					case 0x03:
					case 0x06:
					case 0x07:
						@base = mem8 & 7;
						mem8_loc = regs[@base];
						break;
					case 0x08:
					case 0x09:
					case 0x0a:
					case 0x0b:
					case 0x0d:
					case 0x0e:
					case 0x0f:
						mem8_loc = (uint)((phys_mem8[physmem8_ptr++] << 24) >> 24);
						@base = mem8 & 7;
						mem8_loc = ((mem8_loc + regs[@base]) >> 0);
						break;
					case 0x10:
					case 0x11:
					case 0x12:
					case 0x13:
					case 0x15:
					case 0x16:
					case 0x17:
					default:
						{
							mem8_loc = (uint)(phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
											   (phys_mem8[physmem8_ptr + 3] << 24));
							physmem8_ptr += 4;
						}
						@base = mem8 & 7;
						mem8_loc = ((mem8_loc + regs[@base]) >> 0);
						break;
				}
				return (uint)mem8_loc;
			}
			else if ((CS_flags & 0x0080) != 0)
			{
				if ((mem8 & 0xc7) == 0x06)
				{
					mem8_loc = (uint)ld16_mem8_direct();
					Tb = 3;
				}
				else
				{
					switch (mem8 >> 6)
					{
						case 0:
							mem8_loc = 0;
							break;
						case 1:
							mem8_loc = (uint)((phys_mem8[physmem8_ptr++] << 24) >> 24);
							break;
						default:
							mem8_loc = (uint)ld16_mem8_direct();
							break;
					}
					switch (mem8 & 7)
					{
						case 0:
							mem8_loc = ((mem8_loc + regs[3] + regs[6]) & 0xffff);
							Tb = 3;
							break;
						case 1:
							mem8_loc = ((mem8_loc + regs[3] + regs[7]) & 0xffff);
							Tb = 3;
							break;
						case 2:
							mem8_loc = ((mem8_loc + regs[5] + regs[6]) & 0xffff);
							Tb = 2;
							break;
						case 3:
							mem8_loc = ((mem8_loc + regs[5] + regs[7]) & 0xffff);
							Tb = 2;
							break;
						case 4:
							mem8_loc = ((mem8_loc + regs[6]) & 0xffff);
							Tb = 3;
							break;
						case 5:
							mem8_loc = ((mem8_loc + regs[7]) & 0xffff);
							Tb = 3;
							break;
						case 6:
							mem8_loc = ((mem8_loc + regs[5]) & 0xffff);
							Tb = 2;
							break;
						case 7:
						default:
							mem8_loc = ((mem8_loc + regs[3]) & 0xffff);
							Tb = 3;
							break;
					}
				}
				Sb = (int)(CS_flags & 0x000f);
				if (Sb == 0)
				{
					Sb = (int)Tb;
				}
				else
				{
					Sb--;
				}
				mem8_loc = ((mem8_loc + cpu.segs[Sb].@base) >> 0);
				return mem8_loc;
			}
			else
			{
				switch ((mem8 & 7) | ((mem8 >> 3) & 0x18))
				{
					case 0x04:
						Qb = phys_mem8[physmem8_ptr++];
						@base = Qb & 7;
						if (@base == 5)
						{
							{
								mem8_loc = (uint)(phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
												   (phys_mem8[physmem8_ptr + 3] << 24));
								physmem8_ptr += 4;
							}
							@base = 0;
						}
						else
						{
							mem8_loc = regs[@base];
						}
						Rb = (Qb >> 3) & 7;
						if (Rb != 4)
						{
							mem8_loc = ((mem8_loc + (regs[Rb] << (Qb >> 6))) >> 0);
						}
						break;
					case 0x0c:
						Qb = phys_mem8[physmem8_ptr++];
						mem8_loc = (uint)((phys_mem8[physmem8_ptr++] << 24) >> 24);
						@base = Qb & 7;
						mem8_loc = ((mem8_loc + regs[@base]) >> 0);
						Rb = (Qb >> 3) & 7;
						if (Rb != 4)
						{
							mem8_loc = ((mem8_loc + (regs[Rb] << (Qb >> 6))) >> 0);
						}
						break;
					case 0x14:
						Qb = phys_mem8[physmem8_ptr++];
						{
							mem8_loc = (uint)(phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
											   (phys_mem8[physmem8_ptr + 3] << 24));
							physmem8_ptr += 4;
						}
						@base = Qb & 7;
						mem8_loc = ((mem8_loc + regs[@base]) >> 0);
						Rb = (Qb >> 3) & 7;
						if (Rb != 4)
						{
							mem8_loc = ((mem8_loc + (regs[Rb] << (Qb >> 6))) >> 0);
						}
						break;
					case 0x05:
						{
							mem8_loc = (uint)(phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
											   (phys_mem8[physmem8_ptr + 3] << 24));
							physmem8_ptr += 4;
						}
						@base = 0;
						break;
					case 0x00:
					case 0x01:
					case 0x02:
					case 0x03:
					case 0x06:
					case 0x07:
						@base = mem8 & 7;
						mem8_loc = regs[@base];
						break;
					case 0x08:
					case 0x09:
					case 0x0a:
					case 0x0b:
					case 0x0d:
					case 0x0e:
					case 0x0f:
						mem8_loc = (uint)((phys_mem8[physmem8_ptr++] << 24) >> 24);
						@base = mem8 & 7;
						mem8_loc = ((mem8_loc + regs[@base]) >> 0);
						break;
					case 0x10:
					case 0x11:
					case 0x12:
					case 0x13:
					case 0x15:
					case 0x16:
					case 0x17:
					default:
						{
							mem8_loc = (uint)(phys_mem8[physmem8_ptr] | (phys_mem8[physmem8_ptr + 1] << 8) | (phys_mem8[physmem8_ptr + 2] << 16) |
											   (phys_mem8[physmem8_ptr + 3] << 24));
							physmem8_ptr += 4;
						}
						@base = mem8 & 7;
						mem8_loc = ((mem8_loc + regs[@base]) >> 0);
						break;
				}
				Sb = (int)(CS_flags & 0x000f);
				if (Sb == 0)
				{
					if (@base == 4 || @base == 5)
						Sb = 2;
					else
						Sb = 3;
				}
				else
				{
					Sb--;
				}
				mem8_loc = ((mem8_loc + cpu.segs[Sb].@base) >> 0);
				return mem8_loc;
			}
		}

		private int ld16_mem8_direct()
		{
			int x;
			int y;
			x = phys_mem8[physmem8_ptr++];
			y = phys_mem8[physmem8_ptr++];
			return x | (y << 8);
		}

		/// <summary>
		/// Register Manipulation
		/// </summary>
		private void set_word_in_register(int reg_idx1, uint x)
		{
			/*
				if arg[0] is = 1xx  then set register xx's upper two bytes to two bytes in arg[1]
				if arg[0] is = 0xx  then set register xx's lower two bytes to two bytes in arg[1]
			*/
			if ((reg_idx1 & 4) != 0)
				regs[reg_idx1 & 3] = (uint)((regs[reg_idx1 & 3] & -65281) | ((x & 0xff) << 8));
			else
				regs[reg_idx1 & 3] = (uint)((regs[reg_idx1 & 3] & -256) | (x & 0xff));
		}

		private uint do_8bit_math(int conditional_var, uint Yb, uint Zb)
		{
			uint ac = 0;
			switch (conditional_var)
			{
				case 0:
					_src = Zb;
					Yb = (((Yb + Zb) << 24) >> 24);
					_dst = Yb;
					_op = 0;
					break;
				case 1:
					Yb = (((Yb | Zb) << 24) >> 24);
					_dst = Yb;
					_op = 12;
					break;
				case 2:
					ac = check_carry();
					_src = Zb;
					Yb = (((Yb + Zb + ac) << 24) >> 24);
					_dst = Yb;
					_op = ac != 0 ? 3 : 0;
					break;
				case 3:
					ac = check_carry();
					_src = Zb;
					Yb = (((Yb - Zb - ac) << 24) >> 24);
					_dst = Yb;
					_op = ac != 0 ? 9 : 6;
					break;
				case 4:
					Yb = (((Yb & Zb) << 24) >> 24);
					_dst = Yb;
					_op = 12;
					break;
				case 5:
					_src = Zb;
					Yb = (((Yb - Zb) << 24) >> 24);
					_dst = Yb;
					_op = 6;
					break;
				case 6:
					Yb = (((Yb ^ Zb) << 24) >> 24);
					_dst = Yb;
					_op = 12;
					break;
				case 7:
					_src = Zb;
					_dst = (((Yb - Zb) << 24) >> 24);
					_op = 6;
					break;
				default:
					throw new Exception("arith" + ac + ": invalid op");
			}
			return Yb;
		}

		/*
		 Paged Memory Mode Access Routines
		 ================================================================================
		*/
		/* Storing XOR values as small lookup table is software equivalent of a Translation Lookaside Buffer (TLB) */

		private byte __ld_8bits_mem8_read()
		{
			do_tlb_set_page(mem8_loc, false, cpu.cpl == 3);
			var tlb_lookup = (uint)(_tlb_read_[mem8_loc >> 12] ^ mem8_loc);
			return phys_mem8[tlb_lookup];
		}

		private uint operation_size_function(uint eipOffset, uint OPbyte)
		{
			int @base;int conditional_var;int stride;
        var n = 1;
        var CS_flags = init_CS_flags;
        if ((CS_flags & 0x0100) != 0)//are we in 16bit compatibility mode?
            stride = 2;
        else
            stride = 4;
        EXEC_LOOP: for (; ; )
        {
	        int mem8 = 0;
	        uint local_OPbyte_var = 0;
	        switch (OPbyte) {
                case 0x66://   Operand-size override prefix
                    if ((init_CS_flags & 0x0100) != 0) {
                        stride = 4;
                        CS_flags = (uint) (CS_flags & ~0x0100);
                    } else {
                        stride = 2;
                        CS_flags |= 0x0100;
                    }
					{
						if ((n + 1) > 15)
							abort(6);
						mem8_loc = (uint)((eip_offset + (n++)) >> 0);
						OPbyte = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1) ? __ld_8bits_mem8_read() : phys_mem8[mem8_loc ^ last_tlb_val]);
					}
					break;
                case 0xf0://LOCK   Assert LOCK# Signal Prefix
                case 0xf2://REPNZ  eCX Repeat String Operation Prefix
                case 0xf3://REPZ  eCX Repeat String Operation Prefix
                case 0x26://ES ES  ES segment override prefix
                case 0x2e://CS CS  CS segment override prefix
                case 0x36://SS SS  SS segment override prefix
                case 0x3e://DS DS  DS segment override prefix
                case 0x64://FS FS  FS segment override prefix
                case 0x65://GS GS  GS segment override prefix
                    {
                        if ((n + 1) > 15)
                            abort(6);
                        mem8_loc = (uint) ((eip_offset + (n++)) >> 0);
                        OPbyte = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1) ? __ld_8bits_mem8_read() : phys_mem8[mem8_loc ^ last_tlb_val]);
                    }
                    break;
                case 0x67://   Address-size override prefix
                    if ((init_CS_flags & 0x0080) != 0) {
                        CS_flags = (uint) (CS_flags & ~0x0080);
                    } else {
                        CS_flags |= 0x0080;
                    }
                    {
                        if ((n + 1) > 15)
                            abort(6);
                        mem8_loc = (uint) ((eip_offset + (n++)) >> 0);
                        OPbyte = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1) ? __ld_8bits_mem8_read() : phys_mem8[mem8_loc ^ last_tlb_val]);
                    }
                    break;
                case 0x91:
                case 0x92:
                case 0x93:
                case 0x94:
                case 0x95:
                case 0x96:
                case 0x97:
                case 0x40://INC  Zv Increment by 1
                case 0x41://REX.B   Extension of r/m field, base field, or opcode reg field
                case 0x42://REX.X   Extension of SIB index field
                case 0x43://REX.XB   REX.X and REX.B combination
                case 0x44://REX.R   Extension of ModR/M reg field
                case 0x45://REX.RB   REX.R and REX.B combination
                case 0x46://REX.RX   REX.R and REX.X combination
                case 0x47://REX.RXB   REX.R, REX.X and REX.B combination
                case 0x48://DEC  Zv Decrement by 1
                case 0x49://REX.WB   REX.W and REX.B combination
                case 0x4a://REX.WX   REX.W and REX.X combination
                case 0x4b://REX.WXB   REX.W, REX.X and REX.B combination
                case 0x4c://REX.WR   REX.W and REX.R combination
                case 0x4d://REX.WRB   REX.W, REX.R and REX.B combination
                case 0x4e://REX.WRX   REX.W, REX.R and REX.X combination
                case 0x4f://REX.WRXB   REX.W, REX.R, REX.X and REX.B combination
                case 0x50://PUSH Zv SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
                case 0x51:
                case 0x52:
                case 0x53:
                case 0x54:
                case 0x55:
                case 0x56:
                case 0x57:
                case 0x58://POP SS:[rSP] Zv Pop a Value from the Stack
                case 0x59:
                case 0x5a:
                case 0x5b:
                case 0x5c:
                case 0x5d:
                case 0x5e:
                case 0x5f:
                case 0x98://CBW AL AX Convert Byte to Word
                case 0x99://CWD AX DX Convert Word to Doubleword
                case 0xc9://LEAVE SS:[rSP] eBP High Level Procedure Exit
                case 0x9c://PUSHF Flags SS:[rSP] Push FLAGS Register onto the Stack
                case 0x9d://POPF SS:[rSP] Flags Pop Stack into FLAGS Register
                case 0x06://PUSH ES SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
                case 0x0e://PUSH CS SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
                case 0x16://PUSH SS SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
                case 0x1e://PUSH DS SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
                case 0x07://POP SS:[rSP] ES Pop a Value from the Stack
                case 0x17://POP SS:[rSP] SS Pop a Value from the Stack
                case 0x1f://POP SS:[rSP] DS Pop a Value from the Stack
                case 0xc3://RETN SS:[rSP]  Return from procedure
                case 0xcb://RETF SS:[rSP]  Return from procedure
                case 0x90://XCHG  Zvqp Exchange Register/Memory with Register
                case 0xcc://INT 3 SS:[rSP] Call to Interrupt Procedure
                case 0xce://INTO eFlags SS:[rSP] Call to Interrupt Procedure
                case 0xcf://IRET SS:[rSP] Flags Interrupt Return
                case 0xf5://CMC   Complement Carry Flag
                case 0xf8://CLC   Clear Carry Flag
                case 0xf9://STC   Set Carry Flag
                case 0xfc://CLD   Clear Direction Flag
                case 0xfd://STD   Set Direction Flag
                case 0xfa://CLI   Clear Interrupt Flag
                case 0xfb://STI   Set Interrupt Flag
                case 0x9e://SAHF AH  Store AH into Flags
                case 0x9f://LAHF  AH Load Status Flags into AH Register
                case 0xf4://HLT   Halt
                case 0xa4://MOVS (DS:)[rSI] (ES:)[rDI] Move Data from String to String
                case 0xa5://MOVS DS:[SI] ES:[DI] Move Data from String to String
                case 0xaa://STOS AL (ES:)[rDI] Store String
                case 0xab://STOS AX ES:[DI] Store String
                case 0xa6://CMPS (ES:)[rDI]  Compare String Operands
                case 0xa7://CMPS ES:[DI]  Compare String Operands
                case 0xac://LODS (DS:)[rSI] AL Load String
                case 0xad://LODS DS:[SI] AX Load String
                case 0xae://SCAS (ES:)[rDI]  Scan String
                case 0xaf://SCAS ES:[DI]  Scan String
                case 0x9b://FWAIT   Check pending unmasked floating-point exceptions
                case 0xec://IN DX AL Input from Port
                case 0xed://IN DX eAX Input from Port
                case 0xee://OUT AL DX Output to Port
                case 0xef://OUT eAX DX Output to Port
                case 0xd7://XLAT (DS:)[rBX+AL] AL Table Look-up Translation
                case 0x27://DAA  AL Decimal Adjust AL after Addition
                case 0x2f://DAS  AL Decimal Adjust AL after Subtraction
                case 0x37://AAA  AL ASCII Adjust After Addition
                case 0x3f://AAS  AL ASCII Adjust AL After Subtraction
                case 0x60://PUSHA AX SS:[rSP] Push All General-Purpose Registers
                case 0x61://POPA SS:[rSP] DI Pop All General-Purpose Registers
                case 0x6c://INS DX (ES:)[rDI] Input from Port to String
                case 0x6d://INS DX ES:[DI] Input from Port to String
                case 0x6e://OUTS (DS):[rSI] DX Output String to Port
                case 0x6f://OUTS DS:[SI] DX Output String to Port
                    goto EXEC_LOOP_EXIT;
                case 0xb0://MOV Ib Zb Move
                case 0xb1:
                case 0xb2:
                case 0xb3:
                case 0xb4:
                case 0xb5:
                case 0xb6:
                case 0xb7:
                case 0x04://ADD Ib AL Add
                case 0x0c://OR Ib AL Logical Inclusive OR
                case 0x14://ADC Ib AL Add with Carry
                case 0x1c://SBB Ib AL Integer Subtraction with Borrow
                case 0x24://AND Ib AL Logical AND
                case 0x2c://SUB Ib AL Subtract
                case 0x34://XOR Ib AL Logical Exclusive OR
                case 0x3c://CMP AL  Compare Two Operands
                case 0xa8://TEST AL  Logical Compare
                case 0x6a://PUSH Ibss SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
                case 0xeb://JMP Jbs  Jump
                case 0x70://JO Jbs  Jump short if overflow (OF=1)
                case 0x71://JNO Jbs  Jump short if not overflow (OF=0)
                case 0x72://JB Jbs  Jump short if below/not above or equal/carry (CF=1)
                case 0x73://JNB Jbs  Jump short if not below/above or equal/not carry (CF=0)
                case 0x76://JBE Jbs  Jump short if below or equal/not above (CF=1 AND ZF=1)
                case 0x77://JNBE Jbs  Jump short if not below or equal/above (CF=0 AND ZF=0)
                case 0x78://JS Jbs  Jump short if sign (SF=1)
                case 0x79://JNS Jbs  Jump short if not sign (SF=0)
                case 0x7a://JP Jbs  Jump short if parity/parity even (PF=1)
                case 0x7b://JNP Jbs  Jump short if not parity/parity odd
                case 0x7c://JL Jbs  Jump short if less/not greater (SF!=OF)
                case 0x7d://JNL Jbs  Jump short if not less/greater or equal (SF=OF)
                case 0x7e://JLE Jbs  Jump short if less or equal/not greater ((ZF=1) OR (SF!=OF))
                case 0x7f://JNLE Jbs  Jump short if not less nor equal/greater ((ZF=0) AND (SF=OF))
                case 0x74://JZ Jbs  Jump short if zero/equal (ZF=0)
                case 0x75://JNZ Jbs  Jump short if not zero/not equal (ZF=1)
                case 0xe0://LOOPNZ Jbs eCX Decrement count; Jump short if count!=0 and ZF=0
                case 0xe1://LOOPZ Jbs eCX Decrement count; Jump short if count!=0 and ZF=1
                case 0xe2://LOOP Jbs eCX Decrement count; Jump short if count!=0
                case 0xe3://JCXZ Jbs  Jump short if eCX register is 0
                case 0xcd://INT Ib SS:[rSP] Call to Interrupt Procedure
                case 0xe4://IN Ib AL Input from Port
                case 0xe5://IN Ib eAX Input from Port
                case 0xe6://OUT AL Ib Output to Port
                case 0xe7://OUT eAX Ib Output to Port
                case 0xd4://AAM  AL ASCII Adjust AX After Multiply
                case 0xd5://AAD  AL ASCII Adjust AX Before Division
                    n++;
                    if (n > 15)
                        abort(6);
                    goto EXEC_LOOP_EXIT;
                case 0xb8://MOV Ivqp Zvqp Move
                case 0xb9:
                case 0xba:
                case 0xbb:
                case 0xbc:
                case 0xbd:
                case 0xbe:
                case 0xbf:
                case 0x05://ADD Ivds rAX Add
                case 0x0d://OR Ivds rAX Logical Inclusive OR
                case 0x15://ADC Ivds rAX Add with Carry
                case 0x1d://SBB Ivds rAX Integer Subtraction with Borrow
                case 0x25://AND Ivds rAX Logical AND
                case 0x2d://SUB Ivds rAX Subtract
                case 0x35://XOR Ivds rAX Logical Exclusive OR
                case 0x3d://CMP rAX  Compare Two Operands
                case 0xa9://TEST rAX  Logical Compare
                case 0x68://PUSH Ivs SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
                case 0xe9://JMP Jvds  Jump
                case 0xe8://CALL Jvds SS:[rSP] Call Procedure
                    n += stride;
                    if (n > 15)
                        abort(6);
                    goto EXEC_LOOP_EXIT;
                case 0x88://MOV Gb Eb Move
                case 0x89://MOV Gvqp Evqp Move
                case 0x8a://MOV Eb Gb Move
                case 0x8b://MOV Evqp Gvqp Move
                case 0x86://XCHG  Gb Exchange Register/Memory with Register
                case 0x87://XCHG  Gvqp Exchange Register/Memory with Register
                case 0x8e://MOV Ew Sw Move
                case 0x8c://MOV Sw Mw Move
                case 0xc4://LES Mp ES Load Far Pointer
                case 0xc5://LDS Mp DS Load Far Pointer
                case 0x00://ADD Gb Eb Add
                case 0x08://OR Gb Eb Logical Inclusive OR
                case 0x10://ADC Gb Eb Add with Carry
                case 0x18://SBB Gb Eb Integer Subtraction with Borrow
                case 0x20://AND Gb Eb Logical AND
                case 0x28://SUB Gb Eb Subtract
                case 0x30://XOR Gb Eb Logical Exclusive OR
                case 0x38://CMP Eb  Compare Two Operands
                case 0x01://ADD Gvqp Evqp Add
                case 0x09://OR Gvqp Evqp Logical Inclusive OR
                case 0x11://ADC Gvqp Evqp Add with Carry
                case 0x19://SBB Gvqp Evqp Integer Subtraction with Borrow
                case 0x21://AND Gvqp Evqp Logical AND
                case 0x29://SUB Gvqp Evqp Subtract
                case 0x31://XOR Gvqp Evqp Logical Exclusive OR
                case 0x39://CMP Evqp  Compare Two Operands
                case 0x02://ADD Eb Gb Add
                case 0x0a://OR Eb Gb Logical Inclusive OR
                case 0x12://ADC Eb Gb Add with Carry
                case 0x1a://SBB Eb Gb Integer Subtraction with Borrow
                case 0x22://AND Eb Gb Logical AND
                case 0x2a://SUB Eb Gb Subtract
                case 0x32://XOR Eb Gb Logical Exclusive OR
                case 0x3a://CMP Gb  Compare Two Operands
                case 0x03://ADD Evqp Gvqp Add
                case 0x0b://OR Evqp Gvqp Logical Inclusive OR
                case 0x13://ADC Evqp Gvqp Add with Carry
                case 0x1b://SBB Evqp Gvqp Integer Subtraction with Borrow
                case 0x23://AND Evqp Gvqp Logical AND
                case 0x2b://SUB Evqp Gvqp Subtract
                case 0x33://XOR Evqp Gvqp Logical Exclusive OR
                case 0x3b://CMP Gvqp  Compare Two Operands
                case 0x84://TEST Eb  Logical Compare
                case 0x85://TEST Evqp  Logical Compare
                case 0xd0://ROL 1 Eb Rotate
                case 0xd1://ROL 1 Evqp Rotate
                case 0xd2://ROL CL Eb Rotate
                case 0xd3://ROL CL Evqp Rotate
                case 0x8f://POP SS:[rSP] Ev Pop a Value from the Stack
                case 0x8d://LEA M Gvqp Load Effective Address
                case 0xfe://INC  Eb Increment by 1
                case 0xff://INC  Evqp Increment by 1
                case 0xd8://FADD Msr ST Add
                case 0xd9://FLD ESsr ST Load Floating Point Value
                case 0xda://FIADD Mdi ST Add
                case 0xdb://FILD Mdi ST Load Integer
                case 0xdc://FADD Mdr ST Add
                case 0xdd://FLD Mdr ST Load Floating Point Value
                case 0xde://FIADD Mwi ST Add
                case 0xdf://FILD Mwi ST Load Integer
                case 0x62://BOUND Gv SS:[rSP] Check Array Index Against Bounds
                case 0x63://ARPL Ew  Adjust RPL Field of Segment Selector
                    {
                        {
                            if ((n + 1) > 15)
                                abort(6);
                            mem8_loc = (uint) ((eip_offset + (n++)) >> 0);
                            mem8 = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1) ? __ld_8bits_mem8_read() : phys_mem8[mem8_loc ^ last_tlb_val]);
                        }
                        if ((CS_flags & 0x0080) != 0) {
                            switch (mem8 >> 6) {
                                case 0:
                                    if ((mem8 & 7) == 6)
                                        n += 2;
                                    break;
                                case 1:
                                    n++;
                                    break;
                                default:
                                    n += 2;
                                    break;
                            }
                        } else {
                            switch ((mem8 & 7) | ((mem8 >> 3) & 0x18)) {
                                case 0x04:
                                    {
                                        if ((n + 1) > 15)
                                            abort(6);
                                        mem8_loc = (uint) ((eip_offset + (n++)) >> 0);
                                        local_OPbyte_var = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1) ? __ld_8bits_mem8_read() : phys_mem8[mem8_loc ^ last_tlb_val]);
                                    }
                                    if ((local_OPbyte_var & 7) == 5) {
                                        n += 4;
                                    }
                                    break;
                                case 0x0c:
                                    n += 2;
                                    break;
                                case 0x14:
                                    n += 5;
                                    break;
                                case 0x05:
                                    n += 4;
                                    break;
                                case 0x00:
                                case 0x01:
                                case 0x02:
                                case 0x03:
                                case 0x06:
                                case 0x07:
                                    break;
                                case 0x08:
                                case 0x09:
                                case 0x0a:
                                case 0x0b:
                                case 0x0d:
                                case 0x0e:
                                case 0x0f:
                                    n++;
                                    break;
                                case 0x10:
                                case 0x11:
                                case 0x12:
                                case 0x13:
                                case 0x15:
                                case 0x16:
                                case 0x17:
                                    n += 4;
                                    break;
                            }
                        }
                        if (n > 15)
                            abort(6);
                    }
                    goto EXEC_LOOP_EXIT;
                case 0xa0://MOV Ob AL Move
                case 0xa1://MOV Ovqp rAX Move
                case 0xa2://MOV AL Ob Move
                case 0xa3://MOV rAX Ovqp Move
                    if ((CS_flags & 0x0100) != 0)
                        n += 2;
                    else
                        n += 4;
                    if (n > 15)
                        abort(6);
                    goto EXEC_LOOP_EXIT;
                case 0xc6://MOV Ib Eb Move
                case 0x80://ADD Ib Eb Add
                case 0x82://ADD Ib Eb Add
                case 0x83://ADD Ibs Evqp Add
                case 0x6b://IMUL Evqp Gvqp Signed Multiply
                case 0xc0://ROL Ib Eb Rotate
                case 0xc1://ROL Ib Evqp Rotate
                    {
                        {
                            if ((n + 1) > 15)
                                abort(6);
                            mem8_loc = (uint) ((eip_offset + (n++)) >> 0);
                            mem8 = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1) ? __ld_8bits_mem8_read() : phys_mem8[mem8_loc ^ last_tlb_val]);
                        }
                        if ((CS_flags & 0x0080) != 0) {
                            switch (mem8 >> 6) {
                                case 0:
                                    if ((mem8 & 7) == 6)
                                        n += 2;
                                    break;
                                case 1:
                                    n++;
                                    break;
                                default:
                                    n += 2;
                                    break;
                            }
                        } else {
                            switch ((mem8 & 7) | ((mem8 >> 3) & 0x18)) {
                                case 0x04:
                                    {
                                        if ((n + 1) > 15)
                                            abort(6);
                                        mem8_loc = (uint) ((eip_offset + (n++)) >> 0);
                                        local_OPbyte_var = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1) ? __ld_8bits_mem8_read() : phys_mem8[mem8_loc ^ last_tlb_val]);
                                    }
                                    if ((local_OPbyte_var & 7) == 5) {
                                        n += 4;
                                    }
                                    break;
                                case 0x0c:
                                    n += 2;
                                    break;
                                case 0x14:
                                    n += 5;
                                    break;
                                case 0x05:
                                    n += 4;
                                    break;
                                case 0x00:
                                case 0x01:
                                case 0x02:
                                case 0x03:
                                case 0x06:
                                case 0x07:
                                    break;
                                case 0x08:
                                case 0x09:
                                case 0x0a:
                                case 0x0b:
                                case 0x0d:
                                case 0x0e:
                                case 0x0f:
                                    n++;
                                    break;
                                case 0x10:
                                case 0x11:
                                case 0x12:
                                case 0x13:
                                case 0x15:
                                case 0x16:
                                case 0x17:
                                    n += 4;
                                    break;
                            }
                        }
                        if (n > 15)
                            abort(6);
                    }
                    n++;
                    if (n > 15)
                        abort(6);
                    goto EXEC_LOOP_EXIT;
                case 0xc7://MOV Ivds Evqp Move
                case 0x81://ADD Ivds Evqp Add
                case 0x69://IMUL Evqp Gvqp Signed Multiply
                    {
                        {
                            if ((n + 1) > 15)
                                abort(6);
                            mem8_loc = (uint) ((eip_offset + (n++)) >> 0);
                            mem8 = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1) ? __ld_8bits_mem8_read() : phys_mem8[mem8_loc ^ last_tlb_val]);
                        }
                        if ((CS_flags & 0x0080) != 0) {
                            switch (mem8 >> 6) {
                                case 0:
                                    if ((mem8 & 7) == 6)
                                        n += 2;
                                    break;
                                case 1:
                                    n++;
                                    break;
                                default:
                                    n += 2;
                                    break;
                            }
                        } else {
                            switch ((mem8 & 7) | ((mem8 >> 3) & 0x18)) {
                                case 0x04:
                                    {
                                        if ((n + 1) > 15)
                                            abort(6);
                                        mem8_loc = (uint) ((eip_offset + (n++)) >> 0);
                                        local_OPbyte_var = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1) ? __ld_8bits_mem8_read() : phys_mem8[mem8_loc ^ last_tlb_val]);
                                    }
                                    if ((local_OPbyte_var & 7) == 5) {
                                        n += 4;
                                    }
                                    break;
                                case 0x0c:
                                    n += 2;
                                    break;
                                case 0x14:
                                    n += 5;
                                    break;
                                case 0x05:
                                    n += 4;
                                    break;
                                case 0x00:
                                case 0x01:
                                case 0x02:
                                case 0x03:
                                case 0x06:
                                case 0x07:
                                    break;
                                case 0x08:
                                case 0x09:
                                case 0x0a:
                                case 0x0b:
                                case 0x0d:
                                case 0x0e:
                                case 0x0f:
                                    n++;
                                    break;
                                case 0x10:
                                case 0x11:
                                case 0x12:
                                case 0x13:
                                case 0x15:
                                case 0x16:
                                case 0x17:
                                    n += 4;
                                    break;
                            }
                        }
                        if (n > 15)
                            abort(6);
                    }
                    n += stride;
                    if (n > 15)
                        abort(6);
                    goto EXEC_LOOP_EXIT;
                case 0xf6://TEST Eb  Logical Compare
                    {
                        {
                            if ((n + 1) > 15)
                                abort(6);
                            mem8_loc = (uint) ((eip_offset + (n++)) >> 0);
                            mem8 = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1) ? __ld_8bits_mem8_read() : phys_mem8[mem8_loc ^ last_tlb_val]);
                        }
                        if ((CS_flags & 0x0080) != 0) {
                            switch (mem8 >> 6) {
                                case 0:
                                    if ((mem8 & 7) == 6)
                                        n += 2;
                                    break;
                                case 1:
                                    n++;
                                    break;
                                default:
                                    n += 2;
                                    break;
                            }
                        } else {
                            switch ((mem8 & 7) | ((mem8 >> 3) & 0x18)) {
                                case 0x04:
                                    {
                                        if ((n + 1) > 15)
                                            abort(6);
                                        mem8_loc = (uint) ((eip_offset + (n++)) >> 0);
                                        local_OPbyte_var = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1) ? __ld_8bits_mem8_read() : phys_mem8[mem8_loc ^ last_tlb_val]);
                                    }
                                    if ((local_OPbyte_var & 7) == 5) {
                                        n += 4;
                                    }
                                    break;
                                case 0x0c:
                                    n += 2;
                                    break;
                                case 0x14:
                                    n += 5;
                                    break;
                                case 0x05:
                                    n += 4;
                                    break;
                                case 0x00:
                                case 0x01:
                                case 0x02:
                                case 0x03:
                                case 0x06:
                                case 0x07:
                                    break;
                                case 0x08:
                                case 0x09:
                                case 0x0a:
                                case 0x0b:
                                case 0x0d:
                                case 0x0e:
                                case 0x0f:
                                    n++;
                                    break;
                                case 0x10:
                                case 0x11:
                                case 0x12:
                                case 0x13:
                                case 0x15:
                                case 0x16:
                                case 0x17:
                                    n += 4;
                                    break;
                            }
                        }
                        if (n > 15)
                            abort(6);
                    }
                    conditional_var = (mem8 >> 3) & 7;
                    if (conditional_var == 0) {
                        n++;
                        if (n > 15)
                            abort(6);
                    }
                    goto EXEC_LOOP_EXIT;
                case 0xf7://TEST Evqp  Logical Compare
                    {
                        {
                            if ((n + 1) > 15)
                                abort(6);
                            mem8_loc = (uint) ((eip_offset + (n++)) >> 0);
                            mem8 = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1) ? __ld_8bits_mem8_read() : phys_mem8[mem8_loc ^ last_tlb_val]);
                        }
                        if ((CS_flags & 0x0080) != 0) {
                            switch (mem8 >> 6) {
                                case 0:
                                    if ((mem8 & 7) == 6)
                                        n += 2;
                                    break;
                                case 1:
                                    n++;
                                    break;
                                default:
                                    n += 2;
                                    break;
                            }
                        } else {
                            switch ((mem8 & 7) | ((mem8 >> 3) & 0x18)) {
                                case 0x04:
                                    {
                                        if ((n + 1) > 15)
                                            abort(6);
                                        mem8_loc = (uint) ((eip_offset + (n++)) >> 0);
                                        local_OPbyte_var = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1) ? __ld_8bits_mem8_read() : phys_mem8[mem8_loc ^ last_tlb_val]);
                                    }
                                    if ((local_OPbyte_var & 7) == 5) {
                                        n += 4;
                                    }
                                    break;
                                case 0x0c:
                                    n += 2;
                                    break;
                                case 0x14:
                                    n += 5;
                                    break;
                                case 0x05:
                                    n += 4;
                                    break;
                                case 0x00:
                                case 0x01:
                                case 0x02:
                                case 0x03:
                                case 0x06:
                                case 0x07:
                                    break;
                                case 0x08:
                                case 0x09:
                                case 0x0a:
                                case 0x0b:
                                case 0x0d:
                                case 0x0e:
                                case 0x0f:
                                    n++;
                                    break;
                                case 0x10:
                                case 0x11:
                                case 0x12:
                                case 0x13:
                                case 0x15:
                                case 0x16:
                                case 0x17:
                                    n += 4;
                                    break;
                            }
                        }
                        if (n > 15)
                            abort(6);
                    }
                    conditional_var = (mem8 >> 3) & 7;
                    if (conditional_var == 0) {
                        n += stride;
                        if (n > 15)
                            abort(6);
                    }
                    goto EXEC_LOOP_EXIT;
                case 0xea://JMPF Ap  Jump
                case 0x9a://CALLF Ap SS:[rSP] Call Procedure
                    n += 2 + stride;
                    if (n > 15)
                        abort(6);
                    goto EXEC_LOOP_EXIT;
                case 0xc2://RETN SS:[rSP]  Return from procedure
                case 0xca://RETF Iw  Return from procedure
                    n += 2;
                    if (n > 15)
                        abort(6);
                    goto EXEC_LOOP_EXIT;
                case 0xc8://ENTER Iw SS:[rSP] Make Stack Frame for Procedure Parameters
                    n += 3;
                    if (n > 15)
                        abort(6);
                    goto EXEC_LOOP_EXIT;
                case 0xd6://SALC   Undefined and Reserved; Does not Generate #UD
                case 0xf1://INT1   Undefined and Reserved; Does not Generate #UD
                default:
                    abort(6);
					if (operation_size_function_2_byte(stride, CS_flags, ref n, ref mem8, ref local_OPbyte_var))
						goto EXEC_LOOP_EXIT;
			        break;
                case 0x0f://two-op instruction prefix
					if (operation_size_function_2_byte(stride, CS_flags, ref n, ref mem8, ref local_OPbyte_var))
						goto EXEC_LOOP_EXIT;
			        break;
	        }
        }
			EXEC_LOOP_EXIT:
			{
			}

			return (uint) n;
		}

		private bool operation_size_function_2_byte(int stride, uint CS_flags, ref int n, ref int mem8, ref uint local_OPbyte_var)
		{
			uint OPbyte;
			{
				if ((n + 1) > 15)
					abort(6);
				mem8_loc = (uint) ((eip_offset + (n++)) >> 0);
				OPbyte = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
					? __ld_8bits_mem8_read()
					: phys_mem8[mem8_loc ^ last_tlb_val]);
			}
			switch (OPbyte)
			{
				case 0x06: //CLTS  CR0 Clear Task-Switched Flag in CR0
				case 0xa2: //CPUID  IA32_BIOS_SIGN_ID CPU Identification
				case 0x31: //RDTSC IA32_TIME_STAMP_COUNTER EAX Read Time-Stamp Counter
				case 0xa0: //PUSH FS SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
				case 0xa8: //PUSH GS SS:[rSP] Push Word, Doubleword or Quadword Onto the Stack
				case 0xa1: //POP SS:[rSP] FS Pop a Value from the Stack
				case 0xa9: //POP SS:[rSP] GS Pop a Value from the Stack
				case 0xc8: //BSWAP  Zvqp Byte Swap
				case 0xc9:
				case 0xca:
				case 0xcb:
				case 0xcc:
				case 0xcd:
				case 0xce:
				case 0xcf:
					return true;
				case 0x80: //JO Jvds  Jump short if overflow (OF=1)
				case 0x81: //JNO Jvds  Jump short if not overflow (OF=0)
				case 0x82: //JB Jvds  Jump short if below/not above or equal/carry (CF=1)
				case 0x83: //JNB Jvds  Jump short if not below/above or equal/not carry (CF=0)
				case 0x84: //JZ Jvds  Jump short if zero/equal (ZF=0)
				case 0x85: //JNZ Jvds  Jump short if not zero/not equal (ZF=1)
				case 0x86: //JBE Jvds  Jump short if below or equal/not above (CF=1 AND ZF=1)
				case 0x87: //JNBE Jvds  Jump short if not below or equal/above (CF=0 AND ZF=0)
				case 0x88: //JS Jvds  Jump short if sign (SF=1)
				case 0x89: //JNS Jvds  Jump short if not sign (SF=0)
				case 0x8a: //JP Jvds  Jump short if parity/parity even (PF=1)
				case 0x8b: //JNP Jvds  Jump short if not parity/parity odd
				case 0x8c: //JL Jvds  Jump short if less/not greater (SF!=OF)
				case 0x8d: //JNL Jvds  Jump short if not less/greater or equal (SF=OF)
				case 0x8e: //JLE Jvds  Jump short if less or equal/not greater ((ZF=1) OR (SF!=OF))
				case 0x8f: //JNLE Jvds  Jump short if not less nor equal/greater ((ZF=0) AND (SF=OF))
					n += stride;
					if (n > 15)
						abort(6);
					return true;
				case 0x90: //SETO  Eb Set Byte on Condition - overflow (OF=1)
				case 0x91: //SETNO  Eb Set Byte on Condition - not overflow (OF=0)
				case 0x92: //SETB  Eb Set Byte on Condition - below/not above or equal/carry (CF=1)
				case 0x93: //SETNB  Eb Set Byte on Condition - not below/above or equal/not carry (CF=0)
				case 0x94: //SETZ  Eb Set Byte on Condition - zero/equal (ZF=0)
				case 0x95: //SETNZ  Eb Set Byte on Condition - not zero/not equal (ZF=1)
				case 0x96: //SETBE  Eb Set Byte on Condition - below or equal/not above (CF=1 AND ZF=1)
				case 0x97: //SETNBE  Eb Set Byte on Condition - not below or equal/above (CF=0 AND ZF=0)
				case 0x98: //SETS  Eb Set Byte on Condition - sign (SF=1)
				case 0x99: //SETNS  Eb Set Byte on Condition - not sign (SF=0)
				case 0x9a: //SETP  Eb Set Byte on Condition - parity/parity even (PF=1)
				case 0x9b: //SETNP  Eb Set Byte on Condition - not parity/parity odd
				case 0x9c: //SETL  Eb Set Byte on Condition - less/not greater (SF!=OF)
				case 0x9d: //SETNL  Eb Set Byte on Condition - not less/greater or equal (SF=OF)
				case 0x9e: //SETLE  Eb Set Byte on Condition - less or equal/not greater ((ZF=1) OR (SF!=OF))
				case 0x9f: //SETNLE  Eb Set Byte on Condition - not less nor equal/greater ((ZF=0) AND (SF=OF))
				case 0x40: //CMOVO Evqp Gvqp Conditional Move - overflow (OF=1)
				case 0x41: //CMOVNO Evqp Gvqp Conditional Move - not overflow (OF=0)
				case 0x42: //CMOVB Evqp Gvqp Conditional Move - below/not above or equal/carry (CF=1)
				case 0x43: //CMOVNB Evqp Gvqp Conditional Move - not below/above or equal/not carry (CF=0)
				case 0x44: //CMOVZ Evqp Gvqp Conditional Move - zero/equal (ZF=0)
				case 0x45: //CMOVNZ Evqp Gvqp Conditional Move - not zero/not equal (ZF=1)
				case 0x46: //CMOVBE Evqp Gvqp Conditional Move - below or equal/not above (CF=1 AND ZF=1)
				case 0x47: //CMOVNBE Evqp Gvqp Conditional Move - not below or equal/above (CF=0 AND ZF=0)
				case 0x48: //CMOVS Evqp Gvqp Conditional Move - sign (SF=1)
				case 0x49: //CMOVNS Evqp Gvqp Conditional Move - not sign (SF=0)
				case 0x4a: //CMOVP Evqp Gvqp Conditional Move - parity/parity even (PF=1)
				case 0x4b: //CMOVNP Evqp Gvqp Conditional Move - not parity/parity odd
				case 0x4c: //CMOVL Evqp Gvqp Conditional Move - less/not greater (SF!=OF)
				case 0x4d: //CMOVNL Evqp Gvqp Conditional Move - not less/greater or equal (SF=OF)
				case 0x4e: //CMOVLE Evqp Gvqp Conditional Move - less or equal/not greater ((ZF=1) OR (SF!=OF))
				case 0x4f: //CMOVNLE Evqp Gvqp Conditional Move - not less nor equal/greater ((ZF=0) AND (SF=OF))
				case 0xb6: //MOVZX Eb Gvqp Move with Zero-Extend
				case 0xb7: //MOVZX Ew Gvqp Move with Zero-Extend
				case 0xbe: //MOVSX Eb Gvqp Move with Sign-Extension
				case 0xbf: //MOVSX Ew Gvqp Move with Sign-Extension
				case 0x00: //SLDT LDTR Mw Store Local Descriptor Table Register
				case 0x01: //SGDT GDTR Ms Store Global Descriptor Table Register
				case 0x02: //LAR Mw Gvqp Load Access Rights Byte
				case 0x03: //LSL Mw Gvqp Load Segment Limit
				case 0x20: //MOV Cd Rd Move to/from Control Registers
				case 0x22: //MOV Rd Cd Move to/from Control Registers
				case 0x23: //MOV Rd Dd Move to/from Debug Registers
				case 0xb2: //LSS Mptp SS Load Far Pointer
				case 0xb4: //LFS Mptp FS Load Far Pointer
				case 0xb5: //LGS Mptp GS Load Far Pointer
				case 0xa5: //SHLD Gvqp Evqp Double Precision Shift Left
				case 0xad: //SHRD Gvqp Evqp Double Precision Shift Right
				case 0xa3: //BT Evqp  Bit Test
				case 0xab: //BTS Gvqp Evqp Bit Test and Set
				case 0xb3: //BTR Gvqp Evqp Bit Test and Reset
				case 0xbb: //BTC Gvqp Evqp Bit Test and Complement
				case 0xbc: //BSF Evqp Gvqp Bit Scan Forward
				case 0xbd: //BSR Evqp Gvqp Bit Scan Reverse
				case 0xaf: //IMUL Evqp Gvqp Signed Multiply
				case 0xc0: //XADD  Eb Exchange and Add
				case 0xc1: //XADD  Evqp Exchange and Add
				case 0xb0: //CMPXCHG Gb Eb Compare and Exchange
				case 0xb1: //CMPXCHG Gvqp Evqp Compare and Exchange
				{
					{
						if ((n + 1) > 15)
							abort(6);
						mem8_loc = (uint) ((eip_offset + (n++)) >> 0);
						mem8 = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
							? __ld_8bits_mem8_read()
							: phys_mem8[mem8_loc ^ last_tlb_val]);
					}
					if ((CS_flags & 0x0080) != 0)
					{
						switch (mem8 >> 6)
						{
							case 0:
								if ((mem8 & 7) == 6)
									n += 2;
								break;
							case 1:
								n++;
								break;
							default:
								n += 2;
								break;
						}
					}
					else
					{
						switch ((mem8 & 7) | ((mem8 >> 3) & 0x18))
						{
							case 0x04:
							{
								if ((n + 1) > 15)
									abort(6);
								mem8_loc = (uint) ((eip_offset + (n++)) >> 0);
								local_OPbyte_var = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
									? __ld_8bits_mem8_read()
									: phys_mem8[mem8_loc ^ last_tlb_val]);
							}
								if ((local_OPbyte_var & 7) == 5)
								{
									n += 4;
								}
								break;
							case 0x0c:
								n += 2;
								break;
							case 0x14:
								n += 5;
								break;
							case 0x05:
								n += 4;
								break;
							case 0x00:
							case 0x01:
							case 0x02:
							case 0x03:
							case 0x06:
							case 0x07:
								break;
							case 0x08:
							case 0x09:
							case 0x0a:
							case 0x0b:
							case 0x0d:
							case 0x0e:
							case 0x0f:
								n++;
								break;
							case 0x10:
							case 0x11:
							case 0x12:
							case 0x13:
							case 0x15:
							case 0x16:
							case 0x17:
								n += 4;
								break;
						}
					}
					if (n > 15)
						abort(6);
				}
					return true;
				case 0xa4: //SHLD Gvqp Evqp Double Precision Shift Left
				case 0xac: //SHRD Gvqp Evqp Double Precision Shift Right
				case 0xba: //BT Evqp  Bit Test
				{
					{
						if ((n + 1) > 15)
							abort(6);
						mem8_loc = (uint) ((eip_offset + (n++)) >> 0);
						mem8 = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
							? __ld_8bits_mem8_read()
							: phys_mem8[mem8_loc ^ last_tlb_val]);
					}
					if ((CS_flags & 0x0080) != 0)
					{
						switch (mem8 >> 6)
						{
							case 0:
								if ((mem8 & 7) == 6)
									n += 2;
								break;
							case 1:
								n++;
								break;
							default:
								n += 2;
								break;
						}
					}
					else
					{
						switch ((mem8 & 7) | ((mem8 >> 3) & 0x18))
						{
							case 0x04:
							{
								if ((n + 1) > 15)
									abort(6);
								mem8_loc = (uint) ((eip_offset + (n++)) >> 0);
								local_OPbyte_var = (((last_tlb_val = _tlb_read_[mem8_loc >> 12]) == -1)
									? __ld_8bits_mem8_read()
									: phys_mem8[mem8_loc ^ last_tlb_val]);
							}
								if ((local_OPbyte_var & 7) == 5)
								{
									n += 4;
								}
								break;
							case 0x0c:
								n += 2;
								break;
							case 0x14:
								n += 5;
								break;
							case 0x05:
								n += 4;
								break;
							case 0x00:
							case 0x01:
							case 0x02:
							case 0x03:
							case 0x06:
							case 0x07:
								break;
							case 0x08:
							case 0x09:
							case 0x0a:
							case 0x0b:
							case 0x0d:
							case 0x0e:
							case 0x0f:
								n++;
								break;
							case 0x10:
							case 0x11:
							case 0x12:
							case 0x13:
							case 0x15:
							case 0x16:
							case 0x17:
								n += 4;
								break;
						}
					}
					if (n > 15)
						abort(6);
				}
					n++;
					if (n > 15)
						abort(6);
					return true;
				case 0x04:
				case 0x05: //LOADALL  AX Load All of the CPU Registers
				case 0x07: //LOADALL  EAX Load All of the CPU Registers
				case 0x08: //INVD   Invalidate Internal Caches
				case 0x09: //WBINVD   Write Back and Invalidate Cache
				case 0x0a:
				case 0x0b: //UD2   Undefined Instruction
				case 0x0c:
				case 0x0d: //NOP Ev  No Operation
				case 0x0e:
				case 0x0f:
				case 0x10: //MOVUPS Wps Vps Move Unaligned Packed Single-FP Values
				case 0x11: //MOVUPS Vps Wps Move Unaligned Packed Single-FP Values
				case 0x12: //MOVHLPS Uq Vq Move Packed Single-FP Values High to Low
				case 0x13: //MOVLPS Vq Mq Move Low Packed Single-FP Values
				case 0x14: //UNPCKLPS Wq Vps Unpack and Interleave Low Packed Single-FP Values
				case 0x15: //UNPCKHPS Wq Vps Unpack and Interleave High Packed Single-FP Values
				case 0x16: //MOVLHPS Uq Vq Move Packed Single-FP Values Low to High
				case 0x17: //MOVHPS Vq Mq Move High Packed Single-FP Values
				case 0x18: //HINT_NOP Ev  Hintable NOP
				case 0x19: //HINT_NOP Ev  Hintable NOP
				case 0x1a: //HINT_NOP Ev  Hintable NOP
				case 0x1b: //HINT_NOP Ev  Hintable NOP
				case 0x1c: //HINT_NOP Ev  Hintable NOP
				case 0x1d: //HINT_NOP Ev  Hintable NOP
				case 0x1e: //HINT_NOP Ev  Hintable NOP
				case 0x1f: //HINT_NOP Ev  Hintable NOP
				case 0x21: //MOV Dd Rd Move to/from Debug Registers
				case 0x24: //MOV Td Rd Move to/from Test Registers
				case 0x25:
				case 0x26: //MOV Rd Td Move to/from Test Registers
				case 0x27:
				case 0x28: //MOVAPS Wps Vps Move Aligned Packed Single-FP Values
				case 0x29: //MOVAPS Vps Wps Move Aligned Packed Single-FP Values
				case 0x2a: //CVTPI2PS Qpi Vps Convert Packed DW Integers to1.11 PackedSingle-FP Values
				case 0x2b: //MOVNTPS Vps Mps Store Packed Single-FP Values Using Non-Temporal Hint
				case 0x2c: //CVTTPS2PI Wpsq Ppi Convert with Trunc. Packed Single-FP Values to1.11 PackedDW Integers
				case 0x2d: //CVTPS2PI Wpsq Ppi Convert Packed Single-FP Values to1.11 PackedDW Integers
				case 0x2e: //UCOMISS Vss  Unordered Compare Scalar Single-FP Values and Set EFLAGS
				case 0x2f: //COMISS Vss  Compare Scalar Ordered Single-FP Values and Set EFLAGS
				case 0x30: //WRMSR rCX MSR Write to Model Specific Register
				case 0x32: //RDMSR rCX rAX Read from Model Specific Register
				case 0x33: //RDPMC PMC EAX Read Performance-Monitoring Counters
				case 0x34: //SYSENTER IA32_SYSENTER_CS SS Fast System Call
				case 0x35: //SYSEXIT IA32_SYSENTER_CS SS Fast Return from Fast System Call
				case 0x36:
				case 0x37: //GETSEC EAX  GETSEC Leaf Functions
				case 0x38: //PSHUFB Qq Pq Packed Shuffle Bytes
				case 0x39:
				case 0x3a: //ROUNDPS Wps Vps Round Packed Single-FP Values
				case 0x3b:
				case 0x3c:
				case 0x3d:
				case 0x3e:
				case 0x3f:
				case 0x50: //MOVMSKPS Ups Gdqp Extract Packed Single-FP Sign Mask
				case 0x51: //SQRTPS Wps Vps Compute Square Roots of Packed Single-FP Values
				case 0x52: //RSQRTPS Wps Vps Compute Recipr. of Square Roots of Packed Single-FP Values
				case 0x53: //RCPPS Wps Vps Compute Reciprocals of Packed Single-FP Values
				case 0x54: //ANDPS Wps Vps Bitwise Logical AND of Packed Single-FP Values
				case 0x55: //ANDNPS Wps Vps Bitwise Logical AND NOT of Packed Single-FP Values
				case 0x56: //ORPS Wps Vps Bitwise Logical OR of Single-FP Values
				case 0x57: //XORPS Wps Vps Bitwise Logical XOR for Single-FP Values
				case 0x58: //ADDPS Wps Vps Add Packed Single-FP Values
				case 0x59: //MULPS Wps Vps Multiply Packed Single-FP Values
				case 0x5a: //CVTPS2PD Wps Vpd Convert Packed Single-FP Values to1.11 PackedDouble-FP Values
				case 0x5b: //CVTDQ2PS Wdq Vps Convert Packed DW Integers to1.11 PackedSingle-FP Values
				case 0x5c: //SUBPS Wps Vps Subtract Packed Single-FP Values
				case 0x5d: //MINPS Wps Vps Return Minimum Packed Single-FP Values
				case 0x5e: //DIVPS Wps Vps Divide Packed Single-FP Values
				case 0x5f: //MAXPS Wps Vps Return Maximum Packed Single-FP Values
				case 0x60: //PUNPCKLBW Qd Pq Unpack Low Data
				case 0x61: //PUNPCKLWD Qd Pq Unpack Low Data
				case 0x62: //PUNPCKLDQ Qd Pq Unpack Low Data
				case 0x63: //PACKSSWB Qd Pq Pack with Signed Saturation
				case 0x64: //PCMPGTB Qd Pq Compare Packed Signed Integers for Greater Than
				case 0x65: //PCMPGTW Qd Pq Compare Packed Signed Integers for Greater Than
				case 0x66: //PCMPGTD Qd Pq Compare Packed Signed Integers for Greater Than
				case 0x67: //PACKUSWB Qq Pq Pack with Unsigned Saturation
				case 0x68: //PUNPCKHBW Qq Pq Unpack High Data
				case 0x69: //PUNPCKHWD Qq Pq Unpack High Data
				case 0x6a: //PUNPCKHDQ Qq Pq Unpack High Data
				case 0x6b: //PACKSSDW Qq Pq Pack with Signed Saturation
				case 0x6c: //PUNPCKLQDQ Wdq Vdq Unpack Low Data
				case 0x6d: //PUNPCKHQDQ Wdq Vdq Unpack High Data
				case 0x6e: //MOVD Ed Pq Move Doubleword
				case 0x6f: //MOVQ Qq Pq Move Quadword
				case 0x70: //PSHUFW Qq Pq Shuffle Packed Words
				case 0x71: //PSRLW Ib Nq Shift Packed Data Right Logical
				case 0x72: //PSRLD Ib Nq Shift Double Quadword Right Logical
				case 0x73: //PSRLQ Ib Nq Shift Packed Data Right Logical
				case 0x74: //PCMPEQB Qq Pq Compare Packed Data for Equal
				case 0x75: //PCMPEQW Qq Pq Compare Packed Data for Equal
				case 0x76: //PCMPEQD Qq Pq Compare Packed Data for Equal
				case 0x77: //EMMS   Empty MMX Technology State
				case 0x78: //VMREAD Gd Ed Read Field from Virtual-Machine Control Structure
				case 0x79: //VMWRITE Gd  Write Field to Virtual-Machine Control Structure
				case 0x7a:
				case 0x7b:
				case 0x7c: //HADDPD Wpd Vpd Packed Double-FP Horizontal Add
				case 0x7d: //HSUBPD Wpd Vpd Packed Double-FP Horizontal Subtract
				case 0x7e: //MOVD Pq Ed Move Doubleword
				case 0x7f: //MOVQ Pq Qq Move Quadword
				case 0xa6:
				case 0xa7:
				case 0xaa: //RSM  Flags Resume from System Management Mode
				case 0xae: //FXSAVE ST Mstx Save x87 FPU, MMX, XMM, and MXCSR State
				case 0xb8: //JMPE   Jump to IA-64 Instruction Set
				case 0xb9: //UD G  Undefined Instruction
				case 0xc2: //CMPPS Wps Vps Compare Packed Single-FP Values
				case 0xc3: //MOVNTI Gdqp Mdqp Store Doubleword Using Non-Temporal Hint
				case 0xc4: //PINSRW Rdqp Pq Insert Word
				case 0xc5: //PEXTRW Nq Gdqp Extract Word
				case 0xc6: //SHUFPS Wps Vps Shuffle Packed Single-FP Values
				case 0xc7: //CMPXCHG8B EBX Mq Compare and Exchange Bytes
				default:
					abort(6);
					break;
			}
			return true;
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
			{
				//CR0: bit31 PG Paging If 1, enable paging and use the CR3 register, else disable paging
				cpu.tlb_set_page((int)(Gd & -4096), (int)(Gd & -4096), true);
			}
			else
			{
				var Id = (uint)((cpu.cr3 & -4096) + ((Gd >> 20) & 0xffc));
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
					var Kd = (uint)((Jd & -4096) + ((Gd >> 10) & 0xffc));
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
							cpu.tlb_set_page((int)(Gd & -4096), Ld & -4096, ud, Od);
							return;
						}
					}
				}
				error_code |= (Hd ? 1 : 0) << 1;
				if (ja)
					error_code |= 0x04;
				cpu.cr2 = (int)Gd;
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
			var i = (uint)mem8_loc >> 12;
			if (tlb_read_kernel[i] == -1)
			{
				if (tlb_pages_count >= 2048)
				{
					tlb_flush_all1((i - 1) & 0xfffff);
				}
				tlb_pages[tlb_pages_count++] = i;
			}
			tlb_read_kernel[i] = x;
			if (set_write_tlb)
			{
				tlb_write_kernel[i] = x;
			}
			else
			{
				tlb_write_kernel[i] = -1;
			}
			if (set_user_tlb)
			{
				tlb_read_user[i] = x;
				if (set_write_tlb)
				{
					tlb_write_user[i] = x;
				}
				else
				{
					tlb_write_user[i] = -1;
				}
			}
			else
			{
				tlb_read_user[i] = -1;
				tlb_write_user[i] = -1;
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
		#endregion
	}
}