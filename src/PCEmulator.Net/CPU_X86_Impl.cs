using System;
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
			int last_tlb_val;
			object phys_mem16;
			Int32Array phys_mem32;
			int[] tlb_read_kernel;
			int[] tlb_write_kernel;
			int[] tlb_read_user;
			int[] tlb_write_user;

			uint eip;
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
						case 0x98: //CBW AL AX Convert Byte to Word
							regs[0] = (regs[0] << 16) >> 16;
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

						/*
						TWO BYTE CODE INSTRUCTIONS BEGIN WITH 0F :  0F xx
						=====================================================================================================
					*/
						case 0x0f:
							OPbyte = phys_mem8[physmem8_ptr++];
							switch (OPbyte)
							{
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
						default:
							throw new NotImplementedException(string.Format("OPbyte 0x{0:X} not implemented", OPbyte));
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
			throw new NotImplementedException();
		}

		private uint pop_dword_from_stack_read()
		{
			throw new NotImplementedException();
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

		private uint check_carry()
		{
			throw new NotImplementedException();
		}

		private uint ld_32bits_mem8_read()
		{
			var x = ld_8bits_mem8_read();
			mem8_loc++;
			x |= ld_8bits_mem8_read() << 8;
			mem8_loc++;
			x |= ld_8bits_mem8_read() << 16;
			mem8_loc++;
			x |= ld_8bits_mem8_read() << 24;
			mem8_loc -= 3;
			return x;
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

		private void push_dword_to_stack(uint i)
		{
			throw new NotImplementedException();
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
		private object initial_mem_ptr;
		private object eip_offset;
		private int[] _tlb_read_;

		/// <summary>
		/// segment translation routine (I believe):
		/// Translates Logical Memory Address to Linear Memory Address
		/// </summary>
		private uint segment_translation(int mem8)
		{
			int @base;
			//int mem8_loc;
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
			throw new NotImplementedException();
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