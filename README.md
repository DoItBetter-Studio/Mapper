# Mapper

> Proprietary Software — DoItBetter Studio

**Mapper** is the world and content authoring tool within the Glyphborn ecosystem — a deterministic, production‑grade editor for building handcrafted, tile‑based worlds.

Development began in August 2025 as part of DoItBetter Studio’s long‑term effort to create a modular, console‑style engine and editor suite.  
Mapper reached full architectural completion in February 2026.

---

## Overview

Mapper is responsible for:

- World and map creation workflows  
- Tile placement, editing, and multi‑layer authoring  
- Multi‑floor and multi‑chunk world construction  
- Real‑time adjacency visualization  
- Ghost‑map importing for reference‑based worldbuilding  
- Deterministic data validation  
- Exporting structured, byte‑aligned data for Atlas  

Mapper provides a controlled, predictable environment for building and modifying world data while preserving strict architectural integrity.  
It acts as the bridge between creative workflows and engine‑ready runtime data.

---

## Architectural Role

The Glyphborn ecosystem is intentionally modular.

Mapper handles:

✔ World authoring and editing  
✔ Tile and layer tools  
✔ Ghost‑map reference workflows  
✔ Deterministic data validation  
✔ Export pipelines to Atlas‑compatible binary formats  

Mapper does **not** handle:

✖ Rendering engine logic  
✖ Audio systems  
✖ Game rules or combat systems  
✖ Networking  
✖ Runtime simulation  

Mapper generates structured, deterministic data.  
Atlas consumes and executes that data at runtime.

This separation ensures clarity between content creation and execution.

---

## Design Principles

Mapper follows the engineering principles established by DoItBetter Studio:

- **Deterministic Output** — Identical input produces identical runtime results  
- **Strict Data Contracts** — Perfect alignment with Atlas world structures  
- **Separation of Authoring and Execution**  
- **Modular Architecture** — Independent repository and versioning  
- **Scalable Tooling** — Designed to grow with the engine  
- **Bounded Binary Formats** — Geometry and collision data capped to byte‑safe limits (≤255)  

Mapper is built like a console‑era tool: predictable, minimal, intentional.

---

## Ecosystem Integration

Mapper integrates with:

- **Atlas** — World and spatial data engine  
- **Echo** — Audio systems (future preview tooling)  
- **Glyphborn** — Core runtime and gameplay systems  

Each component is developed independently to allow controlled iteration and long‑term scalability.

---

## Project Status

Mapper is **feature‑complete and production‑ready**.

All major systems are implemented:

- Full tile editing  
- Multi‑layer authoring  
- Deterministic undo/redo  
- Real‑time adjacency rendering  
- Ghost‑map import system  
- Mini‑preview generation  
- Binary save/load  
- Binary export (geometry, collision, tilesets)  
- Byte‑bounded geometry formats  
- Stable UI and workflow  

Mapper now serves as the foundation for the next development layers:

- Matrix Editor  
- Region system  
- Runtime importer  
- World assembly pipeline  

The broader Glyphborn engine will eventually be rebranded and released as:

**Damascus — The Steel Editor Suite**

Until official release, this repository is publicly visible for transparency and portfolio purposes but is not open source.

---

## Ownership & License

Copyright © 2025–2026  
**DoItBetter Studio**

All rights reserved.

This software and associated documentation are proprietary intellectual property of DoItBetter Studio.

No license is granted to use, copy, modify, distribute, sublicense, reverse engineer, or create derivative works without prior written permission.

DoItBetter Studio reserves the right to relicense this software under an open‑source license upon official release.
