const express = require("express");
const app     = express();
const port    = 3000;
app.use(express.json());

const server  = require("http").Server(app);
//server.listen(port, () => console.log("Server listening at port " + port));
server.listen(port, "0.0.0.0", () => console.log("Server listening at http://0.0.0.0:" + port));

const io = require("socket.io")(server, { cors: { origin: "*" } });

// -----------------------------------------------------------------------------
function parse(json) { try { return JSON.parse(json); } catch { return {}; } }
function emitJSON(sock, evt, obj)      { sock.emit(evt, JSON.stringify(obj)); }
function roomBroadcast(code, evt, obj) { io.to(code).emit(evt, JSON.stringify(obj)); }
// -----------------------------------------------------------------------------

const rooms         = {};
const playerSize    = { x: 0.9, y: 2, z: 0.9 };
const TICK_RATE     = 60;
const MAX_PLAYERS   = 2;
let   globalBulletId = 0;
const disconnectTimeouts = {};

// -------- simulated object handlers --------
function degToRad(d) { return d * (Math.PI / 180); }
function getOBBAxes(rotY) {
  const r = degToRad(rotY);
  const c = Math.cos(r), s = Math.sin(r);
  return [
    { x:  c, y: 0, z:  s },  // Right
    { x:  0, y: 1, z:  0 },  // Up
    { x: -s, y: 0, z:  c }   // Forward
  ];
}
// Get all 8 corners of a 3D OBB box
function getCorners(x, y, z, size, rotY) {
  const hx = size.x / 2, hy = size.y / 2, hz = size.z / 2;
  const r = degToRad(rotY);
  const c = Math.cos(r), s = Math.sin(r);

  const right   = { x:  c, y: 0, z:  s };
  const forward = { x: -s, y: 0, z:  c };
  const up      = { x:  0, y: 1, z:  0 };

  const corners = [];

  for (const dx of [-1, 1]) {
    for (const dy of [-1, 1]) {
      for (const dz of [-1, 1]) {
        corners.push({
          x: x + dx * hx * right.x + dy * hy * up.x + dz * hz * forward.x,
          y: y + dx * hx * right.y + dy * hy * up.y + dz * hz * forward.y,
          z: z + dx * hx * right.z + dy * hy * up.z + dz * hz * forward.z
        });
      }
    }
  }

  return corners;
}
function project(points, axis) {
  let min = Infinity, max = -Infinity;
  for (const p of points) {
    const d = p.x * axis.x + p.y * axis.y + p.z * axis.z;
    if (d < min) min = d;
    if (d > max) max = d;
  }
  return { min, max };
}
function overlap(a, b) {
  return a.max >= b.min && b.max >= a.min;
}
function checkOBB(a, b) {
  const aC = getCorners(a.x, a.y, a.z, a.size, a.rotationY);
  const bC = getCorners(b.x, b.y, b.z, b.size, b.rotationY);
  const axes = getOBBAxes(a.rotationY).concat(getOBBAxes(b.rotationY));

  for (const axis of axes) {
    const projA = project(aC, axis);
    const projB = project(bC, axis);
    if (!overlap(projA, projB)) return false; // No overlap = no collision
  }

  return true; // All axes overlap = collision
}
function checkCollision(candidate, room) {
  const obb = { ...candidate, size: playerSize };
  for (const obs of room.obstacles) {
    const obsOBB = {
      x: obs.x,
      y: obs.y,
      z: obs.z,
      size: obs.size,
      rotationY: obs.rotationY || 0
    };
    if (checkOBB(obb, obsOBB)) return true;
  }
  return false;
}
// -----------------------------------------------------------------------------

