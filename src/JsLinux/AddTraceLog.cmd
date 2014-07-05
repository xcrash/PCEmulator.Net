If Not Exist patched patch.exe -N -s -f CMOS.js CMOS.patch
ICACLS CMOS.js /reset

If Not Exist patched patch.exe -N -s -f cpux86-ta.js cpux86-ta.patch
ICACLS cpux86-ta.js /reset

If Not Exist patched patch.exe -N -s -f PCEmulator.js PCEmulator.patch
ICACLS PCEmulator.js /reset

echo patched > patched