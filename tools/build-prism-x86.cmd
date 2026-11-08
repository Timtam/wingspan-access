@echo off
rem Builds Prism (https://github.com/ethindp/prism) as a 32-bit DLL for the 32-bit Wingspan process.
rem Output: third_party\prism\x86\prism.dll
rem Requirements: Visual Studio 2022+ with C++ workload, CMake, Ninja, Git.
setlocal
set PRISM_TAG=v0.18.3
set ROOT=%~dp0..
set SRC=%ROOT%\third_party\prism-src
set BLD=%ROOT%\third_party\prism-build-x86
set OUT=%ROOT%\third_party\prism\x86

for /f "usebackq tokens=*" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set VSDIR=%%i
if not defined VSDIR (echo Visual Studio with C++ tools not found & exit /b 1)

if not exist "%SRC%" git clone --depth 1 --branch %PRISM_TAG% --recurse-submodules --shallow-submodules https://github.com/ethindp/prism.git "%SRC%" || exit /b 1

call "%VSDIR%\VC\Auxiliary\Build\vcvarsall.bat" amd64_x86 || exit /b 1
cmake -S "%SRC%" -B "%BLD%" -G Ninja -DCMAKE_BUILD_TYPE=Release -DBUILD_SHARED_LIBS=ON -DPRISM_ENABLE_TESTS=OFF -DPRISM_ENABLE_DEMOS=OFF -DPRISM_ENABLE_SHIMS=OFF -DPRISM_ENABLE_LEGACY_BACKENDS=ON -DPRISM_ENABLE_GDEXTENSION=OFF || exit /b 1
cmake --build "%BLD%" || exit /b 1
if not exist "%OUT%" mkdir "%OUT%"
copy /y "%BLD%\prism.dll" "%OUT%\prism.dll" >nul || exit /b 1
echo Built %OUT%\prism.dll
