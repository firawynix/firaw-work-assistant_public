Unicode true
!include MUI2.nsh
!include LogicLib.nsh
Name "Firaw Work Assistant - Instalador online"
OutFile "..\release\Firaw-Work-Assistant-Instalador-Online.exe"
InstallDir "$LOCALAPPDATA\Programs\Firaw Work Assistant"
RequestExecutionLevel user
SetCompressor /SOLID lzma
Icon "..\assets\huginn-muninn.ico"
!define MUI_ICON "..\assets\huginn-muninn.ico"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\Firaw.WorkAssistant.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Abrir Firaw Work Assistant agora"
!define MUI_FINISHPAGE_RUN_NOTCHECKED
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_LANGUAGE "PortugueseBR"
VIProductVersion "0.1.4.0"
VIAddVersionKey /LANG=1046 "ProductName" "Firaw Work Assistant"
VIAddVersionKey /LANG=1046 "CompanyName" "Firawynix"
VIAddVersionKey /LANG=1046 "FileDescription" "Instalador online Firaw Work Assistant"
VIAddVersionKey /LANG=1046 "FileVersion" "0.1.4"
Section "Instalar" Core
  SectionIn RO
  InitPluginsDir
  SetOutPath "$PLUGINSDIR"
  File "download-install.ps1"
  ; O Center usa /D= para escolher a pasta. O processo filho precisa receber
  ; a mesma pasta; variáveis do processo são herdadas pelo PowerShell.
  System::Call 'kernel32::SetEnvironmentVariableW(w "FIRAW_WORK_INSTALL_DIR", w "$INSTDIR")i.r0'
  ${If} $0 == 0
    MessageBox MB_ICONSTOP "Não foi possível preparar a pasta de instalação." /SD IDOK
    SetErrorLevel 1
    Abort
  ${EndIf}
  DetailPrint "Baixando o pacote para esta arquitetura..."
  nsExec::ExecToStack '"$SYSDIR\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$PLUGINSDIR\download-install.ps1"'
  Pop $0
  Pop $1
  ${If} $0 != 0
    MessageBox MB_ICONSTOP "A instalação não foi concluída. Consulte $TEMP\FirawWorkAssistant-install-error.txt." /SD IDOK
    SetErrorLevel 1
    Abort
  ${EndIf}
SectionEnd
