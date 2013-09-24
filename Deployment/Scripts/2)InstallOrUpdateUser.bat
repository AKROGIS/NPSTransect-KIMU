@echo off

if not defined LOCALAPPDATA goto error1

if not exist %LOCALAPPDATA%\ESRI mkdir %LOCALAPPDATA%\ESRI

if not exist "%LOCALAPPDATA%\ESRI\ArcGIS Mobile" mkdir "%LOCALAPPDATA%\ESRI\ArcGIS Mobile"

if not exist "%LOCALAPPDATA%\ESRI\ArcGIS Mobile" goto error2

rem do recursive copy
rem Copy /Y /B C:\KIMU\UserData\*.* "%LOCALAPPDATA%\ESRI\ArcGIS Mobile"
rem see http://ss64.com/nt/robocopy-exit.html for robocopy exit codes 
robocopy C:\KIMU\UserData "%LOCALAPPDATA%\ESRI\ArcGIS Mobile" /MIR
IF %ERRORLEVEL% GTR 7 goto error3

rem use python to sync the DB schema in the local map cache
C:\Python26\ArcGIS10.0\python.exe C:\KIMU\Scripts\Tools\_syncSchema.py %LOCALAPPDATA%
IF %ERRORLEVEL% NEQ 0 goto error4

rem get rid of extra project file created in previous step
del "%LOCALAPPDATA%\ESRI\ArcGIS Mobile\P_murrelet\P_murrelet.amp"
IF %ERRORLEVEL% NEQ 0 goto error5

	echo.
	echo Command completed successfully.
	echo.
	pause
	goto end

:error1
	echo.
	echo Local Application Directory doesn't exist.
	echo Edit script to reflect location of user's ArcGIS Mobile data.
	echo.
	pause
	goto end

:error2
	echo.
	echo Unable to create User's ArcGIS Mobile directories.
	echo Check permissions and/or edit script.
	echo.
	pause
	goto end
		
:error3
	echo.
	echo Unable to copy project files to the user's local cache.
	echo Check permissions and/or edit script.
	echo.
	pause
	goto end
		
:error4
	echo.
	echo Unable to run the python script to sync the database schema.
	echo You must have ArcGIS 10 and the Mobile geoprocessing tools installed.
	echo.
	pause
	goto end
	
:error4
	echo.
	echo Unable to delete the extra project file.  
	echo Ignore the project called P_murrelet.
	echo.
	pause
	goto end
		
:end