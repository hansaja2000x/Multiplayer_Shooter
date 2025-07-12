const express = require("express");
const app     = express();
const port    = 3000;

const server  = require("http").Server(app);
server.listen(port, () => console.log("Server listening at port " + port));

const io = require("socket.io")(server, { cors: { origin: "*" } });

// -----------------------------------------------------------------------------
function parse(json) { try { return JSON.parse(json); } catch { return {}; } }
function emitJSON(sock, evt, obj)      { sock.emit(evt, JSON.stringify(obj)); }
function roomBroadcast(code, evt, obj) { io.to(code).emit(evt, JSON.stringify(obj)); }
// -----------------------------------------------------------------------------

const rooms         = {};
const playerSize    = { x: 0.9, y: 1, z: 0.9 };
const TICK_RATE     = 60;
const MAX_PLAYERS   = 2;
let   globalBulletId = 0;

// -------- simulated object handlers --------
function degToRad(d) { return d * (Math.PI / 180); }
function getOBBAxes(rotY) {
  const r = degToRad(rotY), c = Math.cos(r), s = Math.sin(r);
  return [ { x: c,  z: s }, { x: -s, z: c } ];
}
function project(points, axis) {
  let min = Infinity, max = -Infinity;
  for (const p of points) {
    const d = p.x * axis.x + p.z * axis.z;
    if (d < min) min = d;
    if (d > max) max = d;
  }
  return { min, max };
}
function overlap(a, b) { return a.max >= b.min && b.max >= a.min; }
function getCorners(x, z, size, rotY) {
  const r = degToRad(rotY), c = Math.cos(r), s = Math.sin(r);
  const hx = size.x / 2, hz = size.z / 2;
  const base = [
    { x: -hx, z: -hz }, { x: hx, z: -hz },
    { x:  hx, z:  hz }, { x: -hx, z:  hz }
  ];
  return base.map(pt => ({ x: x + pt.x * c - pt.z * s, z: z + pt.x * s + pt.z * c }));
}
function checkOBB(a, b) {
  const aC = getCorners(a.x, a.z, a.size, a.rotationY);
  const bC = getCorners(b.x, b.z, b.size, b.rotationY);
  const axes = getOBBAxes(a.rotationY).concat(getOBBAxes(b.rotationY));
  return axes.every(ax => overlap(project(aC, ax), project(bC, ax)));
}
function checkCollision(candidate, room) {
  const obb = { ...candidate, size: playerSize };
  for (const obs of room.obstacles) {
    const obsOBB = { x: obs.x, z: obs.z, size: obs.size, rotationY: obs.rotationY || 0 };
    if (checkOBB(obb, obsOBB)) return true;
  }
  return false;
}
// -----------------------------------------------------------------------------

io.on("connection", socket => {
  let roomCode = null;

  // ---------------- createRoom ----------------
  socket.on("createRoom", raw => {
    const { name } = parse(raw);
    if (!name) return;

    roomCode = (Math.floor(1000 + Math.random() * 9000)).toString();
    rooms[roomCode] = {
      players: {}, latestInputs: {}, bullets: [],
      obstacles: [
        { x: -12.54, y: 1.1039, z: 16.4442, size: { x: 1, y: 3.2, z: 33.28 }, rotationY: 0 },
        { x: 11.87,  y: 1.1039, z: 0,       size: { x: 1, y: 3.2, z: 33.28 }, rotationY: 0 },
        { x: -0.396, y: 1.1459, z: 32.05,   size: { x: 24.65, y: 3.29, z: 1 }, rotationY: 0 },
        { x: -0.396, y: 1.1459, z: -0.488,  size: { x: 24.65, y: 3.29, z: 1 }, rotationY: 0 },
        { x: -1.0753,y: 1.097,  z: 10.0101, size: { x: 1.415, y: 2.326, z: 1.304 }, rotationY: 0 },
        { x: 5.0897, y: 1.097,  z: 14.7271, size: { x: 1.683, y: 2.326, z: 1.562 }, rotationY: 0 },
        { x: 2.1335, y: 1.1437, z: 22.028,  size: { x: 1.856, y: 2.42,  z: 2.153 }, rotationY: 0 },
        { x: -4.68,  y: 1.1437, z: 16.2,    size: { x: 1.856, y: 2.42,  z: 2.153 }, rotationY: 0 }
      ]
    };
    socket.join(roomCode);

    const p = { id: socket.id, x: 0, y: 0, z: 2, rotationY: 0,
                forward: 0, right: 0, health: 100, canShoot: true, name };
    rooms[roomCode].players[socket.id] = p;

    emitJSON(socket, "yourId",     { id: socket.id, name });
    emitJSON(socket, "roomJoined", { roomCode });
    emitJSON(socket, "init",       { players: rooms[roomCode].players });
  });

  // ---------------- joinRoom ----------------
  socket.on("joinRoom", raw => {
    const { roomCode: code, name } = parse(raw);
    const room = rooms[code];
    if (!room) return emitJSON(socket, "errorRoom", { msg: "Room not found" });
    if (Object.keys(room.players).length >= MAX_PLAYERS)
      return emitJSON(socket, "errorRoom", { msg: "Room is full" });

    roomCode = code;
    socket.join(roomCode);

    const p = { id: socket.id, x: 0, y: 0, z: 29.33, rotationY: 178.27,
                forward: 0, right: 0, health: 100, canShoot: true, name };
    room.players[socket.id] = p;

    emitJSON(socket, "yourId",     { id: socket.id, name });
    emitJSON(socket, "init",       { players: room.players });
    emitJSON(socket, "roomJoined", { roomCode });
    roomBroadcast(roomCode, "newPlayerConnected", { players: room.players });
  });

  // ---------------- move ----------------
  socket.on("move", raw => {
    const { input } = parse(raw);
    if (roomCode && rooms[roomCode]?.players[socket.id])
      rooms[roomCode].latestInputs[socket.id] = input;
    
  });

  // ---------------- shoot --------------------
  socket.on("shoot", () => {
    const room   = rooms[roomCode];
    const player = room?.players[socket.id];
    if (!player || !player.canShoot) return;

    player.canShoot = false;
    setTimeout(() => player.canShoot = true, 500);

    const rad = degToRad(player.rotationY);
    const bx  = player.x + Math.sin(rad);
    const bz  = player.z + Math.cos(rad);

    room.bullets.push({
      id: globalBulletId++, ownerId: socket.id,
      x: bx, y: player.y + 1.535, z: bz,
      rotationY: player.rotationY, lifeTime: 2.0
    });
        socket.emit("mirror", JSON.stringify({ event: "shoot", bulletId: globalBulletId }));
  });

  // ---------------- disconnect -------------
  socket.on("disconnect", () => {
    if (!roomCode || !rooms[roomCode]) return;
    const room = rooms[roomCode];

    delete room.players[socket.id];
    delete room.latestInputs[socket.id];
    roomBroadcast(roomCode, "playerDisconnected", { playerId: socket.id });

    if (Object.keys(room.players).length === 0)
      delete rooms[roomCode];
  });
});

