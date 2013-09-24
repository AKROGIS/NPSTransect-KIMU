@echo OFF

IF DEFINED ProgramFiles(x86) (
	set destDir="%ProgramFiles(x86)%\ArcGIS\Mobile10.0\bin"
) ELSE (
	set destDir="%ProgramFiles%\ArcGIS\Mobile10.0\bin"
)
IF NOT EXIST %destDir% goto error1


Copy /Y /B C:\KIMU\MachineData\*.dll "%ProgramFiles(x86)%\ArcGIS\Mobile10.0\bin"
IF %ERRORLEVEL% NEQ 0 goto error2

rem set mobilepy="C:\Program Files (x86)\ArcGIS\Mobile10.0\arcpy\mobile\mobile.py"
rem IF EXIST %mobilepy% Copy /Y C:\KIMU\MachineData\mobile.py %mobilepy%
rem IF %ERRORLEVEL% NEQ 0 goto error2

	echo.
	echo Command completed successfully.
	echo.
	pause
	goto end

:error1
	echo.
	echo Unable to find the installed location of ArcGIS Mobile.
	echo Install ArcGIS Mobile or Edit this script to reflect installed location.
	echo.
	pause
	goto end
	
:error2
	echo.
	echo Unable to copy all necessary files to ArcGIS Mobile.
	echo Remember, this command must be run as an administrator.
	echo.
	pause
	goto end
		
:end

