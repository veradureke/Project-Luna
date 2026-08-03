@echo off
REM run_luna.bat — one file, run this every time.

set PYTHON_EXE="C:\Users\vdureke\.local\bin\python3.14.exe"
set ADB_PATH="C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"

echo ==========================================
echo LUNA AI Server
echo ==========================================

if not exist venv (
    echo First run detected, creating virtual environment...
    if not exist %PYTHON_EXE% (
        echo ERROR: Could not find Python at %PYTHON_EXE%
        echo Update PYTHON_EXE at the top of this script.
        pause
        exit /b 1
    )
    %PYTHON_EXE% -m venv venv
)

call venv\Scripts\activate.bat

REM Skip the pip check entirely once we know it's already installed —
REM this is what actually saves time on every run after the first,
REM rather than re-running pip's dependency resolution each time.
if not exist venv\.installed (
    echo Installing required packages, this can take several minutes the
    echo first time ^(openai-whisper pulls in torch, which is large^)...
    pip install -r requirements.txt --quiet
    if %ERRORLEVEL% EQU 0 (
        echo done > venv\.installed
    ) else (
        echo Package install failed, see errors above.
        pause
        exit /b 1
    )
) else (
    echo Packages already installed, skipping check.
)

if not exist .env (
    copy .env.example .env
    echo Created .env — opening it now, paste in your real API key and save.
    notepad .env
    echo.
    echo Press any key once you've saved your API key in the Notepad window...
    pause
)

echo.
echo [1/2] Connecting to Quest 3 over USB...
%ADB_PATH% reverse tcp:8000 tcp:8000

if %ERRORLEVEL% NEQ 0 (
    echo Could not connect to the Quest. Check it's plugged in via USB
    echo and USB debugging is allowed, then try again.
    pause
    exit /b 1
)

echo.
echo [2/2] Starting server...
python server.py

pause
