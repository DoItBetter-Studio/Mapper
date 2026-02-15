# Mapper

> Proprietary Software — DoItBetter Studio

**Mapper** is the world and content authoring tool within the Glyphborn ecosystem.

Development began in August 2025 as part of DoItBetter Studio’s long-term effort to build a modular, deterministic game engine and editor suite.

---

## Overview

Mapper is responsible for:

- World and map creation workflows
- Tile placement and editing tools
- Multi-floor map authoring
- World data visualization
- Content validation
- Exporting structured data for Atlas

Mapper provides a controlled environment for building and modifying world data while preserving deterministic structure and architectural integrity.

It acts as the bridge between design workflows and engine-ready world data.

---

## Architectural Role

The Glyphborn ecosystem is intentionally modular.

Mapper handles:

✔ World authoring and editing  
✔ Tile and floor layout tools  
✔ Data validation prior to runtime  
✔ Export pipelines to Atlas-compatible formats  

Mapper does **not** handle:

✖ Rendering engine logic  
✖ Audio systems  
✖ Game rules or combat systems  
✖ Networking  
✖ Runtime simulation  

Mapper generates structured data.  
Atlas defines and manages that data at runtime.

This separation ensures clarity between content creation and execution.

---

## Design Principles

Mapper follows the engineering principles established by DoItBetter Studio:

- **Deterministic Output** — Identical map data produces identical runtime results  
- **Clear Data Contracts** — Strict alignment with Atlas world structures  
- **Separation of Authoring and Execution**  
- **Modular Architecture** — Independent repository and versioning  
- **Scalable Tooling** — Designed to expand alongside engine capabilities  

---

## Ecosystem Integration

Mapper integrates with:

- Atlas — World and spatial data engine  
- Echo — Audio systems (for future preview tooling)  
- Glyphborn — Core runtime and gameplay systems  

Each component is developed independently to allow controlled iteration and long-term scalability.

---

## Project Status

Mapper is currently in active development.

The broader Glyphborn engine will eventually be rebranded and released as:

**Damascus — The Steel Editor Suite**

Until official release, this repository is publicly visible for transparency and portfolio purposes but is not open source.

---

## Ownership & License

Copyright © 2025–2026 DoItBetter Studio

All rights reserved.

This software and associated documentation are proprietary intellectual property of DoItBetter Studio.

No license is granted to use, copy, modify, distribute, sublicense, reverse engineer, or create derivative works without prior written permission.

DoItBetter Studio reserves the right to relicense this software under an open-source license upon official release.
