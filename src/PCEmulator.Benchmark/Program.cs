using System;
using BenchmarkDotNet;
using PCEmulator.Net.Utils;

namespace PCEmulator.Benchmark
{
	public class Uint8ArrayBenchmarkCompetition : BenchmarkCompetition
	{
		const int LEN = 102400000;

		[BenchmarkMethod]
		public void WriteNow()
		{
			var buffer = new byte[4*LEN];
			var ar = new Int32Array(buffer, 0, 1);

			for (int i = 0; i < LEN; i++)
			{
				ar[i] = i;
			}
		}

		[BenchmarkMethod]
		public void WriteStruct()
		{
			var buffer = new byte[4 * LEN];
			for (var i = 0; i < LEN; i++)
			{
				unsafe
				{
					fixed (byte* bufferp = buffer)
					{
						((IntAccess*) (bufferp + i*4))->Data = i;
					}
				}
			}
		}

		[BenchmarkMethod]
		public void WriteStruct2()
		{
			var buffer = new byte[4 * LEN];
			var ar = new Int32ArrayUnsafe(buffer, 0, 1);

			for (int i = 0; i < LEN; i++)
			{
				ar[i] = i;
			}
		}

		[BenchmarkMethod]
		public int ReadNow()
		{
			var buffer = new byte[4 * LEN];
			var ar = new Int32Array(buffer, 0, 1);
			var sum = 0;
			for (var i = 0; i < LEN; i++)
			{
				sum += ar[i];
			}
			return sum;
		}


		[BenchmarkMethod]
		public int ReadStruct()
		{
			var buffer = new byte[4 * LEN];
			var sum = 0;
			for (var i = 0; i < LEN; i++)
			{
				unsafe
				{
					fixed (byte* bufferp = buffer)
					{
						sum += ((IntAccess*)(bufferp + i * 4))->Data;
					}
				}
			}
			return sum;
		}

		[BenchmarkMethod]
		public int ReadStruct2()
		{
			var buffer = new byte[4 * LEN];
			var ar = new Int32ArrayUnsafe(buffer, 0, 1);
			var sum = 0;
			for (var i = 0; i < LEN; i++)
			{
				sum += ar[i];
			}
			return sum;
		}
		private struct IntAccess
		{
			public int Data;
		}

	}

	/// <summary>
	/// Competition results:
	/// WriteNow    : 1018ms, 2325517 ticks [Error = 00.84%, StdDev = 2.84]
	/// WriteStruct :  510ms, 1166338 ticks [Error = 04.37%, StdDev = 5.71]
	/// ReadNow     :  924ms, 2110552 ticks [Error = 01.20%, StdDev = 3.07]
	/// ReadStruct  :  462ms, 1055004 ticks [Error = 02.05%, StdDev = 2.61]
	/// </summary>
	class Program
	{
		static void Main(string[] args)
		{
//			Console.WriteLine("JIT: " + JitVersionInfo.GetJitVersion());
			BenchmarkSettings.Instance.DetailedMode = true;
			var competition = new Uint8ArrayBenchmarkCompetition();
			competition.Run();
			Console.ReadLine();
		}
	}
}
