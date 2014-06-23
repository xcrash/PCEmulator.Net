namespace PCEmulator.Net.Operands
{
	public interface IOperand<T>
	{
		T readX();
		T setX { set; }

		T PopValue();
		void PushValue(T x);
	}
}