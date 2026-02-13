
const { SMTCMonitor } = require('@coooookies/windows-smtc-monitor');
const { startWebSocketServer, broadcastMediaData } = require('./ws.js');
const { characterCheck } = require('./charCheck.js');
const { addSeenSource, shouldAllowSource } = require('./mediaFilter');

// --- helper ---
function mkFingerprint(session) {
  if (!session || !session.media) return null;
  const app = (session.sourceAppId || session.appId || '').trim();
  const title = (session.media.title || '').trim();
  const artist = (session.media.artist || '').trim();
  const duration = Math.round((session.timeline && session.timeline.duration) || 0);
  return `${app}||${title}||${artist}||${duration}`;
}

function hasThumbnail(session) {
  if (!session || !session.media) return false;
  const t = session.media.thumbnail;
  if (!t) return false;
  // thumbnail может быть Buffer или строка
  if (typeof t === 'string') return t.length > 0;
  if (Buffer && Buffer.isBuffer && Buffer.isBuffer(t)) return t.length > 0;
  if (t instanceof Uint8Array) return t.length > 0;
  return !!t;
}

// --- SMTCListener ---
// Глобальная переменная для хранения активного монитора
let activeMonitor = null;

function SMTCListener() {
  // Если монитор уже запущен, не создаем новый
  if (activeMonitor) {
    console.log('SMTCListener уже запущен, пропускаем создание нового экземпляра');
    return activeMonitor;
  }

  startWebSocketServer();

  let lastFingerprint = null;
  let lastSentHadThumbnail = false;

  // map fingerprint -> retryPromise (чтобы не запускать пару ретраев одновременно)
  const retryMap = new Map();

  // retry: пытаемся получить миниатюру для fingerprint, пока не сменится трек
  async function retryThumbnailUntilFound(targetFingerprint, opts = {}) {
    if (retryMap.has(targetFingerprint)) return; // уже идёт
    const maxAttempts = opts.maxAttempts ?? 6;
    const baseDelay = opts.baseDelay ?? 300; // ms
    let attempt = 0;
    retryMap.set(targetFingerprint, true);

    try {
      while (attempt < maxAttempts) {
        attempt++;
        // ждём (экспоненциальный бэк-офф)
        const delay = baseDelay * Math.pow(1.6, attempt - 1);
        await new Promise(res => setTimeout(res, Math.round(delay)));

        // берём актуальные сессии
        const sessions = SMTCMonitor.getMediaSessions() || [];
        const active = sessions
          .filter(s => s.playback && s.playback.playbackStatus === 4)
          .filter(s => s.timeline && s.timeline.duration && s.timeline.duration > 0);

        if (!active.length) break; // ничего не играет — прекращаем
        const session = active[0];
        const fp = mkFingerprint(session);

        if (fp !== targetFingerprint) break; // трек сменили — прекращаем

        if (hasThumbnail(session)) {
          // нашли миниатюру — шлём обновление (sanitize внутри)
          broadcastMediaData(characterCheck(session));
          lastFingerprint = fp;
          lastSentHadThumbnail = true;
          break;
        }

        // иначе — повторяем до maxAttempts
      }
    } finally {
      retryMap.delete(targetFingerprint);
    }
  }

  // основной обработчик: проверяет и отправляет, а при отсутствии thumbnail запускает retry
  function handleCurrent() {
    try {
      const sessions = SMTCMonitor.getMediaSessions() || [];
      const active = sessions
        .filter(s => s.playback && s.playback.playbackStatus === 4)
        .filter(s => s.timeline && s.timeline.duration && s.timeline.duration > 0);

      if (active.length > 0) {
        // Ищем первую сессию, которая проходит фильтр
        let session = null;
        for (const s of active) {
          const appId = (s.sourceAppId || s.appId || '').trim();
          // Сохраняем все увиденные источники
          addSeenSource(appId);
          // Проверяем фильтр
          if (shouldAllowSource(appId)) {
            session = s;
            break;
          }
        }

        if (!session) {
          // Все активные сессии заблокированы фильтром
          if (lastFingerprint !== null) {
            broadcastMediaData(null);
            lastFingerprint = null;
            lastSentHadThumbnail = false;
          }
          return;
        }

        const fp = mkFingerprint(session);

        // если трек поменялся или мы раньше не слали ничего — отсылаем
        if (fp !== lastFingerprint) {
          // отправляем текущее (может быть с заглушкой thumbnail)
          broadcastMediaData(characterCheck(session));
          lastFingerprint = fp;
          lastSentHadThumbnail = hasThumbnail(session);

          // если thumbnail нет — запускаем ретрай, но только один ретрай на fingerprint
          if (!lastSentHadThumbnail) {
            retryThumbnailUntilFound(fp, { maxAttempts: 7, baseDelay: 250 });
          }
        } else {
          // fingerprint тот же — возможно мы послали без thumbnail,
          // а thumbnail появился между событиями — проверим и обновим.
          if (!lastSentHadThumbnail && hasThumbnail(session)) {
            broadcastMediaData(characterCheck(session));
            lastSentHadThumbnail = true;
          }
        }
      } else {
        // ничего не играет — шлём null только если ранее что-то было
        if (lastFingerprint !== null) {
          broadcastMediaData(null);
          lastFingerprint = null;
          lastSentHadThumbnail = false;
        }
      }
    } catch (err) {
      console.error('SMTCListener handleCurrent error:', err);
    }
  }

  // стартовая проверка
  handleCurrent();

  // монитор
  const monitor = new SMTCMonitor();
  monitor.on('session-media-changed', handleCurrent);
  monitor.on('session-playback-changed', handleCurrent);
  monitor.on('session-added', handleCurrent);

  // Сохраняем ссылку на активный монитор
  activeMonitor = monitor;

  return monitor;
}

// Функция для остановки и очистки монитора
function stopSMTCListener() {
  if (activeMonitor) {
    try {
      activeMonitor.removeAllListeners();
      activeMonitor = null;
      console.log('SMTCListener остановлен и очищен');
    } catch (err) {
      console.error('Ошибка при остановке SMTCListener:', err);
    }
  }
}

module.exports = { SMTCListener, stopSMTCListener }