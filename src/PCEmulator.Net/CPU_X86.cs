using System;
using System.IO;
using PCEmulator.Net.Utils;

namespace PCEmulator.Net
{
	public abstract class CPU_X86
	{
		public Func<int> get_hard_intno;
		public uint cycle_count;
		public Func<uint, byte> ld8_port;
		public Func<uint, ushort> ld16_port;
		public Func<uint, uint> ld32_port;
		public Action<uint, byte> st8_port;
		public Action<uint, ushort> st16_port;
		public Action<uint, uint> st32_port;

		/// <summary>
		/// IP/EIP/RIP: Instruction pointer. Holds the program counter, the current instruction address.
		/// </summary>
		public uint eip;

		/// <summary>
		/// EAX, EBX, ECX, EDX, ESI, EDI, ESP, EBP  32bit registers
		/// 
		/// AX/EAX/RAX: Accumulator
		/// BX/EBX/RBX: Base index (for use with arrays)
		/// CX/ECX/RCX: Counter
		/// DX/EDX/RDX: Data/general
		/// SI/ESI/RSI: Source index for string operations.
		/// DI/EDI/RDI: Destination index for string operations.
		/// SP/ESP/RSP: Stack pointer for top address of the stack.
		/// BP/EBP/RBP: Stack base pointer for holding the address of the current stack frame.
		/// </summary>
		public uint[] regs;

		protected uint mem_size;
		private byte[] phys_mem;
		protected Uint8Array phys_mem8;
		protected Uint16Array phys_mem16;
		protected Int32Array phys_mem32;
		protected int cc_op;
		protected int cc_dst;
		protected int cc_src;
		protected int cc_op2;
		protected int cc_dst2;
		private int df;

		/// <summary>
		/// EFLAG register
		/// 
		/// 0.    CF : Carry Flag. Set if the last arithmetic operation carried (addition) or borrowed (subtraction) a
		///             bit beyond the size of the register. This is then checked when the operation is followed with
		///             an add-with-carry or subtract-with-borrow to deal with values too large for just one register to contain.
		/// 2.    PF : Parity Flag. Set if the number of set bits in the least significant byte is a multiple of 2.
		/// 4.    AF : Adjust Flag. Carry of Binary Code Decimal (BCD) numbers arithmetic operations.
		/// 6.    ZF : Zero Flag. Set if the result of an operation is Zero (0).
		/// 7.    SF : Sign Flag. Set if the result of an operation is negative.
		/// 8.    TF : Trap Flag. Set if step by step debugging.
		/// 9.    IF : Interruption Flag. Set if interrupts are enabled.
		/// 10.   DF : Direction Flag. Stream direction. If set, string operations will decrement their pointer rather
		///             than incrementing it, reading memory backwards.
		/// 11.   OF : Overflow Flag. Set if signed arithmetic operations result in a value too large for the register to contain.
		/// 12-13. IOPL : I/O Privilege Level field (2 bits). I/O Privilege Level of the current process.
		/// 14.   NT : Nested Task flag. Controls chaining of interrupts. Set if the current process is linked to the next process.
		/// 16.   RF : Resume Flag. Response to debug exceptions.
		/// 17.   VM : Virtual-8086 Mode. Set if in 8086 compatibility mode.
		/// 18.   AC : Alignment Check. Set if alignment checking of memory references is done.
		/// 19.   VIF : Virtual Interrupt Flag. Virtual image of IF.
		/// 20.   VIP : Virtual Interrupt Pending flag. Set if an interrupt is pending.
		/// 21.   ID : Identification Flag. Support for CPUID instruction if can be set.
		/// </summary>
		protected int eflags;

		protected int hard_irq;
		protected int hard_intno;
		protected int cpl;

		/// <summary>
		/// Control Register
		/// 
		/// CR0
		/// ---
		/// 31    PG  Paging             If 1, enable paging and use the CR3 register, else disable paging
		/// 30    CD  Cache disable      Globally enables/disable the memory cache
		/// 29    NW  Not-write through  Globally enables/disable write-back caching
		/// 18    AM  Alignment mask     Alignment check enabled if AM set, AC flag (in EFLAGS register) set, and privilege level is 3
		/// 16    WP  Write protect      Determines whether the CPU can write to pages marked read-only
		/// 5     NE  Numeric error      Enable internal x87 floating point error reporting when set, else enables PC style x87 error detection
		/// 4     ET  Extension type     On the 386, it allowed to specify whether the external math coprocessor was an 80287 or 80387
		/// 3     TS  Task switched      Allows saving x87 task context only after x87 instruction used after task switch
		/// 2     EM  Emulation          If set, no x87 floating point unit present, if clear, x87 FPU present
		/// 1     MP  Monitor co-processor   Controls interaction of WAIT/FWAIT instructions with TS flag in CR0
		/// 0     PE  Protected Mode Enable  If 1, system is in protected mode, else system is in real mode
		/// </summary>
		private int cr0;

