const WebSocket = require('ws');

const server = new WebSocket.Server({ port: 3000 });
const rooms = {};
const playerSize = { x: 0.9, y: 1, z: 0.9 };
const TICK_RATE = 60;
let globalBulletId = 0;

function generateRoomCode() {
  return Math.floor(1000 + Math.random() * 9000).toString();
}

// --- Rotation-Based OBB Collision System (XZ-plane only) ---
function degToRad(deg) {
  return deg * (Math.PI / 180);
}

function getOBBAxes(rotationY) {
  const rad = degToRad(rotationY);
  const cos = Math.cos(rad);
  const sin = Math.sin(rad);

  return [
    { x: cos, z: sin },     // local right
    { x: -sin, z: cos }     // local forward
  ];
}

function projectOntoAxis(points, axis) {
  let min = Infinity;
  let max = -Infinity;
  for (const p of points) {
    const dot = p.x * axis.x + p.z * axis.z;
    if (dot < min) min = dot;
    if (dot > max) max = dot;
  }
  return { min, max };
}

function isOverlapping(projA, projB) {
  return projA.max >= projB.min && projB.max >= projA.min;
}

function getOBBCorners(x, z, size, rotationY) {
  const rad = degToRad(rotationY);
  const cos = Math.cos(rad);
  const sin = Math.sin(rad);

  const hx = size.x / 2;
  const hz = size.z / 2;

  const corners = [
    { x: -hx, z: -hz },
    { x: hx, z: -hz },
    { x: hx, z: hz },
    { x: -hx, z: hz },
  ];

  return corners.map(corner => ({
    x: x + corner.x * cos - corner.z * sin,
    z: z + corner.x * sin + corner.z * cos
  }));
}

function checkOBBCollision(a, b) {
  const aCorners = getOBBCorners(a.x, a.z, a.size, a.rotationY);
  const bCorners = getOBBCorners(b.x, b.z, b.size, b.rotationY);

  const axes = [
    ...getOBBAxes(a.rotationY),
    ...getOBBAxes(b.rotationY),
  ];

  for (const axis of axes) {
    const projA = projectOntoAxis(aCorners, axis);
    const projB = projectOntoAxis(bCorners, axis);
    if (!isOverlapping(projA, projB)) return false;
  }

  return true;
}

// --- Main collision check using rotation ---
function checkCollision(newPos, room) {
  const playerOBB = {
    x: newPos.x,
    z: newPos.z,
    size: playerSize,
    rotationY: newPos.rotationY
  };

  for (const obs of room.obstacles) {
    const obstacleOBB = {
      x: obs.x,
      z: obs.z,
      size: obs.size,
      rotationY: obs.rotationY || 0
    };
    if (checkOBBCollision(playerOBB, obstacleOBB)) return true;
  }

  return false;
}

function getOBBCollisionData(x, z, size, rotationY) {
  return {
    x, z, size, rotationY
  };
}

function broadcastToRoom(room, data){
  const msg = JSON.stringify(data);
  room.sockets.forEach(s=>{
    if(s.readyState===WebSocket.OPEN) s.send(msg);
  });
}

// --- Ticker ---
setInterval(() => {
  for (const code in rooms) {
    const room = rooms[code];
    if (!room) continue;

    // --- Player movement ---
    for (const id in room.players) {
      const p = room.players[id];
      const input = room.latestInputs?.[id];
      if (!input) continue;

      const speed = 0.09;
      const rad = degToRad(p.rotationY);
      let moveX = 0, moveZ = 0;
      p.forward = 0; p.right = 0;

      if (input.forward)  { moveX += Math.sin(rad) * speed; moveZ += Math.cos(rad) * speed; p.forward = 1; }
      if (input.backward) { moveX -= Math.sin(rad) * speed; moveZ -= Math.cos(rad) * speed; p.forward = -1; }
      if (input.left)     { moveX -= Math.cos(rad) * speed; moveZ += Math.sin(rad) * speed; p.right = -1; }
      if (input.right)    { moveX += Math.cos(rad) * speed; moveZ -= Math.sin(rad) * speed; p.right = 1; }

      if (input.rotationDelta !== undefined) {
        p.rotationY = (p.rotationY + input.rotationDelta + 360) % 360;
      }

      const candidate = { ...p, x: p.x + moveX, z: p.z + moveZ };
      if (!checkCollision(candidate, room)) {
        p.x = candidate.x;
        p.z = candidate.z;
      }
    }

    // --- Bullet simulation ---
    const bulletSpeed = 0.25;
    room.bullets = room.bullets.filter(bullet => {
      bullet.x += Math.sin(degToRad(bullet.rotationY)) * bulletSpeed;
      bullet.z += Math.cos(degToRad(bullet.rotationY)) * bulletSpeed;
      bullet.lifeTime -= 1 / TICK_RATE;

      const bulletOBB = getOBBCollisionData(bullet.x, bullet.z, { x: 0.07, y: 0.07, z: 0.2 }, bullet.rotationY);

      // --- Obstacle Collision ---
      for (const obs of room.obstacles) {
        const obsOBB = getOBBCollisionData(obs.x, obs.z, obs.size, obs.rotationY || 0);
        if (checkOBBCollision(bulletOBB, obsOBB)) {
          broadcastToRoom(room, { type: 'bulletHitObstacle', bulletPos: { x: bullet.x, y: bullet.y, z: bullet.z } });
          broadcastToRoom(room, { type: 'bulletRemove', bulletId: bullet.id });
          return false;
        }
      }

      // --- Player Collision ---
      for (const targetId in room.players) {
        if (targetId === bullet.ownerId) continue;
        const t = room.players[targetId];
        if (t.health <= 0) continue;

        const targetOBB = getOBBCollisionData(t.x, t.z, playerSize, t.rotationY);
        if (checkOBBCollision(bulletOBB, targetOBB)) {
          t.health = Math.max(0, t.health - 20);
          if(t.health <= 0){
            const winner = room.players[bullet.ownerId];
            const loser = room.players[targetId];
           // broadcastToRoom(room, { type: 'playerDead', targetId});
            broadcastToRoom(room, {
              type: 'playerWon',
              winnerId: bullet.ownerId,
              loserId: targetId,
              winnerName: winner?.name ?? 'Unknown',
              loserName: loser?.name ?? 'Unknown'
            });
          }
          broadcastToRoom(room, { type: 'playerHit', targetId, newHealth: t.health });
          broadcastToRoom(room, { type: 'bulletHitObstacle', bulletPos: { x: bullet.x, y: bullet.y, z: bullet.z } });
          broadcastToRoom(room, { type: 'bulletRemove', bulletId: bullet.id });
          return false;
        }
      }

      return bullet.lifeTime > 0;
    });

    // --- Broadcast game state ---
    broadcastToRoom(room, {
      type: 'stateUpdate',
      players: room.players,
      bullets: room.bullets.map(b => ({
        id: b.id,
        ownerId: b.ownerId,
        x: b.x,
        y: b.y,
        z: b.z,
        rotationY: b.rotationY
      }))
    });
  }
}, 1000 / TICK_RATE);

