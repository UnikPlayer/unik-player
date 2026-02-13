; Unicode support
Unicode True

!define PRODUCT_NAME "UnikPlayer"
!define PRODUCT_VERSION "0.7.0.0"
!define PRODUCT_PUBLISHER "uniknow"
!define PRODUCT_WEBSITE "https://github.com/UNIKNOW0/unik-player"
!define APP_DIR "UnikPlayer"

; Paths to C# backend
!define CSHARP_BUILD "..\backend-csharp\UnikPlayer\bin\Release\net9.0-windows10.0.17763.0\win-x64\publish"
!define CSHARP_SRC "..\backend-csharp\UnikPlayer"

; Modern UI
!include "MUI2.nsh"

; MUI Settings - icons for installer
!define MUI_ICON "${CSHARP_SRC}\icon.ico"
!define MUI_UNICON "${CSHARP_SRC}\icon.ico"

Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile ".\UnikPlayer_Installer.exe"

; Icon for installer exe
Icon "${CSHARP_SRC}\icon.ico"
UninstallIcon "${CSHARP_SRC}\icon.ico"

InstallDir "$APPDATA\${APP_DIR}"
RequestExecutionLevel user

VIProductVersion "${PRODUCT_VERSION}"
VIAddVersionKey "ProductName" "${PRODUCT_NAME}"
VIAddVersionKey "ProductVersion" "${PRODUCT_VERSION}"
VIAddVersionKey "CompanyName" "${PRODUCT_PUBLISHER}"
VIAddVersionKey "LegalCopyright" "Copyright (C) 2025 ${PRODUCT_PUBLISHER}"
VIAddVersionKey "FileDescription" "UnikPlayer Installer"
VIAddVersionKey "FileVersion" "${PRODUCT_VERSION}"

; MUI Pages
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES

; Finish page - run app after install
!define MUI_FINISHPAGE_RUN "$INSTDIR\UnikPlayer.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Launch ${PRODUCT_NAME}"
!define MUI_FINISHPAGE_RUN_CHECKED
!insertmacro MUI_PAGE_FINISH

; Language
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "Russian"

Section "Main Application Files" SEC_MAIN
    SectionIn RO  ; Read-only, always installed

    ; Copy UnikPlayer.exe (C# self-contained single-file)
    SetOutPath "$INSTDIR"
    File "${CSHARP_BUILD}\UnikPlayer.exe"

    ; Copy tray icons (all from source folder)
    File "${CSHARP_SRC}\icon.ico"
    File "${CSHARP_SRC}\home.svg"
    File "${CSHARP_SRC}\exit.svg"

    ; Copy frontend build
    SetOutPath "$INSTDIR\frontBuild"
    File /r "..\frontBuild\*"

    ; Create Start Menu shortcut
    CreateDirectory "$SMPROGRAMS\${PRODUCT_NAME}"
    CreateShortCut "$SMPROGRAMS\${PRODUCT_NAME}\${PRODUCT_NAME}.lnk" "$INSTDIR\UnikPlayer.exe" "" "$INSTDIR\icon.ico" 0

    ; Registry entries for uninstall
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}" "DisplayName" "${PRODUCT_NAME}"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}" "DisplayVersion" "${PRODUCT_VERSION}"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}" "Publisher" "${PRODUCT_PUBLISHER}"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}" "URLInfoAbout" "${PRODUCT_WEBSITE}"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}" "UninstallString" "$\"$INSTDIR\uninstall.exe$\""
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}" "DisplayIcon" "$\"$INSTDIR\UnikPlayer.exe$\",0"
    WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}" "NoModify" 1
    WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}" "NoRepair" 1
    WriteUninstaller "$INSTDIR\uninstall.exe"
SectionEnd

Section "Desktop Shortcut" SEC_DESKTOP
    CreateShortCut "$DESKTOP\${PRODUCT_NAME}.lnk" "$INSTDIR\UnikPlayer.exe" "" "$INSTDIR\icon.ico" 0
SectionEnd

Section "Autostart" SEC_AUTOSTART
    ; Add to autostart with --autostart flag
    CreateShortCut "$SMSTARTUP\${PRODUCT_NAME}.lnk" "$INSTDIR\UnikPlayer.exe" "--autostart" "$INSTDIR\icon.ico" 0
SectionEnd

; Section descriptions
LangString DESC_SEC_MAIN ${LANG_ENGLISH} "Main application files (required)"
LangString DESC_SEC_DESKTOP ${LANG_ENGLISH} "Create a desktop shortcut"
LangString DESC_SEC_AUTOSTART ${LANG_ENGLISH} "Start UnikPlayer automatically when Windows starts"

LangString DESC_SEC_MAIN ${LANG_RUSSIAN} "Основные файлы приложения (обязательно)"
LangString DESC_SEC_DESKTOP ${LANG_RUSSIAN} "Создать ярлык на рабочем столе"
LangString DESC_SEC_AUTOSTART ${LANG_RUSSIAN} "Запускать UnikPlayer автоматически при старте Windows"

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SEC_MAIN} $(DESC_SEC_MAIN)
  !insertmacro MUI_DESCRIPTION_TEXT ${SEC_DESKTOP} $(DESC_SEC_DESKTOP)
  !insertmacro MUI_DESCRIPTION_TEXT ${SEC_AUTOSTART} $(DESC_SEC_AUTOSTART)
!insertmacro MUI_FUNCTION_DESCRIPTION_END

; Default selections
Function .onInit
    SectionSetFlags ${SEC_DESKTOP} 1
    SectionSetFlags ${SEC_AUTOSTART} 0
FunctionEnd

Section "Uninstall"
    ; Delete shortcuts
    Delete "$DESKTOP\${PRODUCT_NAME}.lnk"
    Delete "$SMSTARTUP\${PRODUCT_NAME}.lnk"
    RMDir /r "$SMPROGRAMS\${PRODUCT_NAME}"

    ; Delete files
    Delete "$INSTDIR\UnikPlayer.exe"
    Delete "$INSTDIR\icon.ico"
    Delete "$INSTDIR\home.svg"
    Delete "$INSTDIR\exit.svg"
    Delete "$INSTDIR\uninstall.exe"

    ; Delete frontBuild folder
    RMDir /r /REBOOTOK "$INSTDIR\frontBuild"

    ; Delete install directory
    RMDir /REBOOTOK "$INSTDIR"

    ; Delete registry
    DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}"
SectionEnd
