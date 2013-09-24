@echo off
Set mm=%DATE:~4,2%
Set dd=%DATE:~7,2%
Set yyyy=%DATE:~10,4%
set zipFile=C:\KIMU\Backup\%yyyy%-%mm%-%dd%.zip

C:\KIMU\Scripts\Tools\7zip\7za.exe a %zipfile% C:\KIMU\murrelets.gdb C:\KIMU\CSV
pause

