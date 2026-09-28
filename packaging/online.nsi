Unicode true
!include MUI2.nsh
Name "Firaw Work Assistant - Instalador online"
OutFile "..\release\Firaw-Work-Assistant-Instalador-Online.exe"
RequestExecutionLevel user
SetCompressor /SOLID lzma
Icon "..\assets\huginn-muninn.ico"
!define MUI_ICON "..\assets\huginn-muninn.ico"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_LANGUAGE "PortugueseBR"
VIProductVersion "0.1.0.0"
VIAddVersionKey /LANG=1046 "ProductName" "Firaw Work Assistant"
VIAddVersionKey /LANG=1046 "CompanyName" "Firawynix"
VIAddVersionKey /LANG=1046 "FileDescription" "Instalador online Firaw Work Assistant"
VIAddVersionKey /LANG=1046 "FileVersion" "0.1.0"
Section "Instalar" Core
  SectionIn RO
  InitPluginsDir
  SetOutPath "$PLUGINSDIR"
  File "download-install.ps1"
  DetailPrint "Baixando o pacote para esta arquitetura..."
  nsExec::ExecToStack '"$SYSDIR\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$PLUGINSDIR\download-install.ps1"'
  Pop $0
  Pop $1
  ${If} $0 != 0
    MessageBox MB_ICONSTOP "A instalação não foi concluída. Consulte %TEMP%\FirawWorkAssistant-install-error.txt."
    SetErrorLevel 1
    Abort
  ${EndIf}
SectionEnd
