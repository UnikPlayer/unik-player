// funcs/ws.js
const WebSocket = require('ws');
const { WebSocketServer } = WebSocket;

let wss = null;
let currentMediaData = null;

function startWebSocketServer(port = 62727) {
  if (wss) {
    console.log('[ws] WebSocket сервер уже запущен');
    return wss;
  }

  wss = new WebSocketServer({ port });
  console.log(`[ws] WebSocket сервер поднят на ws://localhost:${port}`);

  wss.on('connection', (socket) => {
    console.log('[ws] Новый клиент подключился');
    if (currentMediaData !== null) {
      try { socket.send(JSON.stringify(currentMediaData)); }
      catch (err) { console.error('[ws] Ошибка при отправке initial mediaData:', err); }
    }

    socket.on('error', (err) => console.error('[ws] Ошибка в соединении с клиентом:', err));
    socket.on('close', () => console.log('[ws] Клиент отключился'));
  });

  return wss;
}

function broadcastMediaData(mediaData) {
  if (!wss) {
    console.warn('[ws] WebSocket сервер не запущен — нечего рассылать');
    return;
  }
  currentMediaData = mediaData;
  const msg = JSON.stringify(mediaData);

  wss.clients.forEach(client => {
    if (client.readyState === WebSocket.OPEN) {
      try { client.send(msg); }
      catch (err) { console.error('[ws] Ошибка при отправке mediaData клиенту:', err); }
    }
  });
}

module.exports = { broadcastMediaData, startWebSocketServer }