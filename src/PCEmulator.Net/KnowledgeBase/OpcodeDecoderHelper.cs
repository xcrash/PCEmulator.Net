namespace PCEmulator.Net.KnowledgeBase
{
	public enum OperationDirection
	{
		RegistryToMemory = 0,
		MemoryToRegistry = 1
	}

	public enum OperationOperandSize
	{
		EightBit = 0,
		SixteenBitOr32Bit = 1
	}

	public static class OpcodeDecoderHelper
	{
		public static OperationDirection GetDirection(byte opbyte)
		{
			return ((opbyte >> 1) & 1) == 0
				? OperationDirection.RegistryToMemory
				: OperationDirection.MemoryToRegistry;
		}

		public static OperationOperandSize GetOperandSize(byte opbyte)
		{
			return (opbyte & 1) == 0
				? OperationOperandSize.EightBit
				: OperationOperandSize.SixteenBitOr32Bit;
		}
	}
}