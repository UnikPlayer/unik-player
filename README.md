<p align="center">
  <img width="541" height="183" alt="Снимок экрана 2026-01-15 100101" src="https://github.com/user-attachments/assets/1210ccc5-1d73-416f-ab32-c63790be01fd" />
</p>

---

# UnikPlayer

**Музыкальный виджет для OBS, который показывает текущий трек с любого приложения на вашем устройстве**

[![Windows](https://img.shields.io/badge/Platform-Windows-0078D6?style=flat-square&logo=windows)](https://github.com/UNIKNOW0/unik-player)
[![Release](https://img.shields.io/github/v/release/UNIKNOW0/unik-player?style=flat-square&color=green)](https://github.com/UNIKNOW0/unik-player/releases)
[![License](https://img.shields.io/badge/License-WTFPL-blue?style=flat-square)](LICENSE)

---

https://github.com/user-attachments/assets/b2710d57-1137-494b-a54d-e566e40b7385


## Возможности

- **Универсальный захват** — работает с любым музыкальным приложением (Spotify, Яндекс.Музыка, VK Music, браузер и др.)
- **Интеграция с OBS** — готовый виджет для стримов
- **Кастомизация** — настройка внешнего вида под ваш стиль
- **Лёгкий** — минимальное потребление ресурсов
- **Авто-обновление** — автоматическое определение смены трека

---

## Вариации плееров

<img width="518" height="172" alt="image" src="https://github.com/user-attachments/assets/0796aa66-2e62-498f-87bb-9860b2715606" />

<img width="518" height="172" alt="image" src="https://github.com/user-attachments/assets/7bb229e4-20f2-4e9f-88cd-b638ea819f47" />

<img width="518" height="172" alt="image" src="https://github.com/user-attachments/assets/002314dc-a1a6-4dff-9c84-42552066fc90" />

<img width="518" height="172" alt="image" src="https://github.com/user-attachments/assets/d167507d-1a90-4387-a5a1-58abad732dc7" />

<img width="518" height="172" alt="image" src="https://github.com/user-attachments/assets/ef7eac36-33c2-48b9-bf4d-e4dcf02ea8c8" />

---

## Требования для разработки

### Системные требования
- Windows 10/11 (x64)
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 18+](https://nodejs.org/)

### Зависимости

**Frontend (Svelte/SvelteKit):**
```bash
cd frontend
npm install
```

**Backend (C#/.NET 9):**
Зависимости устанавливаются автоматически при сборке через NuGet:
- `Dubya.WindowsMediaController` - для работы с Windows SMTC
- `System.Text.Json` - сериализация JSON

---

## Разработка

### Dev Mode

В dev mode данные хранятся локально в `dev-data/` вместо `%LOCALAPPDATA%\UnikPlayer\`.

**Настройка:**
```bash
# Скопировать .env.example в .env (уже сделано по умолчанию)
cp backend-csharp/UnikPlayer/.env.example backend-csharp/UnikPlayer/.env
```

**`.env` конфигурация:**
```env
DEV_MODE=true              # true = локальные пути, false = %LOCALAPPDATA%
DEV_DATA_DIR=../../dev-data  # Путь к dev данным
```

**Структура dev-data:**
```
dev-data/
├── player-styles.json    # Настройки плееров (цвет, шрифт)
└── css/
    ├── BackPicture.css   # CSS для каждого плеера
    ├── BigHead.css
    ├── Generic.css
    ├── Separate.css
    └── Square.css
```

### Быстрый старт

**Терминал 1 - Backend (C#):**
```bash
cd backend-csharp/UnikPlayer
dotnet run
```
Запустится HTTP сервер на `http://localhost:27272` и WebSocket на `ws://localhost:62727`

**Терминал 2 - Frontend (с hot reload):**
```bash
cd frontend
npm run dev
```
Запустится dev сервер на `http://localhost:5173`

### Порты
| Сервис | Порт |
|--------|------|
| HTTP (статика) | 27272 |
| WebSocket | 62727 |
| Vite Dev Server | 5173 |

---

## CSS Кастомизация

Каждый плеер имеет свой CSS файл в `dev-data/css/` (dev) или `%LOCALAPPDATA%\UnikPlayer\css\` (prod).

### Важно: стилизация текста

Для стилизации текста используйте `.className *` вместо просто `.className`:

```css
/* ❌ Неправильно - не сработает */
.title {
  font-size: 2rem;
}

/* ✅ Правильно - marquee создаёт дочерние элементы */
.title * {
  font-size: 2rem;
  color: var(--lightVibrant);
}
```

### CSS переменные цветов

Доступны переменные из обложки альбома (или статический цвет):

| Переменная | Описание |
|------------|----------|
| `var(--vibrant)` | Основной яркий цвет |
| `var(--lightVibrant)` | Светлый яркий |
| `var(--darkVibrant)` | Тёмный яркий |
| `var(--muted)` | Приглушённый |
| `var(--lightMuted)` | Светлый приглушённый |
| `var(--darkMuted)` | Тёмный приглушённый (фон) |

### Структура плееров

| Плеер | Классы |
|-------|--------|
| **Generic** | `.mainDiv` > `.picDiv` > `.pic` + `.textDiv` > `.title *` + `.artist *` |
| **BigHead** | `.mainDiv` > `.picDiv` > `.pic` + `.textDiv` > `.title *` + `.artist *` |
| **Square** | `.mainDiv` > `.mainDivGlow` + `.textDiv` > `.blurDiv` + `.title *` + `.artist *` |
| **Separate** | `.mainDiv` > `.picDiv` + `.textDiv` > `.titleDiv` > `.title *` + `.artistDiv` > `.artist *` |
| **BackPicture** | `.mainDiv` > `.mainDivGlow` + `.textDiv` > `.blurDiv` + `.title *` + `.artist *` |

---

## Сборка

Полная сборка до установщика:

npm run build:all

# 1. Frontend
cd frontend && npm run build && cd ..

# 2. C# Backend (publish self-contained single-file)
cd backend-csharp/UnikPlayer
dotnet publish -c Release
cd ../..

# 3. NSIS Installer
makensis projBuild/installer.nsi


### 1. Сборка Frontend

```bash
cd frontend
npm run build
```
Результат: статические файлы в папке `frontBuild/`

### 2. Сборка Backend (Release)

```bash
cd backend-csharp/UnikPlayer
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
Результат: `bin/Release/net9.0-windows10.0.17763.0/win-x64/publish/UnikPlayer.exe`

### 3. Полная сборка (EXE)

```bash
# 1. Собрать фронтенд
cd frontend
npm run build

# 2. Собрать backend
cd ../backend-csharp/UnikPlayer
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# 3. Скопировать frontBuild рядом с exe
```

Финальная структура для распространения:
```
UnikPlayer/
├── UnikPlayer.exe
├── icon.ico (опционально)
└── frontBuild/
    ├── index.html
    └── ...
```

### 4. Создание установщика (Inno Setup)

1. Скачать [Inno Setup](https://jrsoftware.org/isinfo.php)
2. Использовать скрипт `projBuild/installer.iss` (если есть) или создать свой:

```iss
[Setup]
AppName=UnikPlayer
AppVersion=0.7.0
DefaultDirName={autopf}\UnikPlayer
DefaultGroupName=UnikPlayer
OutputDir=output
OutputBaseFilename=UnikPlayer-Setup
Compression=lzma2
SolidCompression=yes

[Files]
Source: "UnikPlayer.exe"; DestDir: "{app}"
Source: "icon.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "frontBuild\*"; DestDir: "{app}\frontBuild"; Flags: recursesubdirs

[Icons]
Name: "{group}\UnikPlayer"; Filename: "{app}\UnikPlayer.exe"
Name: "{autodesktop}\UnikPlayer"; Filename: "{app}\UnikPlayer.exe"

[Run]
Filename: "{app}\UnikPlayer.exe"; Flags: nowait postinstall skipifsilent
```

3. Скомпилировать скрипт в Inno Setup → получится `UnikPlayer-Setup.exe`

---

## Технологии

| Компонент | Технология |
|-----------|------------|
| Frontend | SvelteKit 2, Vite 7 |
| Backend | C# / .NET 9 |
| SMTC | Dubya.WindowsMediaController |
| Стили | SCSS, Flowbite |

---

## Структура проекта

```
unikPlayer/
├── backend-csharp/     # C# backend (основной)
│   └── UnikPlayer/
│       ├── Program.cs
│       └── UnikPlayer.csproj
├── frontend/           # Svelte frontend
│   ├── src/
│   │   ├── lib/
│   │   │   ├── players/      # Компоненты плееров
│   │   │   ├── stores/       # Svelte stores
│   │   │   └── ws.js         # WebSocket клиент
│   │   └── routes/
│   └── package.json
├── frontBuild/         # Собранный фронтенд
├── projBuild/          # Файлы для сборки установщика
└── README.md
```

---

## Лицензия

This project is open source and available under the [WTFPL](https://www.wtfpl.net/).