// API endpoint for room creation (via HTTP POST)
app.post("/api/createRoom", (req, res) => {
  try {
    const { room, players } = req.body;
    const roomCode = room.gameSessionUuid;

    if (rooms[roomCode]) {
      return res.status(400).json({ status: false, message: "Game session already exists" });
    }

    // Setup room
    rooms[roomCode] = {
      players: {},
      latestInputs: {},
      bullets: [],
      obstacles: [
        { x: -12.54, y: 1.1039, z: 16.4442, size: { x: 1, y: 3.2, z: 33.28 }, rotationY: 0 },
        { x: 11.87,  y: 1.1039, z: 0,       size: { x: 1, y: 3.2, z: 33.28 }, rotationY: 0 },
        { x: -0.396, y: 1.1459, z: 32.05,   size: { x: 24.65, y: 3.29, z: 1 }, rotationY: 0 },
        { x: -0.396, y: 1.1459, z: -0.488,  size: { x: 24.65, y: 3.29, z: 1 }, rotationY: 0 },
        { x: -1.0753,y: 1.097,  z: 10.0101, size: { x: 1.415, y: 2.326, z: 1.304 }, rotationY: 0 },
        { x: 5.0897, y: 1.097,  z: 14.7271, size: { x: 1.683, y: 2.326, z: 1.562 }, rotationY: 0 },
        { x: 2.1335, y: 1.1437, z: 22.028,  size: { x: 1.856, y: 2.42,  z: 2.153 }, rotationY: 0 },
        { x: -4.68,  y: 1.1437, z: 16.2,    size: { x: 1.856, y: 2.42,  z: 2.153 }, rotationY: 0 }
      ],
      movingObstacles: [
        { id: 0, x: 0.13,  y: 1.1437, z: 15.04,  size: { x: 1.856, y: 2.42,  z: 2.153 }, rotationY: 0, speed: 0.1 },
        { id: 1, x: 7.94,  y: 1.1437, z: 15.04,  size: { x: 1.856, y: 2.42,  z: 2.153 }, rotationY: 0, speed: 0.15 }
      ],
      allowedPlayers: players.map(p => p.uuid),
      isPlaying: false
    };

    const responseData = {
      status: true,
      message: "success",
      payload: {
        gameSessionUuid: roomCode,
        gameStateId: roomCode,
        name: room.name,
        createDate: new Date(),
        link1: `http://192.168.1.3:8000/?gameSessionUuid=${roomCode}&gameStateId=${roomCode}&uuid=${players[0].uuid}`,
        link2: `http://192.168.1.3:8000/?gameSessionUuid=${roomCode}&gameStateId=${roomCode}&uuid=${players[1]?.uuid || ""}`,
      }
    };

    rooms[roomCode].allowedPlayers = players.map(p => p.uuid);
    rooms[roomCode].playerInfo = players.reduce((acc, player) => {
      acc[player.uuid] = player.name;
      return acc;
    }, {});
    res.status(200).json(responseData);
  } catch (err) {
    console.error("Error creating room:", err);
    res.status(500).json({ error: "Server error" });
  }
});