// -----------------------------------------------------------------------------
// Main game loop
// -----------------------------------------------------------------------------
setInterval(() => {
  for (const code in rooms) {
    const room = rooms[code];
    if (!room) continue;

    // Player movement
    for (const id in room.players) {
      const p     = room.players[id];
      const input = room.latestInputs[id];
      if (!input) continue;

      const speed = 0.09;
      const rad   = degToRad(p.rotationY);
      let dx = 0, dz = 0;
      p.forward = 0; p.right = 0;

      if (input.forward)  { dx += Math.sin(rad) * speed; dz += Math.cos(rad) * speed; p.forward = 1; }
      if (input.backward) { dx -= Math.sin(rad) * speed; dz -= Math.cos(rad) * speed; p.forward = -1; }
      if (input.left)     { dx -= Math.cos(rad) * speed; dz += Math.sin(rad) * speed; p.right  = -1; }
      if (input.right)    { dx += Math.cos(rad) * speed; dz -= Math.sin(rad) * speed; p.right  = 1; }
      if (typeof input.rotationDelta === "number")
        p.rotationY = (p.rotationY + input.rotationDelta + 360) % 360;

      const candidate = { ...p, x: p.x + dx, z: p.z + dz };
      if (!checkCollision(candidate, room)) {
        p.x = candidate.x; p.z = candidate.z;
      }
    }

    // Bullet updates
    room.bullets = room.bullets.filter(b => {
      b.x += Math.sin(degToRad(b.rotationY)) * 0.25;
      b.z += Math.cos(degToRad(b.rotationY)) * 0.25;
      b.lifeTime -= 1 / TICK_RATE;

      const bulletOBB = { x: b.x, z: b.z, size: { x: 0.07, y: 0.07, z: 0.2 }, rotationY: b.rotationY };

      // Obstacle hit
      for (const obs of room.obstacles) {
        const obsOBB = { x: obs.x, z: obs.z, size: obs.size, rotationY: obs.rotationY || 0 };
        if (checkOBB(bulletOBB, obsOBB)) {
          roomBroadcast(code, "bulletHitObstacle", { bulletPos: { x: b.x, y: b.y, z: b.z } });
          roomBroadcast(code, "bulletRemove",      { bulletId: b.id });
          return false;
        }
      }

      // Player hit
      for (const tid in room.players) {
        if (tid === b.ownerId) continue;
        const t = room.players[tid];
        if (t.health <= 0) continue;

        const playerOBB = { x: t.x, z: t.z, size: playerSize, rotationY: t.rotationY };
        if (checkOBB(bulletOBB, playerOBB)) {
          t.health = Math.max(0, t.health - 20);
          roomBroadcast(code, "playerHit",   { targetId: tid, newHealth: t.health });
          roomBroadcast(code, "bulletHitObstacle", { bulletPos: { x: b.x, y: b.y, z: b.z } });
          roomBroadcast(code, "bulletRemove", { bulletId: b.id });
          if (t.health <= 0) {
            const winner = room.players[b.ownerId];
            roomBroadcast(code, "playerWon", {
              winnerId: b.ownerId, loserId: tid,
              winnerName: winner.name, loserName: t.name
            });
          }
          return false;
        }
      }
      return b.lifeTime > 0;
    });

    // Broadcast world state
    roomBroadcast(code, "stateUpdate", {
      players: room.players,
      bullets: room.bullets.map(b => ({
        id: b.id, ownerId: b.ownerId,
        x: b.x, y: b.y, z: b.z, rotationY: b.rotationY
      }))
    });
  }
}, 1000 / TICK_RATE);
