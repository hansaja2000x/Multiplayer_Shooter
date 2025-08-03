const express = require("express");
const app = express();
const port = 3000;
app.use(express.json());
const cors = require('cors');
const axios = require('axios');
const dotenv = require('dotenv');
const path = require('path');
const Room = require('./schemas/roomSchema');
require('./config/database');
dotenv.config();
const SAFA_BACKEND_URL = process.env.SAFA_BACKEND_URL;
const server = require("http").Server(app);
server.listen(port, () => console.log("Server listening at " + port));
const io = require("socket.io")(server, { cors: { origin: "*" } });
// -----------------------------------------------------------------------------
function parse(json) { try { return JSON.parse(json); } catch { return {}; } }
function emitJSON(sock, evt, obj) { sock.emit(evt, JSON.stringify(obj)); }
function roomBroadcast(code, evt, obj) { io.to(code).emit(evt, JSON.stringify(obj)); }
function getCharacterKeyFromUrl(url) {
  if (!url) return '';
  try {
    const fileName = path.basename(url);
    return path.parse(fileName).name; // Extracts "Ravex_M" from "Ravex_M.png"
  } catch (error) {
    console.error("Error parsing avatar URL:", error);
    return '';
  }
}
// -----------------------------------------------------------------------------
const rooms = {};
const playerSize = { x: 0.9, y: 2, z: 0.9 };
const TICK_RATE = 60;
const MAX_PLAYERS = 2;
let globalBulletId = 0;
const disconnectTimeouts = {};
// obstacle array
const movingObstacleSets = [
  [
    { id: 0, x: 0.13, y: -1.185, z: 15.04, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0.01, startPoint: -1.185, endPoint: 1.43, prefabType: 0 },
    { id: 1, x: 7.94, y: -1.185, z: 15.04, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0.015, startPoint: -1.185, endPoint: 1.43, prefabType: 1 },
    { id: 2, x: -1.0753, y: 1.1437, z: 10.0101, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0, startPoint: 0, endPoint: 0, prefabType: 0 },
    { id: 3, x: 5.0897, y: 1.1437, z: 14.7271, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0, startPoint: 0, endPoint: 0, prefabType: 1 },
    { id: 4, x: 2.1335, y: 1.1437, z: 22.028, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0, startPoint: 0, endPoint: 0, prefabType: 0 },
    { id: 5, x: -4.68, y: 1.1437, z: 16.2, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0, startPoint: 0, endPoint: 0, prefabType: 1 },
  ],
  [
    { id: 0, x: -7.58, y: -1.185, z: 21.39, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0.01, startPoint: -1.185, endPoint: 1.43, prefabType: 1 },
    { id: 1, x: -1.59, y: -1.185, z: 15.18, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0.015, startPoint: -1.185, endPoint: 1.43, prefabType: 1 },
    { id: 2, x: 3.9, y: -1.185, z: 10.23, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0.015, startPoint: -1.185, endPoint: 1.43, prefabType: 0 },
    { id: 3, x: -4.09, y: 1.1437, z: 9.25, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0, startPoint: 0, endPoint: 0, prefabType: 0 },
    { id: 4, x: 4.89, y: 1.1437, z: 19.24, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0, startPoint: 0, endPoint: 0, prefabType: 1 },
    { id: 5, x: -9.26, y: 1.1437, z: 9.99, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0, startPoint: 0, endPoint: 0, prefabType: 1 },
  ],
  [
    { id: 0, x: -9.7, y: -1.185, z: 16.71, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0.01, startPoint: -1.185, endPoint: 1.43, prefabType: 1 },
    { id: 1, x: -1.41, y: -1.185, z: 16.72, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0.015, startPoint: -1.185, endPoint: 1.43, prefabType: 1 },
    { id: 2, x: 7.8, y: -1.185, z: 16.69, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0.015, startPoint: -1.185, endPoint: 1.43, prefabType: 0 },
    { id: 3, x: -5.76, y: 1.1437, z: 13.22, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0, startPoint: 0, endPoint: 0, prefabType: 0 },
    { id: 4, x: 2.85, y: 1.1437, z: 13.44, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0, startPoint: 0, endPoint: 0, prefabType: 1 },
    { id: 5, x: -5.58, y: 1.1437, z: 20.78, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0, startPoint: 0, endPoint: 0, prefabType: 0 },
    { id: 6, x: 3.27, y: 1.1437, z: 20.78, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0, startPoint: 0, endPoint: 0, prefabType: 1 },
  ],
  [
    { id: 0, x: 6.6066, y: 1.1437, z: 5.1004, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0, startPoint: 0, endPoint: 0, prefabType: 1 },
    { id: 1, x: -9.5756, y: 1.1437, z: 5.4982, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0, startPoint: 0, endPoint: 0, prefabType: 1 },
    { id: 2, x: -6.352, y: 1.1437, z: 9.9735, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0, startPoint: 0, endPoint: 0, prefabType: 1 },
    { id: 3, x: -8.9864, y: 1.1437, z: 20.0617, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0, startPoint: 0, endPoint: 0, prefabType: 1 },
    { id: 4, x: -3.4993, y: 1.1437, z: 20.2428, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0, startPoint: 0, endPoint: 0, prefabType: 1 },
  ],
  [
    { id: 0, x: -5.7557, y: 1.1437, z: 20.1993, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0, startPoint: 0, endPoint: 0, prefabType: 1 },
    { id: 1, x: 2.815, y: -1.185, z: 11.4557, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0.014, startPoint: -1.185, endPoint: 1.43, prefabType: 0 },
    { id: 2, x: -6.9516, y: 1.1437, z: 14.5641, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0, startPoint: 0, endPoint: 0, prefabType: 1 },
    { id: 3, x: 6.1585, y: 1.1437, z: 8.4176, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0, startPoint: 0, endPoint: 0, prefabType: 1 },
    { id: 4, x: -9.1371, y: -1.185, z: 24.4719, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0.013, startPoint: -1.185, endPoint: 1.43, prefabType: 1 },
  ],
  [
    { id: 0, x: 9.9076, y: -1.185, z: 12.8731, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0.012, startPoint: -1.185, endPoint: 1.43, prefabType: 0 },
    { id: 1, x: -3.4662, y: 1.1437, z: 8.8358, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0, startPoint: 0, endPoint: 0, prefabType: 0 },
    { id: 2, x: -7.9684, y: 1.1437, z: 18.972, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0, startPoint: 0, endPoint: 0, prefabType: 0 },
    { id: 3, x: 8.4, y: 1.1437, z: 5.0679, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0, startPoint: 0, endPoint: 0, prefabType: 0 },
    { id: 4, x: -1.2403, y: 1.1437, z: 24.5683, size: { x: 1.856, y: 2.42, z: 2.153 }, rotationY: 0, speed: 0, startPoint: 0, endPoint: 0, prefabType: 0 },
  ],
];
//-------- simulated object handlers --------
function degToRad(d) { return d * (Math.PI / 180); }
function getOBBAxes(rotY) {
  const r = degToRad(rotY);
  const c = Math.cos(r), s = Math.sin(r);
  return [
    { x: c, y: 0, z: s }, // Right
    { x: 0, y: 1, z: 0 }, // Up
    { x: -s, y: 0, z: c } // Forward
  ];
}
// Get all 8 corners of a 3D OBB box
function getCorners(x, y, z, size, rotY) {
  const hx = size.x / 2, hy = size.y / 2, hz = size.z / 2;
  const r = degToRad(rotY);
  const c = Math.cos(r), s = Math.sin(r);
  const right = { x: c, y: 0, z: s };
  const forward = { x: -s, y: 0, z: c };
  const up = { x: 0, y: 1, z: 0 };
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
  let onMovingObstacle = null;
  let topCollision = false;
  let withinObstacleArea = null; // Track if player is within x-z area of an obstacle
  // Check static obstacles (walls)
  for (const obs of room.obstacles) {
    const obsOBB = {
      x: obs.x,
      y: obs.y,
      z: obs.z,
      size: obs.size,
      rotationY: obs.rotationY || 0
    };
    if (checkOBB(obb, obsOBB)) return { collision: true };
  }
  // Check moving obstacles for x-z area overlap (no collision required)
  for (const mob of room.movingObstacles) {
    // Define the x-z bounds of the obstacle
    const halfSizeX = mob.size.x / 2;
    const halfSizeZ = mob.size.z / 2;
    const minX = mob.x - halfSizeX;
    const maxX = mob.x + halfSizeX;
    const minZ = mob.z - halfSizeZ;
    const maxZ = mob.z + halfSizeZ;
    // Check if player's x-z position is within the obstacle's x-z area
    if (candidate.x >= minX && candidate.x <= maxX && candidate.z >= minZ && candidate.z <= maxZ) {
      withinObstacleArea = mob; // Player is within this obstacle's x-z area
      break; // Only assign one obstacle (first match)
    }
  }
  // Check moving obstacles for collision (for top detection and other collisions)
  for (const mob of room.movingObstacles) {
    const mobOBB = {
      x: mob.x,
      y: mob.y,
      z: mob.z,
      size: mob.size,
      rotationY: mob.rotationY || 0
    };
    if (checkOBB(obb, mobOBB)) {
      const playerBottom = candidate.y - playerSize.y / 2;
      const obstacleTop = mob.y + mob.size.y / 2;
      const yDiff = Math.abs(playerBottom - obstacleTop);
      if (yDiff < 0.15 && playerBottom >= obstacleTop - 0.15) {
        // Player is on top of the obstacle (collision-based)
        onMovingObstacle = mob;
        topCollision = true;
      } else {
        // Collision with obstacle (not on top)
        return { collision: true };
      }
    }
  }
  return { collision: false, onMovingObstacle, topCollision, withinObstacleArea };
}
function resetRound(room) {
  const playerIds = Object.keys(room.players);
  if (playerIds.length !== 2) return;
  const spawnPositions = [
    { z: 29.33, rotationY: 180 },
    { z: 2, rotationY: 0 }
  ];
  const offset = (room.currentRound % 2 === 0) ? 1 : 0;
  // Broadcast bullet removal for all existing bullets
  for (const bullet of room.bullets) {
    roomBroadcast(room.gameSessionUuid, "bulletRemove", { bulletId: bullet.id });
  }
  // Clear bullets
  room.bullets = [];
  for (let i = 0; i < 2; i++) {
    const p = room.players[playerIds[i]];
    const spawn = spawnPositions[(i + offset) % 2];
    p.x = 0;
    p.y = 1;
    p.z = spawn.z;
    p.rotationY = spawn.rotationY;
    p.rotationX = -169.2;
    p.forward = 0;
    p.right = 0;
    p.health = 100;
    p.canShoot = true;
    p.isOnObstacle = false; // Reset obstacle state
    p.currentObstacle = null; // Reset current obstacle
    p.isFalling = false; // Reset falling state
  }
  for (const mob of room.movingObstacles) {
    mob.y = mob.startPoint;
    mob.direction = 1;
  }
}
// -----------------------------------------------------------------------------
// API endpoint for room creation (via HTTP POST)
app.post("/api/createRoom", async (req, res) => {
  try {
    const { room, players } = req.body;
    const roomCode = room.gameSessionUuid;
    // Check if room already exists in memory
    if (rooms[roomCode]) {
      return res.status(400).json({ status: false, message: "Game session already exists" });
    }
    // Check if room already exists in DB
    const existingRoom = await Room.findOne({ gameSessionUuid: roomCode });
    if (existingRoom) {
      return res.status(400).json({ status: false, message: "Game session already exists" });
    }
    // Pick a random moving obstacle set
    const randomSet = movingObstacleSets[Math.floor(Math.random() * movingObstacleSets.length)];
    // Setup room in memory
    rooms[roomCode] = {
      players: {},
      latestInputs: {},
      bullets: [],
      obstacles: [
        { id: 0, x: -12.54, y: 1.1039, z: 16.4442, size: { x: 1, y: 3.2, z: 33.28 }, rotationY: 0, prefabType: 0 },
        { id: 1, x: 11.87, y: 1.1039, z: 16.4442, size: { x: 1, y: 3.2, z: 33.28 }, rotationY: 0, prefabType: 0 },
        { id: 2, x: -0.396, y: 1.1459, z: 32.05, size: { x: 24.65, y: 3.29, z: 1 }, rotationY: 0, prefabType: 0 },
        { id: 3, x: -0.396, y: 1.1459, z: -0.488, size: { x: 24.65, y: 3.29, z: 1 }, rotationY: 0, prefabType: 0 },
      ], // walls
      movingObstacles: randomSet,
      allowedPlayers: players.map(p => p.uuid),
      isPlaying: false,
      winnerDataSent: false,
      currentRound: 1,
      maxRounds: 5, // Updated to 5 rounds
      roundWins: players.reduce((acc, p) => { acc[p.uuid] = 0; return acc; }, {}),
      gameStartTime: 0,
      activeTime: 0, // NEW: Accumulated active gameplay time
      lastActiveTimestamp: 0, // NEW: For delta calculation
      waitingForCountdown: 0, // NEW: Count of players needed to ready after countdown
      waitingTimer: null, // NEW: Timer for waiting opponent
      waitingStart: 0 // NEW: Start time for waiting
    };
    // Save to MongoDB
    const newRoom = new Room({
      gameSessionUuid: roomCode,
      players: players.map(p => ({
        name: p.name,
        uuid: p.uuid,
        profileImage: p.profileImage || '',
        ready: false,
      }))
    });
    await newRoom.save();
    const responseData = {
      status: true,
      message: "success",
      payload: {
        gameSessionUuid: roomCode,
        gameStateId: roomCode,
        name: room.name,
        createDate: new Date(),
        link1: `http://192.168.1.12:8000/?gameSessionUuid=${roomCode}&gameStateId=${roomCode}&uuid=${players[0].uuid}`,
        link2: `http://192.168.1.12:8000/?gameSessionUuid=${roomCode}&gameStateId=${roomCode}&uuid=${players[1]?.uuid || ""}`,
      }
    };
    rooms[roomCode].allowedPlayers = players.map(p => p.uuid);
    rooms[roomCode].playerInfo = players.reduce((acc, player) => {
      acc[player.uuid] = {
        name: player.name,
        profileImage: player.profileImage || '',
        characterKey: getCharacterKeyFromUrl(player.profileImage)
      };
      return acc;
    }, {});
    res.status(200).json(responseData);
  } catch (err) {
    console.error("Error creating room:", err);
    res.status(500).json({ error: "Server error" });
  }
});
function startCountdown(code) {
  const room = rooms[code];
  if (!room) return;
  let countdownPhase = 4; // 3,2,1,Start!
  room.countdownInterval = setInterval(() => {
    countdownPhase--;
    let text = '';
    if (countdownPhase === 3) text = '3';
    else if (countdownPhase === 2) text = '2';
    else if (countdownPhase === 1) text = '1';
    else if (countdownPhase === 0) text = 'Start!';
    roomBroadcast(code, "countdown", { text });
    if (countdownPhase < 0) {
      clearInterval(room.countdownInterval);
      room.countdownInterval = null;
      room.latestInputs = {};
      room.isPlaying = true;
      room.lastActiveTimestamp = Date.now();
      if (room.gameStartTime === 0) {
        room.gameStartTime = Date.now();
      }
      roomBroadcast(code, "roundBegin", {});
    }
  }, 1000);
}
async function updateRoomInDB(code, winnerData, isDraw = false) {
  try {
    const room = rooms[code];
    const remainingTime = Math.max(0, 300 - Math.floor(room.activeTime / 1000));
    await Room.findOneAndUpdate(
      { gameSessionUuid: code },
      {
        $set: {
          gameEndTime: new Date(),
          roundWins: room.roundWins,
          isDraw,
          remainingTime,
          players: winnerData.players.map(p => ({
            uuid: p.uuid,
            name: room.playerInfo[p.uuid]?.name || '',
            profileImage: room.playerInfo[p.uuid]?.profileImage || '',
            ready: false,
            status: p.userGameSessionStatus
          }))
        }
      },
      { new: true }
    );
    console.log(`Room ${code} updated in database with gameEndTime, roundWins, isDraw, remainingTime, and player statuses`);
  } catch (error) {
    console.error(`Error updating room ${code} in database:`, error);
  }
}
function handleWaitingTimeout(code) {
  const room = rooms[code];
  if (!room || room.winnerDataSent) return;
  const playerCount = Object.keys(room.players).length;
  if (playerCount === 1) {
    const playerId = Object.keys(room.players)[0];
    const player = room.players[playerId];
    const absentUuid = room.allowedPlayers.find(u => u !== player.uuId);
    const winnerData = {
      gameSessionUuid: code,
      gameStatus: "FINISHED",
      players: [
        {
          uuid: player.uuId,
          points: 100,
          userGameSessionStatus: "WON",
        },
        {
          uuid: absentUuid,
          points: 0,
          userGameSessionStatus: "DROPPED",
        },
      ],
    };
    room.winnerDataSent = true;
    roomBroadcast(code, "gameWon", { winnerName: player.name });
    roomBroadcast(code, "gameOver", {});
    console.log("Winner data (waiting timeout):", winnerData);
    (async () => {
      try {
        // Update room in MongoDB
        await updateRoomInDB(code, winnerData);
        // Send winner data to backend
        const response = await axios.post(
          `${SAFA_BACKEND_URL}/api/external_game/v1/game_session_finish`,
          winnerData
        );
        console.log("Backend response (waiting timeout):", response.data);
      } catch (error) {
        console.error("Error sending winner data (waiting timeout):", error.response?.data || error);
      }
      // Clean up room
      for (const pid in room.players) {
        io.sockets.sockets.get(pid)?.disconnect();
      }
      if (room.countdownInterval) {
        clearInterval(room.countdownInterval);
      }
      delete rooms[code];
    })();
  }
}
io.on("connection", socket => {
  let roomCode = null;
  // ---------------- joinRoom ----------------
  socket.on("joinRoom", async raw => {
    const { roomCode: code, uuId } = parse(raw);
    const room = rooms[code];
    if (!room) return emitJSON(socket, "errorRoom", { msg: "Room not found" });
    const dbRoom = await Room.findOne({ gameSessionUuid: code });
    if (!dbRoom) return emitJSON(socket, "errorRoom", { msg: "Room not found in database" });
    const dbPlayer = dbRoom.players.find(p => p.uuid === uuId);
    if (!dbPlayer) return emitJSON(socket, "errorRoom", { msg: "Player not allowed in this room" });
    if (!room.allowedPlayers.includes(uuId))
      return emitJSON(socket, "errorRoom", { msg: "Player not allowed in this room" });
    // Reconnection
    const existingPlayerId = Object.keys(room.players).find(
      id => room.players[id].uuId === uuId && room.players[id].disconnected
    );
    if (existingPlayerId) {
      roomCode = code;
      socket.join(roomCode);
      const p = {
        id: socket.id,
        x: room.players[existingPlayerId].x,
        y: room.players[existingPlayerId].y,
        z: room.players[existingPlayerId].z,
        rotationY: room.players[existingPlayerId].rotationY,
        rotationX: room.players[existingPlayerId].rotationX,
        forward: 0,
        right: 0,
        health: room.players[existingPlayerId].health,
        canShoot: true,
        uuId: uuId,
        name: dbPlayer.name,
        profileImage: dbPlayer.profileImage,
        characterKey: room.playerInfo[uuId].characterKey,
        disconnected: false,
        isOnObstacle: room.players[existingPlayerId].isOnObstacle,
        currentObstacle: room.players[existingPlayerId].currentObstacle,
        isFalling: room.players[existingPlayerId].isFalling
      };
      room.players[socket.id] = p;
      emitJSON(socket, "yourId", {
        id: socket.id,
        name: p.name,
        profileImage: p.profileImage,
        characterKey: p.characterKey
      });
      emitJSON(socket, "roomJoined", { roomCode });
      // If reconnecting makes it 2 players
      if (Object.keys(room.players).length >= MAX_PLAYERS) {
        if (room.waitingTimer) {
          clearTimeout(room.waitingTimer);
          room.waitingTimer = null;
        }
        // Send init to all
        for (const playerId in room.players) {
          const s = io.sockets.sockets.get(playerId);
          if (s) {
            emitJSON(s, "init", { players: room.players, obstacles: room.obstacles, movingObstacles: room.movingObstacles });
            emitJSON(s, "roundStart", { currentRound: room.currentRound });
          }
        }
        roomBroadcast(roomCode, "newPlayerConnected", { players: room.players, obstacles: room.obstacles, movingObstacles: room.movingObstacles });
        room.waitingForCountdown = Object.keys(room.players).length;
        setTimeout(() => {
          if (room.waitingForCountdown > 0) {
            console.warn(`Force-starting countdown for room ${roomCode} after timeout`);
            room.waitingForCountdown = 0;
            startCountdown(roomCode);
          }
        }, 20000);
      }
      delete room.players[existingPlayerId];
      roomBroadcast(roomCode, "playerDisconnected", { playerId: existingPlayerId });
      if (Object.keys(room.players).length === 0) {
        delete rooms[roomCode];
      }
    } else if (Object.keys(room.players).length >= MAX_PLAYERS) {
      return emitJSON(socket, "errorRoom", { msg: "Room is full" });
    } else {
      roomCode = code;
      socket.join(roomCode);
      // Determine spawn position based on current number of players
      const numPlayers = Object.keys(room.players).length;
      const spawnZ = numPlayers === 0 ? 29.33 : 2;
      const spawnRotateY = numPlayers === 0 ? 180 : 0;
      const p = {
        id: socket.id,
        x: 0,
        y: 1,
        z: spawnZ,
        rotationY: spawnRotateY,
        rotationX: -169.2,
        forward: 0,
        right: 0,
        health: 100,
        canShoot: true,
        uuId: uuId,
        name: dbPlayer.name,
        profileImage: dbPlayer.profileImage,
        characterKey: room.playerInfo[uuId].characterKey,
        disconnected: false,
        isOnObstacle: false,
        currentObstacle: null,
        isFalling: false
      };
      room.players[socket.id] = p;
      // Always send yourId and roomJoined to the joining player
      emitJSON(socket, "yourId", {
        id: socket.id,
        name: p.name,
        profileImage: p.profileImage,
        characterKey: p.characterKey
      });
      emitJSON(socket, "roomJoined", { roomCode });
      const currentNumPlayers = Object.keys(room.players).length;
      if (currentNumPlayers === 1) {
        // Start waiting timer for opponent
        room.waitingStart = Date.now();
        room.waitingTimer = setTimeout(() => handleWaitingTimeout(code), 120000);
        // Notify the first player to start waiting timer display
        emitJSON(socket, "waitingForOpponent", { timeout: 120000 });
      } else if (currentNumPlayers >= MAX_PLAYERS) {
        // Cancel waiting timer if active
        if (room.waitingTimer) {
          clearTimeout(room.waitingTimer);
          room.waitingTimer = null;
        }
        // Send init and start game for all players
        for (const playerId in room.players) {
          const s = io.sockets.sockets.get(playerId);
          if (s) {
            emitJSON(s, "init", { players: room.players, obstacles: room.obstacles, movingObstacles: room.movingObstacles });
            emitJSON(s, "roundStart", { currentRound: room.currentRound });
          }
        }
        roomBroadcast(roomCode, "newPlayerConnected", { players: room.players, obstacles: room.obstacles, movingObstacles: room.movingObstacles });
        room.waitingForCountdown = Object.keys(room.players).length;
        setTimeout(() => {
          if (room.waitingForCountdown > 0) {
            console.warn(`Force-starting countdown for room ${roomCode} after timeout`);
            room.waitingForCountdown = 0;
            startCountdown(roomCode);
          }
        }, 20000);
      }
    }
  });
  // NEW: Handle client ready after countdown
  socket.on("readyForCountdown", () => {
    if (!roomCode || !rooms[roomCode]) return;
    const room = rooms[roomCode];
    if (room.waitingForCountdown > 0) {
      room.waitingForCountdown--;
      if (room.waitingForCountdown === 0) {
        startCountdown(roomCode);
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
    if (!roomCode || !rooms[roomCode] || !rooms[roomCode].isPlaying) return;
    const room = rooms[roomCode];
    const player = room?.players[socket.id];
    if (!player || !player.canShoot) return;
    player.canShoot = false;
    setTimeout(() => player.canShoot = true, 200);
    const rad = degToRad(player.rotationY);
    const bx = player.x + Math.sin(rad);
    const bz = player.z + Math.cos(rad);
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
      if (room.players[socket.id]?.disconnected && !room.winnerDataSent) {
        // Find the remaining player (the one who didn't disconnect)
        let remainingPlayerId = null;
        for (const pid in room.players) {
          if (pid !== socket.id) {
            remainingPlayerId = pid;
            break;
          }
        }
        if (remainingPlayerId) {
          const remainingPlayer = room.players[remainingPlayerId];
          // Prepare winnerData before deleting player
          const winnerData = {
            gameSessionUuid: roomCode,
            gameStatus: "FINISHED",
            players: [
              {
                uuid: remainingPlayer.uuId,
                points: 100,
                userGameSessionStatus: "WON",
              },
              {
                uuid: player.uuId,
                points: 0,
                userGameSessionStatus: "DROPPED",
              },
            ],
          };
          room.winnerDataSent = true;
          // Broadcast playerDropped instead of playerWon for disconnect case
          roomBroadcast(roomCode, "playerDropped", { playerId: socket.id });
          roomBroadcast(roomCode, "gameWon", { winnerName: remainingPlayer.name });
          // Send gameOver event to trigger client-side postMessage
          roomBroadcast(roomCode, "gameOver", {});
          // Send winnerData to backend and update DB
          console.log("Winner data (disconnect):", winnerData);
          (async () => {
            try {
              // Update room in MongoDB
              await updateRoomInDB(roomCode, winnerData);
              // Send winner data to backend
              const response = await axios.post(
                `${SAFA_BACKEND_URL}/api/external_game/v1/game_session_finish`,
                winnerData
              );
              console.log("Backend response (disconnect):", response.data);
            } catch (error) {
              console.error("Error sending winner data (disconnect):", error.response?.data || error);
            }
            // Delete player and room after sending data
            delete room.players[socket.id];
            if (room.countdownInterval) {
              clearInterval(room.countdownInterval);
            }
            if (Object.keys(room.players).length === 0) {
              delete rooms[roomCode];
            }
          })();
        } else {
          // If no remaining players, just clean up
          delete room.players[socket.id];
          if (room.countdownInterval) {
            clearInterval(room.countdownInterval);
          }
          delete rooms[roomCode];
        }
      }
    }, 10000); // 10 seconds
  });
});
// Timer sync interval
setInterval(() => {
  for (const code in rooms) {
    const room = rooms[code];
    if (room.isPlaying && !room.winnerDataSent) {
      const elapsed = Math.floor(room.activeTime / 1000);
      const remaining = Math.max(0, 300 - elapsed);
      roomBroadcast(code, "timerSync", { remaining });
    }
  }
}, 10000);
// -----------------------------------------------------------------------------
// Main game loop
// -----------------------------------------------------------------------------
setInterval(() => {
  for (const code in rooms) {
    const room = rooms[code];
    if (!room) continue;
    let winnerDataToSend = null;
    const now = Date.now();
    if (room.isPlaying) {
      room.activeTime += now - room.lastActiveTimestamp;
      room.lastActiveTimestamp = now;
    }
    // NEW: Check 5-minute active gameplay timer
    if (room.activeTime > 300000 && !room.winnerDataSent) {
      const uuIds = Object.keys(room.roundWins);
      if (uuIds.length !== 2) continue; // Safety check
      const wins = uuIds.map(u => room.roundWins[u] || 0);
      let winnerIndex = -1;
      if (wins[0] > wins[1]) winnerIndex = 0;
      else if (wins[1] > wins[0]) winnerIndex = 1;
      if (winnerIndex !== -1) {
        // Declare winner based on most round wins
        const winnerUuId = uuIds[winnerIndex];
        const loserUuId = uuIds[1 - winnerIndex];
        const winnerPlayer = Object.values(room.players).find(p => p.uuId === winnerUuId);
        const winnerName = winnerPlayer ? winnerPlayer.name : room.playerInfo[winnerUuId].name;
        roomBroadcast(code, "gameWon", { winnerName });
        winnerDataToSend = {
          gameSessionUuid: code,
          gameStatus: "FINISHED",
          players: [
            {
              uuid: winnerUuId,
              points: 100,
              userGameSessionStatus: "WON",
            },
            {
              uuid: loserUuId,
              points: 0,
              userGameSessionStatus: "DEFEATED",
            },
          ],
        };
        room.winnerDataSent = true;
        room.isPlaying = false;
      } else if (wins[0] === wins[1]) {
        // Tie: Restart the game
        room.currentRound = 1;
        room.roundWins = uuIds.reduce((acc, u) => { acc[u] = 0; return acc; }, {});
        room.movingObstacles = movingObstacleSets[Math.floor(Math.random() * movingObstacleSets.length)];
        resetRound(room);
        room.activeTime = 0;
        room.lastActiveTimestamp = 0;
        room.gameStartTime = 0;
        roomBroadcast(code, "roundStart", { currentRound: room.currentRound });
        room.waitingForCountdown = Object.keys(room.players).length;
        room.isPlaying = false;
        if (room.roundEnding) room.roundEnding = false;
        setTimeout(() => {
          if (room.waitingForCountdown > 0) {
            console.warn(`Force-starting countdown for room ${code} after timeout in tie restart`);
            room.waitingForCountdown = 0;
            startCountdown(code);
          }
        }, 20000);
      }
    }
    if (!room.isPlaying) continue;
    // --- Update moving obstacles (Y-axis ping-pong) ---
    for (const mob of room.movingObstacles) {
      if (mob.speed === 0) continue;
      if (!mob.direction) mob.direction = 1; // 1 = up, -1 = down
      mob.y += mob.speed * mob.direction;
      if (mob.y > mob.endPoint) {
        mob.y = mob.endPoint - 0.03;
        mob.direction = -1;
      } else if (mob.y < mob.startPoint) {
        mob.y = mob.startPoint + 0.03;
        mob.direction = 1;
      }
    }
    // Player movement
    for (const id in room.players) {
      const p = room.players[id];
      const input = room.latestInputs[id];
      if (!input || p.health <= 0) continue;
      const speed = 0.09;
      const rad = degToRad(p.rotationY);
      let dx = 0, dz = 0;
      p.forward = 0;
      p.right = 0;
      if (input.forward) { dx += Math.sin(rad) * speed; dz += Math.cos(rad) * speed; p.forward = 1; }
      if (input.backward) { dx -= Math.sin(rad) * speed; dz -= Math.cos(rad) * speed; p.forward = -1; }
      if (input.left) { dx -= Math.cos(rad) * speed; dz += Math.sin(rad) * speed; p.right = -1; }
      if (input.right) { dx += Math.cos(rad) * speed; dz -= Math.sin(rad) * speed; p.right = 1; }
      if (typeof input.rotationDelta === "number")
        p.rotationY = (p.rotationY + input.rotationDelta + 360) % 360;
      if (typeof input.rotationVerticalDelta === "number") {
        p.rotationX = (p.rotationX || -169.2) - input.rotationVerticalDelta;
        p.rotationX = Math.max(-189.7, Math.min(-137, p.rotationX));
      }
      let candidate = { ...p, x: p.x + dx, y: p.y, z: p.z + dz };
      // Check collisions and x-z area overlap
      const collisionResult = checkCollision(candidate, room);
      if (!collisionResult.collision) {
        p.x = candidate.x;
        p.z = candidate.z;
        if (collisionResult.withinObstacleArea) {
          // Player is within the x-z area of a moving obstacle
          p.isOnObstacle = true;
          p.currentObstacle = collisionResult.withinObstacleArea;
          // Set player's y-position to follow the obstacle's y-position
          p.y = collisionResult.withinObstacleArea.y + collisionResult.withinObstacleArea.size.y / 2 + playerSize.y / 2;
          p.isFalling = false; // No gravity applied
        } else {
          // Player is not within any obstacle's x-z area
          p.isOnObstacle = false;
          p.currentObstacle = null;
          p.isFalling = true; // Apply gravity
        }
      } else {
        // Collision occurred (e.g., with walls or obstacle sides)
        p.isFalling = p.y > 1;
      }
      // Apply gravity only if the player is falling (not on an obstacle's x-z area)
      if (p.isFalling) {
        const gravity = -7;
        p.y = Math.max(1, p.y + gravity / TICK_RATE);
        if (p.y <= 1) {
          p.y = 1;
          p.isFalling = false;
        }
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
          roomBroadcast(code, "bulletRemove", { bulletId: b.id });
          return false;
        }
      }
      // Moving obstacle hit
      for (const mob of room.movingObstacles) {
        const mobOBB = { x: mob.x, y: mob.y, z: b.z, size: mob.size, rotationY: mob.rotationY || 0 };
        if (checkOBB(bulletOBB, mobOBB)) {
          roomBroadcast(code, "bulletHitObstacle", { bulletPos: { x: b.x, y: b.y, z: b.z } });
          roomBroadcast(code, "bulletRemove", { bulletId: b.id });
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
          if (t.health <= 0) {
            t.forward = 0;
            t.right = 0;
            delete room.latestInputs[tid];
          }
          roomBroadcast(code, "playerHit", { targetId: tid, newHealth: t.health });
          roomBroadcast(code, "bulletHitObstacle", { bulletPos: { x: b.x, y: b.y, z: b.z } });
          roomBroadcast(code, "bulletRemove", { bulletId: b.id });
          return false;
        }
      }
      return b.lifeTime > 0;
    });
    // Check for round end after bullet updates
    const playerIds = Object.keys(room.players);
    let deadPlayerId = null;
    for (const pid of playerIds) {
      if (room.players[pid].health <= 0) {
        deadPlayerId = pid;
        break;
      }
    }
    if (deadPlayerId && !room.roundEnding) {
      room.roundEnding = true;
      const loser = room.players[deadPlayerId];
      const winnerId = playerIds.find(id => id !== deadPlayerId);
      const winner = room.players[winnerId];
      const roundWinnerUuId = winner.uuId;
      room.roundWins[roundWinnerUuId]++;
      const requiredWins = Math.ceil(room.maxRounds / 2);
      const overallWinnerUuId = Object.keys(room.roundWins).find(u => room.roundWins[u] >= requiredWins);
      if (overallWinnerUuId) {
        const overallLoserUuId = Object.keys(room.roundWins).find(u => u !== overallWinnerUuId);
        const overallWinnerName = winner.name;
        roomBroadcast(code, "gameWon", { winnerName: overallWinnerName });
        winnerDataToSend = {
          gameSessionUuid: code,
          gameStatus: "FINISHED",
          players: [
            {
              uuid: overallWinnerUuId,
              points: 100,
              userGameSessionStatus: "WON",
            },
            {
              uuid: overallLoserUuId,
              points: 0,
              userGameSessionStatus: "DEFEATED",
            },
          ],
        };
        room.winnerDataSent = true;
        room.isPlaying = false;
        if (room.countdownInterval) {
          clearInterval(room.countdownInterval);
        }
      }
      else {
        roomBroadcast(code, "roundOver", {
          winnerId: winnerId, loserId: deadPlayerId,
          winnerName: winner.name, loserName: loser.name
        });
        room.isPlaying = false;
        setTimeout(() => {
          room.currentRound++;
          resetRound(room);
          roomBroadcast(code, "roundStart", { currentRound: room.currentRound });
          room.waitingForCountdown = Object.keys(room.players).length;
          room.roundEnding = false;
          // Optional safety timeout for force-start if clients don't ready
          setTimeout(() => {
            if (room.waitingForCountdown > 0) {
              console.warn(`Force-starting countdown for room ${code} after timeout`);
              room.waitingForCountdown = 0;
              startCountdown(code);
            }
          }, 20000); // Increased to 20 seconds
        }, 5000);
      }
    }
    if (winnerDataToSend) {
      console.log("Winner data:", winnerDataToSend);
      // Send gameOver event to trigger client-side postMessage
      roomBroadcast(code, "gameOver", {});
      // Perform async operation outside the filter loop
      (async () => {
        try {
          // Update room in MongoDB
          await updateRoomInDB(code, winnerDataToSend);
          // Send winner data to backend
          const response = await axios.post(
            `${SAFA_BACKEND_URL}/api/external_game/v1/game_session_finish`,
            winnerDataToSend
          );
          console.log("Backend response:", response.data);
        } catch (error) {
          console.error("Error sending winner data:", error.response?.data || error);
        }
        // Disconnect all sockets in the room and delete the room
        for (const pid in room.players) {
          io.sockets.sockets.get(pid)?.disconnect();
        }
        if (room.countdownInterval) {
          clearInterval(room.countdownInterval);
        }
        delete rooms[code];
      })();
    }
    // Broadcast world state
    roomBroadcast(code, "stateUpdate", {
      players: room.players,
      bullets: room.bullets.map(b => ({
        id: b.id, ownerId: b.ownerId,
        x: b.x, y: b.y, z: b.z, rotationY: b.rotationY
      })),
      movingObstacles: room.movingObstacles.map(m => ({
        id: m.id, x: m.x, y: m.y, z: m.z, size: m.size, rotationY: m.rotationY, prefabType: m.prefabType
      })),
      roundWins: room.roundWins // Sends UUID-to-wins mapping
    });
  }
}, 1000 / TICK_RATE);