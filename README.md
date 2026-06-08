<div align="center">
  
# UnikPlayer
  
https://github.com/user-attachments/assets/94860bf0-9891-4df4-84d2-5b1d13636a4e

[![Release](https://img.shields.io/github/v/release/UNIKNOW0/unik-player?style=for-the-badge&label=VERSION)](https://github.com/UNIKNOW0/unik-player/releases/latest)
[![Windows](https://img.shields.io/badge/PLATFORM-Windows%2010%2F11-0078D6?style=for-the-badge&logo=windows)](https://github.com/UNIKNOW0/unik-player)
[![License](https://img.shields.io/badge/LICENSE-WTFPL-blue?style=for-the-badge)](LICENSE)

<br/>

# [**СКАЧАТЬ**](https://github.com/UNIKNOW0/unik-player/releases/latest/download/UnikPlayer_Installer.exe)

<br/>

</div>



### UnikPlayer — десктопный медиа-виджет для Windows, который захватывает информацию о текущем треке из **любого** приложения и показывает его на стриме через OBS.

## Возможности

- Универсальный захват медиа — работает с любым SMTC-совместимым приложением
- Кастомизация визуала — изменение цветов, шрифтов, CSS каждого плеера
- Встроенные варианты оформления — несколько готовых плееров из коробки
- Кастомные HTML-плееры — создание собственных плееров на HTML/CSS
- Интеграция с OBS — подключение через Browser Source
- Автообновление — фоновая проверка и тихая установка обновлений
- Системный трей — приложение работает в фоне
- Media Filter — фильтрация источников по приложениям

## Вариации плееров

<img width="518" height="172" alt="Players" src="https://github.com/user-attachments/assets/0796aa66-2e62-498f-87bb-9860b2715606" />
<img width="518" height="172" alt="Players" src="https://github.com/user-attachments/assets/7bb229e4-20f2-4e9f-88cd-b638ea819f47" />
<img width="518" height="172" alt="Players" src="https://github.com/user-attachments/assets/002314dc-a1a6-4dff-9c84-42552066fc90" />
<img width="518" height="172" alt="Players" src="https://github.com/user-attachments/assets/d167507d-1a90-4387-a5a1-58abad732dc7" />
<img width="518" height="172" alt="Players" src="https://github.com/user-attachments/assets/ef7eac36-33c2-48b9-bf4d-e4dcf02ea8c8" />

---

## Системные требования

- Windows 10/11 (x64)

## Разработка

### Зависимости

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 18+](https://nodejs.org/)
- [NSIS](https://nsis.sourceforge.io/) (для сборки установщика)

### Быстрый старт

**1. Установить зависимости фронтенда:**
```bash
cd frontend
npm install
```

**2. Запустить бэкенд (C#):**
```bash
cd backend-csharp/UnikPlayer
dotnet run
```

**3. Запустить фронтенд (отдельный терминал):**
```bash
cd frontend
npm run dev
```

**4. Открыть в браузере:**
```
http://localhost:5173
```

**Адрес сайта для разработки и собранного проекта отличается!**

### Dev Mode

В dev mode данные хранятся локально в `dev-data/` вместо `%LOCALAPPDATA%\UnikPlayer`.

Конфигурация в `backend-csharp/UnikPlayer/.env`:
```
DEV_MODE=true
DEV_DATA_DIR=../../dev-data
```

Структура данных:
```
dev-data/
  player-styles.json    -- настройки плееров (цвет, шрифт)
  css/
    BackPicture.css
    BigHead.css
    Generic.css
    Separate.css
    Square.css
  custom/               -- кастомные HTML-плееры
  media-filter.json     -- фильтр источников
```

### Порты

| Сервис           | Порт  |
|------------------|-------|
| HTTP (статика)   | 27272 |
| WebSocket        | 62727 |
| Vite Dev Server  | 5173  |

### Полная сборка

```bash
npm run build:all
```

Эта команда:
1. Пишет git-хеш текущего коммита в `frontBuild/_app/version.json`
2. Собирает фронтенд (SvelteKit -> статика в `frontBuild/`)
3. Собирает бэкенд (.NET -> `UnikPlayer.exe`)
4. Собирает установщик (NSIS -> `UnikPlayer_Installer.exe`)

Ручные шаги по отдельности:

```bash
# Фронтенд
cd frontend && npm run build

# Бэкенд
cd backend-csharp/UnikPlayer
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# Установщик
cd projBuild
makensis installer.nsi
```

## Структура проекта

```
unikPlayer/
  backend-csharp/           -- C# бэкенд
    UnikPlayer/
      Program.cs            -- HTTP, WebSocket, SMTC, трей, автообновление
      Logger.cs
      UnikPlayer.csproj
      .env
  frontend/                 -- SvelteKit фронтенд
    src/
      lib/
        components/         -- Svelte компоненты
        players/            -- компоненты плееров
        stores/             -- Svelte stores
      routes/               -- страницы
    static/                 -- статические ассеты
      guide.json            -- шаги интерактивного гайда
      hands/                -- изображения рук для гайда
      tts/                  -- аудиофайлы для гайда
  frontBuild/               -- собранный фронтенд (отдаётся бэкендом)
  projBuild/
    installer.nsi           -- NSIS скрипт установщика
    static/
      icon.ico
      icon_update.ico       -- иконка для трея при доступном обновлении
```

## Технологии

| Компонент     | Технология                               |
|---------------|------------------------------------------|
| Frontend      | SvelteKit 2, Svelte 5, Vite 7            |
| Backend       | C#, .NET 9, HttpListener, WebSockets     |
| SMTC          | Dubya.WindowsMediaController             |
| Стили         | SCSS, Flowbite Svelte                    |
| Установщик    | NSIS                                     |
| Иконки        | Svg.NET                                  |

## Лицензия

Этот проект распространяется под лицензией [WTFPL](https://www.wtfpl.net/).
