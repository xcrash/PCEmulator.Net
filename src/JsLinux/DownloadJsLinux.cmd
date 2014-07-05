If Not Exist LICENSE ..\packages\WGETWindows.1.11.4\wget.exe --no-check-certificate https://github.com/levskaya/jslinux-deobfuscated/raw/master/LICENSE

If Not Exist index.html ..\packages\WGETWindows.1.11.4\wget.exe --no-check-certificate https://github.com/levskaya/jslinux-deobfuscated/raw/master/index.html

If Not Exist clipboard.js ..\packages\WGETWindows.1.11.4\wget.exe --no-check-certificate https://github.com/levskaya/jslinux-deobfuscated/raw/master/clipboard.js
If Not Exist CMOS.js ..\packages\WGETWindows.1.11.4\wget.exe --no-check-certificate https://github.com/levskaya/jslinux-deobfuscated/raw/master/CMOS.js
If Not Exist cpux86-ta.js ..\packages\WGETWindows.1.11.4\wget.exe --no-check-certificate https://github.com/levskaya/jslinux-deobfuscated/raw/master/cpux86-ta.js
If Not Exist jslinux.js ..\packages\WGETWindows.1.11.4\wget.exe --no-check-certificate https://github.com/levskaya/jslinux-deobfuscated/raw/master/jslinux.js
If Not Exist KBD.js ..\packages\WGETWindows.1.11.4\wget.exe --no-check-certificate https://github.com/levskaya/jslinux-deobfuscated/raw/master/KBD.js
If Not Exist PCEmulator.js ..\packages\WGETWindows.1.11.4\wget.exe --no-check-certificate https://github.com/levskaya/jslinux-deobfuscated/raw/master/PCEmulator.js
If Not Exist PIC.js ..\packages\WGETWindows.1.11.4\wget.exe --no-check-certificate https://github.com/levskaya/jslinux-deobfuscated/raw/master/PIC.js
If Not Exist PIT.js ..\packages\WGETWindows.1.11.4\wget.exe --no-check-certificate https://github.com/levskaya/jslinux-deobfuscated/raw/master/PIT.js
If Not Exist Serial.js ..\packages\WGETWindows.1.11.4\wget.exe --no-check-certificate https://github.com/levskaya/jslinux-deobfuscated/raw/master/Serial.js
If Not Exist term.js ..\packages\WGETWindows.1.11.4\wget.exe --no-check-certificate https://github.com/levskaya/jslinux-deobfuscated/raw/master/term.js

If Not Exist vmlinux-2.6.20.bin ..\packages\WGETWindows.1.11.4\wget.exe --no-check-certificate https://github.com/levskaya/jslinux-deobfuscated/raw/master/vmlinux-2.6.20.bin
If Not Exist root.bin ..\packages\WGETWindows.1.11.4\wget.exe --no-check-certificate https://github.com/levskaya/jslinux-deobfuscated/raw/master/root.bin
If Not Exist linuxstart.bin ..\packages\WGETWindows.1.11.4\wget.exe --no-check-certificate https://github.com/levskaya/jslinux-deobfuscated/raw/master/linuxstart.bin

If Not Exist patched patch.exe -N -s -f CMOS.js CMOS.patch
ICACLS CMOS.js /reset

If Not Exist patched patch.exe -N -s -f cpux86-ta.js cpux86-ta.patch
ICACLS cpux86-ta.js /reset

If Not Exist patched patch.exe -N -s -f PCEmulator.js PCEmulator.patch
ICACLS PCEmulator.js /reset

echo patched > patched