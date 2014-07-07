namespace PCEmulator.Net.Operands
{
	public interface IOperand<T>
	{
		T readX();
		T setX { set; }

		T PopValue();

		//Arithmetic ops
		uint ReadOpValue0();
		uint ReadOpValue1();
		void ProceedResult(uint r);
	}
}