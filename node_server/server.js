const WebSocket = require('ws');

const server = new WebSocket.Server({ port: 3000 });
const rooms = {};
const playerSize = { x: 0.9, y: 1, z: 0.9 };
const TICK_RATE = 60;

function generateRoomCode() {
  return Math.floor(1000 + Math.random() * 9000).toString();
}

function checkAABBCollision(a, b) {
  return (
    Math.abs(a.x - b.x) < (a.size.x + b.size.x) / 2 &&
    Math.abs(a.y - b.y) < (a.size.y + b.size.y) / 2 &&
    Math.abs(a.z - b.z) < (a.size.z + b.size.z) / 2
  );
}

function checkCollision(newPos, room) {
  const playerAABB = { x: newPos.x, y: newPos.y, z: newPos.z, size: playerSize };
  for (const obs of room.obstacles) {
    const obstacleAABB = { x: obs.x, y: obs.y, z: obs.z, size: obs.size };
    if (checkAABBCollision(playerAABB, obstacleAABB)) return true;
  }
  return false;
}

function broadcastToRoom(room, data) {
  const msg = JSON.stringify(data);
  for (const sock of room.sockets || []) {
    if (sock.readyState === WebSocket.OPEN) sock.send(msg);
  }
}

// Tick
setInterval(() => {
  for (const code in rooms) {
    const room = rooms[code];
    if (!room) continue;

    for (const id in room.players) {
      const p = room.players[id];
      const input = room.latestInputs?.[id];
      if (!input) continue;

      const speed = 0.09;
      const rad = (p.rotationY * Math.PI) / 180;

      let moveX = 0, moveZ = 0;
      p.forward = 0;
      p.right = 0;
      if (input.forward)  { moveX += Math.sin(rad) * speed; moveZ += Math.cos(rad) * speed; p.forward = 1; }
      if (input.backward) { moveX -= Math.sin(rad) * speed; moveZ -= Math.cos(rad) * speed; p.forward = -1;}
      if (input.left)     { moveX -= Math.cos(rad) * speed; moveZ += Math.sin(rad) * speed; p.right = -1;}
      if (input.right)    { moveX += Math.cos(rad) * speed; moveZ -= Math.sin(rad) * speed; p.right = 1;}

      if (input.rotationDelta !== undefined) {
        p.rotationY = (p.rotationY + input.rotationDelta + 360) % 360;
      }

      let newPos = { ...p, x: p.x + moveX, z: p.z + moveZ };
      if (!checkCollision(newPos, room)) {
        p.x = newPos.x;
        p.z = newPos.z;
      }
    }

    broadcastToRoom(room, { type: 'stateUpdate', players: room.players });
  }
}, 1000 / TICK_RATE);

// Handle new WebSocket connection
server.on('connection', (ws) => {
  let roomCode = null;
  const playerId = Math.random().toString(36).substr(2, 9);

  ws.on('message', (msg) => {
    try {
      const data = JSON.parse(msg);

      if (data.type === 'createRoom') {
        roomCode = generateRoomCode();
        rooms[roomCode] = {
          players: {},
          latestInputs: {},
          sockets: [ws],
          obstacles: [
            { x: 2, y: 0, z: 2, size: { x: 1, y: 1, z: 1 } },
            { x: -1, y: 0, z: -3, size: { x: 2, y: 1, z: 2 } },
            { x: 0, y: 0, z: 5, size: { x: 1, y: 1, z: 1 } }
          ]
        };
        rooms[roomCode].players[playerId] = { id: playerId, x: 0, y: 0, z: 0, rotationY: 0 };
        ws.send(JSON.stringify({ type: 'yourId', playerId }));
        ws.send(JSON.stringify({ type: 'roomCreated', roomCode }));
        ws.send(JSON.stringify({ type: 'init', players: rooms[roomCode].players }));
      }

      else if (data.type === 'joinRoom') {
        roomCode = data.roomCode;
        const room = rooms[roomCode];
        if (!room) return ws.send(JSON.stringify({ type: 'error', msg: 'Room not found' }));

        room.players[playerId] = { id: playerId, x: 0, y: 0, z: 0, rotationY: 0 };
        room.sockets.push(ws);
        ws.send(JSON.stringify({ type: 'yourId', playerId }));
        ws.send(JSON.stringify({ type: 'init', players: room.players }));
      }

      else if (data.type === 'move') {
        const room = rooms[roomCode];
        if (!room || !room.players[playerId]) return;
        room.latestInputs[playerId] = data.input;
      }
    } catch (err) {
      console.error('Invalid message:', err);
    }
  });

  ws.on('close', () => {
    if (roomCode && rooms[roomCode]) {
      delete rooms[roomCode].players[playerId];
      delete rooms[roomCode].latestInputs[playerId];
      rooms[roomCode].sockets = rooms[roomCode].sockets.filter(s => s !== ws);
    }
  });
});

console.log('WebSocket server running on ws://localhost:3000');
