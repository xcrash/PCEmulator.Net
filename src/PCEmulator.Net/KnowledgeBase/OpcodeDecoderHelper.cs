namespace PCEmulator.Net.KnowledgeBase
{
	public enum OpDirection : byte
	{
		RegistryToMemory = 0,
		MemoryToRegistry = 1
	}

	public enum OpOperandSize : byte
	{
		EightBit = 0,
		SixteenBitOr32Bit = 1
	}

	public enum OpArithmeticType : byte
	{
		Add = 0,
		Or = 1,
		Adc = 2,
		Sbb = 3,
		And = 4,
		Sub = 5,
		Xor = 6,
		Cmp = 7
	}


	public static class OpcodeDecoderHelper
	{
		/// <summary>
		/// http://www.c-jump.com/CIS77/CPU/x86/X77_0050_add_opcode.htm
		/// </summary>
		/// <param name="opbyte"></param>
		/// <returns></returns>
		public static OpDirection GetDirection(this byte opbyte)
		{
			return ((opbyte >> 1) & 1) == 0
				? OpDirection.RegistryToMemory
				: OpDirection.MemoryToRegistry;
		}

		public static OpDirection GetDirection(this uint opuint)
		{
			return GetDirection((byte)opuint);
		}

		/// <summary>
		/// http://www.c-jump.com/CIS77/CPU/x86/X77_0050_add_opcode.htm
		/// </summary>
		/// <param name="opbyte"></param>
		/// <returns></returns>
		public static OpOperandSize GetOperandSize(this byte opbyte)
		{
			return (opbyte & 1) == 0
				? OpOperandSize.EightBit
				: OpOperandSize.SixteenBitOr32Bit;
		}

		/// <summary>
		/// see do_8bit_math
		/// </summary>
		/// <param name="opbyte"></param>
		/// <returns></returns>
		public static OpArithmeticType GetArithmeticOpType(this byte opbyte)
		{
			return (OpArithmeticType) (opbyte >> 3);
		}

		public static OpArithmeticType GetArithmeticOpType(this uint opuint)
		{
			return GetArithmeticOpType((byte)opuint);
		}
	}
}