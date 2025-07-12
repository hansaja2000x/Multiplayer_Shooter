# Multiplayer Shooter

This is a real-time multiplayer shooter game built using Unity and Node.js. The game uses a fully server-authoritative architecture to ensure fair gameplay, prevent cheating, and maintain real-time synchronization between all connected players.

## Project Link

GitHub Repository: https://github.com/hansaja2000x/Multiplayer_Shooter

---

## Game Architecture Overview

This project follows a server-authoritative design. All gameplay-related logic is handled on the server, while the client focuses on input handling, UI, and rendering.

### Technologies Used

- Unity 2022.3.34f
- KyleDulce Unity-SocketIO (WebSocket client for Unity)
- Node.js with Express and Socket.IO

---

## Server-Authoritative Logic

The server enforces all core game rules and ensures no client can cheat. Here's a breakdown:

**Server handles:**

- Player movement validation using Oriented Bounding Box (OBB) collision detection with obstacles
- Bullet simulation, movement, and collision with players and walls
- Player health management and hit detection
- Win condition logic (e.g., player elimination)
- Fire rate limiting (to prevent spamming bullets)

**Client handles:**

- Collecting input for movement and shooting
- Rendering received player and bullet positions
- Updating animations and health bars
- Displaying user interface (UI) and effects

This architecture ensures consistent gameplay across clients.

---

## Real-Time Synchronization

The server sends updates at a rate of 60 ticks per second (`TICK_RATE = 60`). These updates include:

- Player positions, health values, and animation states
- Bullet positions and states

Clients receive and use this data to render accurate gameplay visuals.

---

## How to Run the Game

### 1. Running the Server (Node.js)

Follow these steps to start the server:

1. Open a terminal.
2. Navigate to the server directory:

cd Multiplayer_Shooter/node_server


3. Install dependencies:

npm install express socket.io


4. Start the server:

node server.js

You should see the following output:

Server listening at port 3000


### 2. Unity Editor Setup

If you're testing the game in the Unity Editor, be aware of some limitations.

**Important:** Due to limitations in the KyleDulce Unity-SocketIO client, WebGL builds will not function correctly in Play mode inside the Editor. For that reason, you must switch to the PC platform.

Steps:

1. Open the Unity project located at:

Multiplayer_Shooter/Multiplayer Shooter Project

2. Go to the top menu: `File → Build Settings`.

3. Select `PC, Mac & Linux Standalone`, then click **Switch Platform**.

4. After the platform is switched, press the **Play** button in the Editor.

**Note:** Expect higher latency in the Editor. For optimal performance, use the WebGL build described below.

---

### 3. Running the WebGL Build

The project already includes a ready-to-use WebGL build.

#### A. Start the server

Make sure the Node.js server is running:

cd Multiplayer_Shooter/node_server
node server.js

#### B. Serve the WebGL build locally

You cannot open the `index.html` directly in your browser (it won't connect to the WebSocket server). Instead, you must host it using a local HTTP server.

1. Open a terminal and go to the WebGL build directory:

cd Multiplayer_Shooter/WebGL Builds

2. Use Python to serve the files locally:

If you are using Python 3.x:

python -m http.server

If you are using Python 2.x:

python -m SimpleHTTPServer

You should see something like:

Serving HTTP on :: port 8000 (http://[::]:8000/)

3. Open your web browser and go to:

http://localhost:8000/

The game should load and connect to the server.

#### C. Disable browser cache for consistent testing

1. Press `Ctrl + Shift + I` to open Developer Tools.
2. Go to the **Network** tab.
3. Check the **Disable cache** option.
4. Refresh the page (`Ctrl + R`).

---

## Unity 2020.x WebGL Fix (Only for Future Builds)

If you're using Unity 2020.x and plan to build the WebGL version yourself, you must modify the `index.html` file for WebSocket support to work correctly.

Follow the instructions provided here:  
https://github.com/KyleDulce/Unity-Socketio/wiki/Making-SocketIo-Work-in-2020.x

**Important:**  
The WebGL build already included in this project (`Multiplayer_Shooter/WebGL Builds`) has this fix applied.  
You do not need to apply it again unless you generate a new build.

---

## Demo Video

A working demonstration of multiplayer gameplay is available here:

**Link:**  
https://drive.google.com/drive/u/2/folders/1zX471f7M1q4I-Us0e3PAf7yw2MPdwVud

---

## Contributors

- Hansaja Hewanayake  
  GitHub: https://github.com/hansaja2000x

---
