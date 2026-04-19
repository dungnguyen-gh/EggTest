EggTest Recruitment Prototype
=============================

Overview
--------
This project implements the Ubisoft-style egg collection recruitment test as a single-scene Unity prototype for PC.
It targets **Unity 2022.3.62f1** and uses a scene-resident `GameRoot` in `SampleScene` so the composition is easy to inspect before pressing Play.

The prototype is intentionally small and review-friendly:
- one local keyboard-controlled player
- multiple server-driven remote bots
- server-authoritative eggs, scoring, and match outcome
- custom grid-based A* pathfinding
- latency/jitter/spike simulation
- client prediction plus remote interpolation

Requirement Coverage
--------------------
- **Fixed time-limit egg collection match**  
  Implemented in `ServerSimulator` with authoritative timer and match end.

- **N players at match start**  
  Configurable through `GameConfig.PlayerCount`; one local player, remaining players are remote bots.

- **4-direction movement**  
  Local input is cardinalized and bots also move using grid/cardinal movement.

- **Keyboard local player + simulated remote players**  
  Local player uses Unity's New Input System action asset; bots are driven by server-side AI.

- **Remote bots find shortest path and avoid obstacles**  
  Bots evaluate reachable eggs through custom A* over a blocked grid.

- **Build your own pathfinding**  
  `GridPathfinder` is a custom implementation; no third-party pathfinding plugin is used.

- **Random colored eggs**  
  Eggs are spawned by the server with palette-driven color selection.

- **Server decides egg spawn and collection**  
  Egg creation, collection, score updates, and winners all come from the authoritative simulator.

- **Obstacle/blocker map**  
  The arena uses a deterministic blocked-cell layout shared by client and server.

- **Top-down / isometric style view**  
  The camera uses an angled top-down 3D setup suitable for the brief.

- **Random server update interval between 0.1s and 0.5s**  
  Snapshot cadence is randomized between `SnapshotMinInterval` and `SnapshotMaxInterval`.

- **Interpolation under variable network timing**  
  Remote players buffer snapshots and interpolate with bounded extrapolation.

- **Handle latency gracefully and allow latency simulation**  
  `SimulatedTransport` supports presets, jitter, and spike simulation.

Architecture
------------
- **Shared (`Assets/Scripts/Shared`)**  
  Message contracts, IDs, arena/grid data, movement helpers, config, and transport abstractions.

- **Server (`Assets/Scripts/Server`)**  
  Authoritative match simulation, bot decision-making, pathfinding, egg spawning, and scoring.

- **Client (`Assets/Scripts/Client`)**  
  Runtime composition, local input send path, prediction/reconciliation, remote interpolation, view updates, and HUD.

- **Editor (`Assets/Editor`)**  
  Sample scene authoring and rebuild helpers for keeping the scene contract inspectable.

- **Assembly Definitions**  
  Shared / Server / Client / Editor are split into separate Unity assemblies to keep dependencies explicit and reduce iteration cost.

Networking Approach
-------------------
1. **Server-authoritative simulation**  
   The simulator owns official player positions, egg lifecycle, scores, and winners.

2. **Message-based seam**  
   Client and server communicate through explicit message DTOs so the simulator can be replaced by a real transport later.

3. **Fake local transport with latency simulation**  
   `SimulatedTransport` queues messages with configurable latency, jitter, and spike behavior.

4. **Local prediction + reconciliation**  
   The local player sends sequenced input, predicts movement immediately, and reconciles against server snapshots.

5. **Remote snapshot interpolation**  
   Remote players render from buffered snapshots with adaptive interpolation back-time and bounded extrapolation.

Optimization Applied
--------------------
- **New Input System migration**  
  Local movement now uses a dedicated input actions asset.

- **Assembly definition boundaries**  
  Improves compile isolation and reduces accidental cross-layer coupling.

- **Heap-based open set for A***  
  `GridPathfinder` now uses a small binary heap and reused search buffers instead of a linear-scan open set.

- **Basic egg view pooling**  
  Egg views are reused instead of repeatedly creating/destroying GameObjects during normal play.

- **Focused EditMode tests**  
  Core deterministic logic is covered:
  - cardinal input logic
  - arena spawn/egg-cell rules
  - pathfinding correctness

How to Run and Verify
---------------------
1. Open the project in **Unity 2022.3.62f1**.
2. Let Unity import packages and compile assemblies.
3. Open `Assets/Scenes/SampleScene.unity`.
4. Press **Play**.
5. Move with **WASD** or **arrow keys**.
6. Use the right-side debug panel to:
   - change player count
   - change match duration
   - switch network preset
   - toggle latency spike simulation
   - restart the match

Suggested manual verification:
- local movement remains responsive
- bots route around blockers and chase eggs
- eggs spawn/despawn/score correctly
- restarting the match multiple times remains stable
- presets Stable / Low / Medium / High still produce expected smoothing differences

Automated verification:
- open **Window > General > Test Runner**
- run all **EditMode** tests

Known Limitations
-----------------
- The arena is generated from code instead of fully scene-authored prefabs.
- Visuals are intentionally primitive/programmer-art to keep the focus on gameplay and architecture.
- Local reconciliation is lightweight replay-from-authority, not full rollback netcode.
- The transport still passes in-memory message objects rather than serialized wire payloads.
- Packet loss / out-of-order delivery simulation is not implemented yet.
- The project is a single-scene prototype, not a production multiplayer framework.