io.on("connection", socket => {
  let roomCode = null;


  // ---------------- joinRoom ----------------
  socket.on("joinRoom", raw => {
    const { roomCode: code, uuId } = parse(raw);
    const room = rooms[code];

    if (!room) return emitJSON(socket, "errorRoom", { msg: "Room not found" });

    if (!room.allowedPlayers.includes(uuId))
      return emitJSON(socket, "errorRoom", { msg: "Player not allowed in this room" });

    // Reconnection
    const existingPlayerId = Object.keys(room.players).find(
      id => room.players[id].uuId === uuId && room.players[id].disconnected
    );

    if(existingPlayerId){ 
      roomCode = code;
      socket.join(roomCode);
      const p = {
        id: socket.id,
        x:  room.players[existingPlayerId].x,
        y:  room.players[existingPlayerId].y,
        z:  room.players[existingPlayerId].z,
        rotationY:  room.players[existingPlayerId].rotationY,
        forward: 0,
        right: 0,
        health:  room.players[existingPlayerId].health,
        canShoot: true,
        uuId: uuId,
        name: room.playerInfo[uuId],
        disconnected: false
      };
      room.players[socket.id] = p;
    // Wait until 2 players have joined before starting game
      if (Object.keys(room.players).length >= MAX_PLAYERS) {
        // Send to both players
        for (const playerId in room.players) {
          const s = io.sockets.sockets.get(playerId);
          if (s) {
            emitJSON(s, "yourId", { id: s.id, name: room.players[s.id].name });
            emitJSON(s, "init", { players: room.players });
            emitJSON(s, "roomJoined", { roomCode });
          }
        }

        roomBroadcast(roomCode, "newPlayerConnected", { players: room.players });
        room.isPlaying = true;
      }
      delete room.players[existingPlayerId];
        
      roomBroadcast(roomCode, "playerDisconnected", { playerId: existingPlayerId });

      if (Object.keys(room.players).length === 0) {
        delete rooms[roomCode];
      }
    }
    else if (Object.keys(room.players).length > MAX_PLAYERS){
      return emitJSON(socket, "errorRoom", { msg: "Room is full" });
    }else{
      roomCode = code;
      socket.join(roomCode);

      // Determine spawn position based on current number of players
      const numPlayers = Object.keys(room.players).length;
      const spawnZ = numPlayers === 0 ? 29.33 : 2;

      const p = {
        id: socket.id,
        x: 0,
        y: 1,
        z: spawnZ,
        rotationY: 178.27,
        forward: 0,
        right: 0,
        health: 100,
        canShoot: true,
        uuId: uuId,
        name: room.playerInfo[uuId],
        disconnected: false
      };

      room.players[socket.id] = p;
    // Wait until 2 players have joined before starting game
    if (Object.keys(room.players).length >= MAX_PLAYERS) {
      // Send to both players
      for (const playerId in room.players) {
        const s = io.sockets.sockets.get(playerId);
        if (s) {
          emitJSON(s, "yourId", { id: s.id, name: room.players[s.id].name });
          emitJSON(s, "init", { players: room.players });
          emitJSON(s, "roomJoined", { roomCode });
        }
      }

      roomBroadcast(roomCode, "newPlayerConnected", { players: room.players });
      room.isPlaying = true;
    }
   }

    
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
      x: bx, y: player.y + 0.535, z: bz,
      rotationY: player.rotationY, lifeTime: 2.0
    });
        socket.emit("mirror", JSON.stringify({ event: "shoot", bulletId: globalBulletId }));
  });

  // ---------------- disconnect -------------
  socket.on("disconnect", () => {
    if (!roomCode || !rooms[roomCode]) return;
    const room = rooms[roomCode];
    const player = room.players[socket.id];
    if (!player) return;

    // Mark player as temporarily disconnected
    player.disconnected = true;

    delete room.latestInputs[socket.id];
    disconnectTimeouts[socket.id] = setTimeout(() => {
      if (room.players[socket.id]?.disconnected) {
        delete room.players[socket.id];       
        remainingPlayerId = Object.keys(room.players)[0];
        remainingPlayer = room.players[remainingPlayerId];
        roomBroadcast(roomCode, "playerWon", {
              winnerId: remainingPlayerId, loserId: socket.id,
              winnerName: room.players[remainingPlayerId].name, loserName: "null"
        });
        delete rooms[roomCode];
      }
    }, 10000); // 10 seconds
  });

});

// -----------------------------------------------------------------------------
// Main game loop
// -----------------------------------------------------------------------------
setInterval(() => {
  for (const code in rooms) {
    const room = rooms[code];
    if (!room || room.isPlaying == false) continue;

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

      const candidate = { ...p, x: p.x + dx, y: p.y, z: p.z + dz };
      if (!checkCollision(candidate, room)) {
        p.x = candidate.x; p.z = candidate.z;
      }
    }

    // Bullet updates
    room.bullets = room.bullets.filter(b => {
      b.x += Math.sin(degToRad(b.rotationY)) * 0.25;
      b.z += Math.cos(degToRad(b.rotationY)) * 0.25;
      b.lifeTime -= 1 / TICK_RATE;

      const bulletOBB = { x: b.x, y: b.y, z: b.z, size: { x: 0.07, y: 0.07, z: 0.2 }, rotationY: b.rotationY };

      // Obstacle hit
      for (const obs of room.obstacles) {
        const obsOBB = { x: obs.x, y: obs.y, z: obs.z, size: obs.size, rotationY: obs.rotationY || 0 };
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

        const playerOBB = { x: t.x, y: t.y, z: t.z, size: playerSize, rotationY: t.rotationY };
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
            delete room;
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