		/// <summary>
		/// Control Register
		/// 
		/// CR2
		/// ---
		/// Page Fault Linear Address (PFLA) When a page fault occurs,
		/// the address the program attempted to access is stored in the
		/// CR2 register.
		/// </summary>
		private int cr2;

		/// <summary>
		/// Control Register
		/// 
		/// CR3
		/// ---
		/// Used when virtual addressing is enabled, hence when the PG
		/// bit is set in CR0.  CR3 enables the processor to translate
		/// virtual addresses into physical addresses by locating the page
		/// directory and page tables for the current task.
		/// 
		/// Typically, the upper 20 bits of CR3 become the page directory
		/// base register (PDBR), which stores the physical address of the
		/// first page directory entry.
		/// </summary>
		private int cr3;

		/// <summary>
		/// Control Register
		/// 
		/// CR4
		/// ---
		/// Used in protected mode to control operations such as virtual-8086 support, enabling I/O breakpoints,
		/// page size extension and machine check exceptions.
		/// Bit  Name    Full Name   Description
		/// 18   OSXSAVE XSAVE and Processor Extended States Enable
		/// 17   PCIDE   PCID Enable If set, enables process-context identifiers (PCIDs).
		/// 14   SMXE    SMX Enable
		/// 13   VMXE    VMX Enable
		/// 10   OSXMMEXCPT  Operating System Support for Unmasked SIMD Floating-Point Exceptions    If set, enables unmasked SSE exceptions.
		/// 9    OSFXSR  Operating system support for FXSAVE and FXSTOR instructions If set, enables SSE instructions and fast FPU save & restore8    PCE Performance-Monitoring Counter enable
		///         If set, RDPMC can be executed at any privilege level, else RDPMC can only be used in ring 0.
		/// 7    PGE Page Global Enabled If set, address translations (PDE or PTE records) may be shared between address spaces.
		/// 6    MCE Machine Check Exception If set, enables machine check interrupts to occur.
		/// 5    PAE Physical Address Extension
		///         If set, changes page table layout to translate 32-bit virtual addresses into extended 36-bit physical addresses.
		/// 4    PSE Page Size Extensions    If unset, page size is 4 KB, else page size is increased to 4 MB (ignored with PAE set).
		/// 3    DE  Debugging Extensions
		/// 2    TSD Time Stamp Disable
		///         If set, RDTSC instruction can only be executed when in ring 0, otherwise RDTSC can be used at any privilege level.
		/// 1    PVI Protected-mode Virtual Interrupts   If set, enables support for the virtual interrupt flag (VIF) in protected mode.
		/// 0    VME Virtual 8086 Mode Extensions    If set, enables support for the virtual interrupt flag (VIF) in virtual-8086 mode.
		/// </summary>
		private int cr4;

		/// <summary>
		/// Segment registers:
		/// --------------------
		/// ES: Extra
		/// CS: Code
		/// SS: Stack
		/// DS: Data
		/// FS: Extra
		/// GS: Extra
		/// 
		/// In memory addressing for Intel x86 computer architectures,
		/// segment descriptors are a part of the segmentation unit, used for
		/// translating a logical address to a linear address. Segment descriptors
		/// describe the memory segment referred to in the logical address.
		/// 
		/// The segment descriptor (8 bytes long in 80286) contains the following
		/// fields:
		/// 
		/// - A segment base address
		/// - The segment limit which specifies the segment limit
		/// - Access rights byte containing the protection mechanism information
		/// - Control bits
		/// </summary>
		protected Segment[] segs;

		/// <summary>
		/// Interrupt Descriptor Table
		/// ---------------------------
		/// The interrupt descriptor table (IDT) associates each interrupt
		/// or exception identifier with a descriptor for the instructions
		/// that service the associated event. Like the GDT and LDTs, the
		/// IDT is an array of 8-byte descriptors. Unlike the GDT and LDTs,
		/// the first entry of the IDT may contain a descriptor.
		/// 
		/// To form an index into the IDT, the processor multiplies the
		/// interrupt or exception identifier by eight. Because there are
		/// only 256 identifiers, the IDT need not contain more than 256
		/// descriptors. It can contain fewer than 256 entries; entries are
		/// required only for interrupt identifiers that are actually used.
		/// </summary>
		private Segment idt;

