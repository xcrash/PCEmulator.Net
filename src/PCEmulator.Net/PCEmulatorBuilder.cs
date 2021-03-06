﻿using System;
using System.Collections.Generic;

namespace PCEmulator.Net
{
	public class PCEmulatorBuilder
	{
		public static PCEmulator BuildLinuxReady(Action<char> serialWrite, DateTime? cmosFixedDate = null, Func<int> getBootTime = null, bool isTraceEnabled = false)
		{
			var @params = new PCEmulatorParams
			{
				serial_write = serialWrite,
				mem_size = 16*1024*1024,
				get_boot_time = getBootTime
			};

			const int startAddr = 0x10000;

			var pc = new PCEmulator(@params, isTraceEnabled || cmosFixedDate.HasValue || getBootTime == null, cmosFixedDate);
			var loadmemRes = pc.LoadBinnaries(
				new Dictionary<string, uint>
				{
					{"vmlinux-2.6.20.bin", 0x00100000},
					{"root.bin", 0x00400000},
					{"linuxstart.bin", 0x10000},
				});

			//set the Linux kernel command line
			//Note: we don't use initramfs because it is not possible to
			//disable gzip decompression in this case, which would be too
			//slow.
			const int cmdlineAddr = 0xf800;
			pc.cpu.write_string(cmdlineAddr, "console=ttyS0 root=/dev/ram0 rw init=/sbin/init notsc=1");

			pc.cpu.eip = startAddr;
			pc.cpu.regs[CPU_X86.REG_AX] = @params.mem_size; /* eax */
			pc.cpu.regs[CPU_X86.REG_BX] = loadmemRes[1]; /* ebx */
			pc.cpu.regs[CPU_X86.REG_CX] = cmdlineAddr; /* ecx */
			return pc;
		}
	}
}