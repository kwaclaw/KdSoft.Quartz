echo off
start /B http://localhost:8090 && start docfx.exe serve _site -p 8090
exit