		/// <summary>
		/// The Global Descriptor Table
		/// </summary>
		private Segment gdt;

		/// <summary>
		/// The Local Descriptor Table
		/// </summary>
		private Segment ldt;

		/// <summary>
		/// Task Register
		/// --------------
		/// The task register (TR) identifies the currently executing task
		/// by pointing to the TSS.
		/// 
		/// The task register has both a "visible" portion (i.e., can be
		/// read and changed by instructions) and an "invisible" portion
		/// (maintained by the processor to correspond to the visible
		/// portion; cannot be read by any instruction). The selector in
		/// the visible portion selects a TSS descriptor in the GDT. The
		/// processor uses the invisible portion to cache the base and
		/// limit values from the TSS descriptor. Holding the base and
		/// limit in a register makes execution of the task more efficient,
		/// because the processor does not need to repeatedly fetch these
		/// values from memory when it references the TSS of the current
		/// task.
		/// 
		/// The instructions LTR and STR are used to modify and read the
		/// visible portion of the task register. Both instructions take
		/// one operand, a 16-bit selector located in memory or in a
		/// general register.
		/// 
		/// LTR (Load task register) loads the visible portion of the task
		/// register with the selector operand, which must select a TSS
		/// descriptor in the GDT. LTR also loads the invisible portion
		/// with information from the TSS descriptor selected by the
		/// operand. LTR is a privileged instruction; it may be executed
		/// only when CPL is zero. LTR is generally used during system
		/// initialization to give an initial value to the task register;
		/// thereafter, the contents of TR are changed by task switch
		/// operations.
		/// 
		/// STR (Store task register) stores the visible portion of the task
		/// register in a general register or memory word. STR is not privileged.
		/// 
		/// All the information the processor needs in order to manage a
		/// task is stored in a special type of segment, a task state
		/// segment (TSS). The fields of a TSS belong to two classes:
		/// 
		/// 1. A dynamic set that the processor updates with each switch from the
		/// task. This set includes the fields that store:
		/// 
		/// - The general registers (EAX, ECX, EDX, EBX, ESP, EBP, ESI, EDI).
		/// - The segment registers (ES, CS, SS, DS, FS, GS).
		/// - The flags register (EFLAGS).
		/// - The instruction pointer (EIP).
		/// - The selector of the TSS of the previously executing task (updated only when a return is expected).
		/// 
		/// 2. A static set that the processor reads but does not change. This
		/// set includes the fields that store:
		/// 
		/// - The selector of the task's LDT.
		/// - The register (PDBR) that contains the base address of the task's
		/// page directory (read only when paging is enabled).
		/// - Pointers to the stacks for privilege levels 0-2.
		/// - The T-bit (debug trap bit) which causes the processor to raise a
		/// debug exception when a task switch occurs.
		/// - The I/O map base
		/// </summary>
		private Segment tr;

		protected bool halted;
		protected byte[] tlb_read_kernel;
		protected int[] tlb_write_kernel;
		protected byte[] tlb_read_user;
		protected int[] tlb_write_user;
		private int[] tlb_pages;
		private int tlb_pages_count;

