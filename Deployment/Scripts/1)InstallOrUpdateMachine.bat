@echo OFF

set destDir="%ALLUSERSPROFILE%\ESRI\ArcGIS Mobile"

if not exist %destDir% goto error1

set destDir="%ALLUSERSPROFILE%\ESRI\ArcGIS Mobile\Extensions"

if not exist %destDir% mkdir %destDir%

IF NOT EXIST %destDir% goto error2

Copy /Y /B C:\KIMU\MachineData\*.dll %destDir%
IF %ERRORLEVEL% NEQ 0 goto error3

	echo.
	echo Command completed successfully.
	echo.
	pause
	goto end

:error1
	echo.
	echo Unable to find the ArcGIS Mobile directory for all users.
	echo Install ArcGIS Mobile or Edit this script to reflect the installed location.
	echo.
	pause
	goto end
	

:error2
	echo.
	echo Unable to create the extentions directory for ArcGIS Mobile.
	echo Create the extensions directory by hand (see help for proper location).
	echo.
	pause
	goto end
	
:error3
	echo.
	echo Unable to copy all necessary files to ArcGIS Mobile.
	echo Remember, this command must be run as an administrator.
	echo.
	pause
	goto end
		
:end

