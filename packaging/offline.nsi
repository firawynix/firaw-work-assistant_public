Unicode true
!include MUI2.nsh
!include LogicLib.nsh
!include x64.nsh
!ifndef ARCH
  !error "Defina ARCH como x64 ou x86"
!endif
!ifndef VERSION
  !define VERSION "0.1.0"
!endif
Name "Firaw Work Assistant"
OutFile "..\release\Firaw-Work-Assistant-${VERSION}-${ARCH}-Setup.exe"
InstallDir "$LOCALAPPDATA\Programs\Firaw Work Assistant"
RequestExecutionLevel user
SetCompressor /SOLID lzma
Icon "..\assets\huginn-muninn.ico"
!define MUI_ICON "..\assets\huginn-muninn.ico"
!define MUI_UNICON "..\assets\huginn-muninn.ico"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\Firaw.WorkAssistant.exe"
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "PortugueseBR"
VIProductVersion "0.1.0.0"
VIAddVersionKey /LANG=1046 "ProductName" "Firaw Work Assistant"
VIAddVersionKey /LANG=1046 "CompanyName" "Firawynix"
VIAddVersionKey /LANG=1046 "FileDescription" "Assistente de trabalho"
VIAddVersionKey /LANG=1046 "FileVersion" "${VERSION}"

Function .onInit
  !if "${ARCH}" == "x64"
    ${IfNot} ${RunningX64}
      MessageBox MB_ICONSTOP "Esta versão requer Windows 64 bits."
      Abort
    ${EndIf}
  !endif
FunctionEnd

Section "Aplicativo" Core
  SectionIn RO
  SetOutPath "$INSTDIR"
  File "..\release\app-${ARCH}\Firaw.WorkAssistant.exe"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\Firaw"
  CreateShortcut "$SMPROGRAMS\Firaw\Work Assistant.lnk" "$INSTDIR\Firaw.WorkAssistant.exe"
  CreateShortcut "$DESKTOP\Firaw Work Assistant.lnk" "$INSTDIR\Firaw.WorkAssistant.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\FirawWorkAssistant" "DisplayName" "Firaw Work Assistant"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\FirawWorkAssistant" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\FirawWorkAssistant" "Publisher" "Firawynix"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\FirawWorkAssistant" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\FirawWorkAssistant" "DisplayIcon" "$INSTDIR\Firaw.WorkAssistant.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\FirawWorkAssistant" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\FirawWorkAssistant" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\FirawWorkAssistant" "NoRepair" 1
SectionEnd

Section "Desinstalar"
  Delete "$INSTDIR\Firaw.WorkAssistant.exe"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  Delete "$SMPROGRAMS\Firaw\Work Assistant.lnk"
  RMDir "$SMPROGRAMS\Firaw"
  Delete "$DESKTOP\Firaw Work Assistant.lnk"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\FirawWorkAssistant"
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "FirawWorkAssistant"
SectionEnd