		public CPU_X86()
		{
			regs = new uint[8];
			for (var i = 0; i < 8; i++)
			{
				regs[i] = 0;
			}

			eip = 0; //instruction pointer
			cc_op = 0; // current op
			cc_dst = 0; // current dest
			cc_src = 0; // current src
			cc_op2 = 0; // current op, byte2
			cc_dst2 = 0; // current dest, byte2
			df = 1; // Direction Flag
			eflags = 0x2; // EFLAG register
			cycle_count = 0;
			hard_irq = 0;
			hard_intno = -1;
			cpl = 0; //current privilege level
			cr0 = (1 << 0); //PE-mode ON
			cr2 = 0;
			cr3 = 0;
			cr4 = 0;

			/* NOTE: Only segs 0->5 appear to be used in the code, so only ES->GS */
			segs = new Segment[7]; //   [" ES", " CS", " SS", " DS", " FS", " GS", "LDT", " TR"]
			for (var i = 0; i < 7; i++)
			{
				segs[i] = new Segment {selector = 0, @base = 0, limit = 0, flags = 0};
			}
			segs[2].flags = (1 << 22); // SS
			segs[1].flags = (1 << 22); // CS

			idt = new Segment {@base = 0, limit = 0};
			gdt = new Segment {@base = 0, limit = 0};
			ldt = new Segment {selector = 0, @base = 0, limit = 0, flags = 0};
			tr = new Segment {selector = 0, @base = 0, limit = 0, flags = 0};
			halted = false;
			phys_mem = null; //pointer to raw memory buffer allocated by browser

			/*
			A translation lookaside buffer (TLB) is a CPU cache that memory
			management hardware uses to improve virtual address translation
			speed.

			A TLB has a fixed number of slots that contain page table
			entries, which map virtual addresses to physical addresses. The
			virtual memory is the space seen from a process. This space is
			segmented in pages of a prefixed size. The page table
			(generally loaded in memory) keeps track of where the virtual
			pages are loaded in the physical memory. The TLB is a cache of
			the page table; that is, only a subset of its content are
			stored.
			*/
			const int tlbSize = 0x100000;
			tlb_read_kernel = new byte[tlbSize];
			tlb_write_kernel = new int[tlbSize];
			tlb_read_user = new byte[tlbSize];
			tlb_write_user = new int[tlbSize];
			for (var i = 0; i < tlbSize; i++)
			{
				tlb_read_kernel[i] = byte.MaxValue;
				tlb_write_kernel[i] = -1;
				tlb_read_user[i] = byte.MaxValue;
				tlb_write_user[i] = -1;
			}
			tlb_pages = new int[2048];
			tlb_pages_count = 0;
		}

		/// <summary>
		/// Allocates a memory chunnk new_mem_size bytes long and makes 8,16,32 bit array references into it
		/// </summary>
		/// <param name="new_mem_size"></param>
		public void phys_mem_resize(uint new_mem_size)
		{
			mem_size = new_mem_size;
			new_mem_size += ((15 + 3) & ~3);
			phys_mem = new byte[new_mem_size];
			phys_mem8 = new Uint8Array(phys_mem, 0, new_mem_size);
			phys_mem16 = new Uint16Array(phys_mem, 0, new_mem_size/2);
			phys_mem32 = new Int32Array(phys_mem, 0, new_mem_size/4);
		}

		public void set_hard_irq_wrapper(uint irq)
		{
			throw new NotImplementedException();
		}

		public uint return_cycle_count()
		{
			return cycle_count;
		}

		public uint load_binary(string fileName, uint mem8Loc)
		{
			var fileContent = File.ReadAllBytes(fileName);
			for (var i = 0; i < fileContent.Length; i++)
			{
				this.st8_phys((uint) (mem8Loc + i), fileContent[i]);
			}

			return (uint) fileContent.Length;
		}

		/// <summary>
		///  Execution Wrapper
		/// ==========================================================================================
		/// This seems to primarily catch internal interrupts.
		/// </summary>
		/// <param name="N_cycles"></param>
		/// <returns></returns>
		public int exec(uint N_cycles)
		{
			var final_cycle_count = cycle_count + N_cycles;
			var exit_code = 256;
			IntNoException interrupt = null;
			while (cycle_count < final_cycle_count)
			{
				try
				{
					exit_code = exec_internal(final_cycle_count - cycle_count, interrupt);
					if (exit_code != 256)
						break;
					interrupt = null;
				}
				catch (IntNoException cpu_exception)
				{
					interrupt = cpu_exception;
				}
			}
			return exit_code;
		}

		protected abstract int exec_internal(uint u, IntNoException interrupt);

		/// <summary>
		/// writes ASCII string in na into memory location mem8_loc
		/// </summary>
		/// <param name="mem8_loc"></param>
		/// <param name="str"></param>
		public void write_string(uint mem8_loc, string str)
		{
			for (var i = 0; i < str.Length; i++)
			{
				st8_phys(mem8_loc++, (byte) (str[i] & 0xff));
			}
			st8_phys(mem8_loc, 0);
		}

		private void st8_phys(uint mem8_loc, byte x)
		{
			phys_mem8[mem8_loc] = x;
		}

		public class Segment
		{
			public int selector;
			public uint @base;
			public int limit;
			public int flags;
		}

		public class IntNoException : Exception
		{
			public int intno;
			public int error_code { get; private set; }
		}
	}
}