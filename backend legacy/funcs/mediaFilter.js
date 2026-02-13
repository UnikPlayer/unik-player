// backend/funcs/mediaFilter.js
const fs = require('fs');
const path = require('path');
const os = require('os');

const filterDir = path.join(os.homedir(), 'AppData', 'Local', 'UnikPlayer');
const filterFilePath = path.join(filterDir, 'media-filter.json');

// Дефолтный конфиг
const defaultConfig = {
  mode: 'allowAll',   // "allowAll" | "allowOnly" | "blockOnly"
  sources: [],         // sourceAppId для фильтра
  seenSources: []      // все когда-либо увиденные источники
};

// In-memory кэш
let filterConfig = null;

function ensureDir() {
  if (!fs.existsSync(filterDir)) {
    fs.mkdirSync(filterDir, { recursive: true });
  }
}

function loadMediaFilter() {
  if (filterConfig) return filterConfig;

  try {
    if (fs.existsSync(filterFilePath)) {
      const data = fs.readFileSync(filterFilePath, 'utf-8');
      filterConfig = { ...defaultConfig, ...JSON.parse(data) };
    } else {
      filterConfig = { ...defaultConfig };
    }
  } catch (e) {
    console.error('[MediaFilter] Error loading config:', e.message);
    filterConfig = { ...defaultConfig };
  }

  return filterConfig;
}

function saveMediaFilter(config) {
  try {
    ensureDir();
    filterConfig = { ...defaultConfig, ...config };
    fs.writeFileSync(filterFilePath, JSON.stringify(filterConfig, null, 2), 'utf-8');
    console.log('[MediaFilter] Config saved');
    return true;
  } catch (e) {
    console.error('[MediaFilter] Error saving config:', e.message);
    return false;
  }
}

/**
 * Добавить sourceAppId в seenSources (если ещё нет)
 */
function addSeenSource(sourceAppId) {
  if (!sourceAppId) return;

  const config = loadMediaFilter();
  if (!config.seenSources.includes(sourceAppId)) {
    config.seenSources.push(sourceAppId);
    saveMediaFilter(config);
    console.log('[MediaFilter] New source seen:', sourceAppId);
  }
}

/**
 * Проверить, нужно ли пропустить медиа от этого источника
 * @returns {boolean} true = пропускаем (показываем), false = блокируем
 */
function shouldAllowSource(sourceAppId) {
  const config = loadMediaFilter();

  switch (config.mode) {
    case 'allowAll':
      return true;

    case 'allowOnly':
      // Показываем только если sourceAppId в списке
      return config.sources.includes(sourceAppId);

    case 'blockOnly':
      // Показываем если sourceAppId НЕ в списке
      return !config.sources.includes(sourceAppId);

    default:
      return true;
  }
}

/**
 * Сбросить in-memory кэш (после изменения через API)
 */
function reloadFilter() {
  filterConfig = null;
  loadMediaFilter();
}

module.exports = {
  loadMediaFilter,
  saveMediaFilter,
  addSeenSource,
  shouldAllowSource,
  reloadFilter
};
