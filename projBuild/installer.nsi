; Unicode support
Unicode True

!define PRODUCT_NAME "UnikPlayer"
!define PRODUCT_VERSION "0.6.9.0"
!define PRODUCT_PUBLISHER "uniknow"
!define PRODUCT_WEBSITE "https://github.com/UNIKNOW0/unik-player"
!define APP_DIR "UnikPlayer"

; Modern UI
!include "MUI2.nsh"

; MUI Settings - иконки для установщика
!define MUI_ICON "..\backend\static\trayIcon.ico"
!define MUI_UNICON "..\backend\static\trayIcon.ico"

Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile ".\UnikPlayer_Installer.exe"

; Иконка для самого .exe файла установщика
Icon "..\backend\static\trayIcon.ico"
UninstallIcon "..\backend\static\trayIcon.ico"

InstallDir "$APPDATA\${APP_DIR}"
RequestExecutionLevel user

VIProductVersion "${PRODUCT_VERSION}" ; <-- Версия продукта, 4 числа
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

; Language
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "Russian"

Section "Main Application Files" SEC_MAIN
    SectionIn RO  ; Read-only, always installed

    ; Копируем UnikPlayer.exe в корневую директорию установки
    SetOutPath "$INSTDIR"
    File "..\backend\UnikPlayer.exe"

    ; Копируем VBS файлы для запуска
    File "..\backend\UnikPlayer-NoConsole.vbs"
    File "..\backend\UnikPlayer-Autostart.vbs"

    ; Копируем node_modules (только необходимые нативные модули)
    SetOutPath "$INSTDIR\node_modules"
    File /r "..\backend\node_modules\@coooookies"
    File /r "..\backend\node_modules\systray"

    ; Копируем статические файлы (иконка)
    SetOutPath "$INSTDIR\static"
    File /r "..\backend\static\*"

    ; Создаем ярлык в меню "Пуск" (всегда создается)
    CreateDirectory "$SMPROGRAMS\${PRODUCT_NAME}"
    CreateShortCut "$SMPROGRAMS\${PRODUCT_NAME}\${PRODUCT_NAME}.lnk" "$INSTDIR\UnikPlayer-NoConsole.vbs" "" "$INSTDIR\static\trayIcon.ico" 0

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
    ; Создаем ярлык на рабочем столе с иконкой
    CreateShortCut "$DESKTOP\${PRODUCT_NAME}.lnk" "$INSTDIR\UnikPlayer-NoConsole.vbs" "" "$INSTDIR\static\trayIcon.ico" 0
SectionEnd

Section "Autostart" SEC_AUTOSTART
    ; Добавляем в автозагрузку
    CreateShortCut "$SMSTARTUP\${PRODUCT_NAME}.lnk" "$INSTDIR\UnikPlayer-Autostart.vbs" "" "$INSTDIR\static\trayIcon.ico" 0
SectionEnd

; Описания секций
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

; Выбираем Desktop Shortcut по умолчанию, но не Autostart
Function .onInit
    ; Desktop shortcut включен по умолчанию
    SectionSetFlags ${SEC_DESKTOP} 1
    ; Autostart выключен по умолчанию
    SectionSetFlags ${SEC_AUTOSTART} 0
FunctionEnd

Section "Uninstall"
    ; Удаляем ярлыки сначала
    Delete "$DESKTOP\${PRODUCT_NAME}.lnk"
    Delete "$SMSTARTUP\${PRODUCT_NAME}.lnk"
    RMDir /r "$SMPROGRAMS\${PRODUCT_NAME}"

    ; Удаляем все файлы и папки
    Delete "$INSTDIR\UnikPlayer.exe"
    Delete "$INSTDIR\UnikPlayer-NoConsole.vbs"
    Delete "$INSTDIR\UnikPlayer-Autostart.vbs"
    Delete "$INSTDIR\uninstall.exe"

    ; Удаляем папки с содержимым
    RMDir /r /REBOOTOK "$INSTDIR\node_modules"
    RMDir /r /REBOOTOK "$INSTDIR\static"

    ; Удаляем саму директорию установки
    RMDir /REBOOTOK "$INSTDIR"

    ; Удаляем из реестра
    DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}"
SectionEnd