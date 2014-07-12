using System.Security.Cryptography;

namespace PCEmulator.Net
{
	internal class DebugHelper
	{
		private readonly CPU_X86_Impl.Executor executor;

		public R8DebugHelper R8;
		public R16DebugHelper R16;
		public R32DebugHelper R32;

		public DebugHelper(CPU_X86_Impl.Executor executor)
		{
			R8 = new R8DebugHelper(executor);
			R16 = new R16DebugHelper(executor);
			R32 = new R32DebugHelper(executor);
			this.executor = executor;
		}
	}

	internal class R8DebugHelper
	{
		private readonly CPU_X86_Impl.Executor executor;

		public R8DebugHelper(CPU_X86_Impl.Executor executor)
		{
			this.executor = executor;
		}

		public byte AL
		{
			get { return (byte) executor.regs[0]; }
		}

		public byte CL
		{
			get { return (byte)executor.regs[1]; }
		}

		public byte DL
		{
			get { return (byte)executor.regs[2]; }
		}

		public byte BL
		{
			get { return (byte)executor.regs[3]; }
		}

		public byte AH
		{
			get { return (byte)(executor.regs[0] >> 8); }
		}

		public byte CH
		{
			get { return (byte)(executor.regs[1] >> 8); }
		}

		public byte DH
		{
			get { return (byte)(executor.regs[2] >> 8); }
		}

		public byte BH
		{
			get { return (byte)(executor.regs[3] >> 8); }
		}

		public override string ToString()
		{
			return string.Format("AL={0:x2} BL={1:x2} CL={2:x2} DL={3:x2} AH={4:x2} BH={5:x2} CH={6:x2} DH={7:x2}",
				AL, BL, CL, DL, AH, BH, CH, DH);
		}
	}

	internal class R16DebugHelper
	{
		private readonly CPU_X86_Impl.Executor executor;

		public R16DebugHelper(CPU_X86_Impl.Executor executor)
		{
			this.executor = executor;
		}

		public ushort AX
		{
			get { return (ushort)executor.regs[CPU_X86.REG_AX]; }
		}

		public ushort CX
		{
			get { return (ushort)executor.regs[CPU_X86.REG_CX]; }
		}

		public ushort DX
		{
			get { return (ushort)executor.regs[CPU_X86.REG_DX]; }
		}

		public ushort BX
		{
			get { return (ushort)executor.regs[CPU_X86.REG_BX]; }
		}

		public ushort SP
		{
			get { return (ushort)executor.regs[4]; }
		}

		public ushort BP
		{
			get { return (ushort)executor.regs[5]; }
		}

		public ushort SI
		{
			get { return (ushort)executor.regs[6]; }
		}

		public ushort DI
		{
			get { return (ushort)executor.regs[7]; }
		}

		public override string ToString()
		{
			return string.Format("AX={0:x4} BX={1:x4} CX={2:x4} DX={3:x4} SP={4:x4} BP={5:x4} SI={6:x4} DI={7:x4}",
				AX, BX, CX, DX, SP, BP, SI, DI);
		}
	}

	internal class R32DebugHelper
	{
		private readonly CPU_X86_Impl.Executor executor;

		public R32DebugHelper(CPU_X86_Impl.Executor executor)
		{
			this.executor = executor;
		}

		public ushort EAX
		{
			get { return (ushort)executor.regs[CPU_X86.REG_AX]; }
		}

		public ushort ECX
		{
			get { return (ushort)executor.regs[CPU_X86.REG_CX]; }
		}

		public ushort EDX
		{
			get { return (ushort)executor.regs[CPU_X86.REG_DX]; }
		}

		public ushort EBX
		{
			get { return (ushort)executor.regs[CPU_X86.REG_BX]; }
		}

		public ushort ESP
		{
			get { return (ushort)executor.regs[4]; }
		}

		public ushort EBP
		{
			get { return (ushort)executor.regs[5]; }
		}

		public ushort ESI
		{
			get { return (ushort)executor.regs[6]; }
		}

		public ushort EDI
		{
			get { return (ushort)executor.regs[7]; }
		}

		public override string ToString()
		{
			return string.Format("EAX={0:x8} EBX={1:x8} ECX={2:x8} EDX={3:x8} ESP={4:x8} EBP={5:x8} ESI={6:x8} EDI={7:x8}",
				EAX, EBX, ECX, EDX, ESP, EBP, ESI, EDI);
		}
	}
}