// --- Connection handler ---
server.on('connection', ws => {
  let roomCode = null;
  const playerId = Math.random().toString(36).substr(2, 9);

  ws.on('message', msg => {
    try {
      const data = JSON.parse(msg);

      if (data.type === 'createRoom') {
        roomCode = generateRoomCode();
        rooms[roomCode] = {
          players: {},
          latestInputs: {},
          sockets: [ws],
          bullets: [],
          obstacles: [
            { x: -12.54, y: 1.1039, z: 16.4442, size: { x: 1, y: 3.2078, z: 33.2798 }, rotationY: 0 },
            { x: 11.87, y: 1.1039, z: 0, size: { x: 1, y: 3.2078, z: 33.27982 }, rotationY: 0 },
            { x: -0.396, y: 1.1459, z: 32.05, size: { x: 24.65454, y: 3.2919, z: 1 }, rotationY: 0 },
            { x: -0.396, y: 1.1459, z: -0.488, size: { x: 24.65454, y: 3.2919, z: 1 }, rotationY: 0 },
            { x: -1.0753, y: 1.097, z: 10.0101, size: { x: 1.415077, y: 2.326, z: 1.304252 }, rotationY: 0 },
            { x: 5.0897, y: 1.097, z: 14.7271, size: { x: 1.683031, y: 2.326, z: 1.561712 }, rotationY: 0 },
            { x: 2.1335, y: 1.1437, z: 22.028, size: { x: 1.856079, y: 2.419505, z: 2.153355 }, rotationY: 0 },
            { x: -4.68, y: 1.1437, z: 16.2, size: { x: 1.856079, y: 2.419505, z: 2.153355 }, rotationY: 0 }
          ]
        };

        const player = { id: playerId, x: 0, y: 0, z: 2, rotationY: 0, forward: 0, right: 0, health: 100, canShoot: true, name: data.name };
        rooms[roomCode].players[playerId] = player;
        playerName = player.name;
        ws.send(JSON.stringify({ type: 'yourId', id: playerId, name: playerName }));
        ws.send(JSON.stringify({ type: 'roomJoined', roomCode }));
        ws.send(JSON.stringify({ type: 'init', players: rooms[roomCode].players }));
      }

      else if (data.type === 'joinRoom') {
        roomCode = data.roomCode;
        const room = rooms[roomCode];
        if (!room) {
          ws.send(JSON.stringify({ type: 'noRoom' }))
          return ws.send(JSON.stringify({ type: 'error', msg: 'Room not found' }));
        }

        const player = { id: playerId, x: 0, y: 0, z: 29.33, rotationY: 178.27, forward: 0, right: 0, health: 100, canShoot: true, name: data.name };
        room.players[playerId] = player;
        room.sockets.push(ws);

        playerName = player.name;
        ws.send(JSON.stringify({ type: 'yourId', id: playerId, name: playerName }));
        ws.send(JSON.stringify({ type: 'init', players: room.players }));
        ws.send(JSON.stringify({ type: 'roomJoined', roomCode }));
        broadcastToRoom(room, { type: 'newPlayerConnected', players: room.players });
      }

      else if (data.type === 'move') {
        const room = rooms[roomCode];
        if (!room || !room.players[playerId]) return;
        room.latestInputs[playerId] = data.input;
      }

      else if (data.type === 'shoot') {
        const room = rooms[roomCode];
        const player = room?.players?.[playerId];
        if (!player || !player.canShoot) return;

        player.canShoot = false;
        setTimeout(() => { player.canShoot = true; }, 500);

        const rad = degToRad(player.rotationY);
        const bx = player.x + Math.sin(rad);
        const bz = player.z + Math.cos(rad);

        const bulletId = globalBulletId++;
        room.bullets.push({
          id: bulletId,
          x: bx,
          y: player.y + 1.535,
          z: bz,
          rotationY: player.rotationY,
          ownerId: playerId,
          lifeTime: 2.0
        });
      }

    } catch (e) {
      console.error("Invalid message:", e);
    }
  });

  ws.on('close', () => {
    if (roomCode && rooms[roomCode]) {
      const room = rooms[roomCode];
      delete room.players[playerId];
      delete room.latestInputs[playerId];
      room.sockets = room.sockets.filter(s => s !== ws);
    }
  });
});

console.log("WebSocket server running on ws://localhost:3000");