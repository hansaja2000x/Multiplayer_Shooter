const WebSocket = require('ws');

const server = new WebSocket.Server({ port: 3000 });
const rooms = {};
const playerSize = { x: 0.9, y: 1, z: 0.9 };

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
          obstacles: [
            { x: 2, y: 0, z: 2, size: { x: 1, y: 1, z: 1 } },
            { x: -1, y: 0, z: -3, size: { x: 2, y: 1, z: 2 } },
            { x: 0, y: 0, z: 5, size: { x: 1, y: 1, z: 1 } }
          ]
        };
        // Add creator player at start position with rotation
        rooms[roomCode].players[playerId] = { id: playerId, x: 0, y: 0, z: 0, rotationY: 0 };

        // Inform client about room and their playerId
        ws.send(JSON.stringify({ type: 'yourId', playerId }));
        ws.send(JSON.stringify({ type: 'roomCreated', roomCode }));
        ws.send(JSON.stringify({ type: 'init', players: rooms[roomCode].players }));
      }

      if (data.type === 'joinRoom') {
        roomCode = data.roomCode;
        const room = rooms[roomCode];
        if (!room) return ws.send(JSON.stringify({ type: 'error', msg: 'Room not found' }));

        room.players[playerId] = { id: playerId, x: 0, y: 0, z: 0, rotationY: 0 };
        ws.send(JSON.stringify({ type: 'yourId', playerId }));
        ws.send(JSON.stringify({ type: 'init', players: room.players }));
      }

      if (data.type === 'move') {
        const room = rooms[roomCode];
        if (!room || !room.players[playerId]) return;

        const input = data.input;
        let p = room.players[playerId];
        let newPos = { ...p };
        const speed = 0.05;

        // Update rotationY from input.rotationDelta (mouse X movement)
        if (input.rotationDelta !== undefined) {
          p.rotationY = (p.rotationY + input.rotationDelta) % 360;
          if (p.rotationY < 0) p.rotationY += 360;
        }

        // Calculate movement relative to rotationY
        const rad = (p.rotationY * Math.PI) / 180;

        let moveX = 0;
        let moveZ = 0;
        if (input.forward) {
          moveX += Math.sin(rad) * speed;
          moveZ += Math.cos(rad) * speed;
        }
        if (input.backward) {
          moveX -= Math.sin(rad) * speed;
          moveZ -= Math.cos(rad) * speed;
        }
        if (input.left) {
          moveX -= Math.cos(rad) * speed;
          moveZ += Math.sin(rad) * speed;
        }
        if (input.right) {
          moveX += Math.cos(rad) * speed;
          moveZ -= Math.sin(rad) * speed;
        }

        newPos.x += moveX;
        newPos.z += moveZ;

        if (!checkCollision(newPos, room)) {
          room.players[playerId].x = newPos.x;
          room.players[playerId].z = newPos.z;
          room.players[playerId].rotationY = p.rotationY;
        }

        // Send updated state
        ws.send(JSON.stringify({ type: 'stateUpdate', players: room.players }));
        ws.send(JSON.stringify({ type: 'mirror', input, position: room.players[playerId] }));
      }
    } catch (err) {
      console.error('Invalid message:', err);
    }
  });

  ws.on('close', () => {
    if (roomCode && rooms[roomCode]) {
      delete rooms[roomCode].players[playerId];
    }
  });
});

console.log('WebSocket server running on ws://localhost:3000');
