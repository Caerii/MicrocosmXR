please perform a deep research based on our spec of this experience we are hoping to create and design as a platform technology

# Microcosm XR — Written Spec (v0.2)

## 1) One-liner

**Microcosm XR** is a **multiplayer co-located mixed-reality sandbox** where players act like “small gods” building and evolving **Minecraft-like micro-worlds** on real tables/spaces—using **hands + gaze + lightweight agents**, with non-optional **Gaussian Splatting** for scene capture + semantic understanding + persistence to make things a lot more rich. 

## 2) North Star Experience

You and a friend put on headsets in the same room.

* Your living room becomes the “stage.”
* A tabletop becomes a “world plane.”
* You **spawn a microcosm**, sculpt it with your hands, and drop in **agents** (creatures, bots, civilizations, “tentacle AI for robot design” vibes).
* You can switch into **God Mode** (authoring / editing / orchestration) or **Play Mode** (inhabiting / interacting).
* The world persists, evolves, and can be reloaded later in the same space.

## 3) Pillars

1. **Co-located multiplayer first** (low latency, shared spatial context).
2. **Embodied authoring** (hands, gaze, grab/spawn tools).
3. **World persistence** (save/load microcosms; location-linked anchors).
4. **Semantic scene understanding (optional but strategic)** via splats + segmentation.
5. **Privacy-respecting capture** (remove/blur humans + personal objects; access controls).

---

# 4) Core Modes

## A) Gameplay Mode (Loop)

The “fun” loop: spawn → sculpt → populate → interact → observe evolution → tweak → share.

## B) God Mode (Multiplayer)

A higher-privilege toolset:

* spawn/delete/edit assets
* manage agents
* world parameters (physics, resources, rules)
* time controls (pause, fast-forward, rollback snapshots)
* access controls (who can view/edit)

## C) Utility Mode (Splat/Scene Tools)

A “toolbox” mode:

* capture a scene representation (splats or meshes)
* semantic cleanup (remove people, sensitive items)
* editing utilities (crop/erase regions, simplify)
* export/import library assets

## D) Library Loading Page

Browse + load:

* microcosms
* templates
* shared worlds
* asset packs

## E) Settings / Account

Permissions, sharing, privacy defaults, input calibration, comfort.

---

# 5) MVP Target: “Tabletop Scene (1)”

This is your real MVP spine. Everything else can be fake/stubbed.

## MVP Feature Set (Minimum Viable *Delight*)

1. **Physics + Colliders + Basic Interactions**

   * small set of primitives (blocks, spheres, ramps)
   * grab / place / scale
   * stable collision + gravity
2. **Multiplayer (Low-latency)**

   * synced transforms + physics-ish objects (authoritative or hybrid)
   * consistent object ownership rules
3. **Shared Mixed Reality (Co-location SDK)**

   * shared coordinate system so everyone sees the same world in the same place
4. **Core Hand Tracking (Low-fidelity OK)**

   * pinch to grab
   * two-hand scale/rotate
   * spawn tool
5. **Find Good Example Scenes + Assets**

   * 2–3 “toy worlds” that are instantly fun (domino chain, tiny civ sandbox, marble run)

### MVP Deliverable

A 10-minute demo where 2 players:

* co-locate
* spawn a microcosm
* build something together
* toss physics objects around
* save it
* reload it

---

# 6) Interaction Design

## Hand Tracking (Sandbox-first)

* **Grab/Move**: pinch + drag
* **Scale/Rotate**: two-hand gesture
* **Spawn Tool**: “paintbrush” or “wand” you pick up
* **Delete**: pinch + flick / lasso + confirm

## Eye Tracking (Gameplay spice, not MVP-critical)

Two concrete mechanics from your notes:

* **“Looking makes stuff go towards you”** (gaze magnetism)
* **Gaze heatmap** (“Cognitive 3D” visualization layer)

Deliverables:

* gaze ray + fixation detection
* optional debug overlay: heatmap projected onto world surface / splats / objects

## Voice/Keyboard (Optional authoring speed)

* voice: “spawn tree”, “make it rainy”, “add 20 villagers”
* keyboard fallback for dev + power users

---

# 7) World Representation & Persistence

## Microcosm Data Model

A microcosm is a package of:

* world anchor info (shared origin + scale)
* object graph (entities, transforms, components)
* physics state (optional snapshot)
* agent state (beliefs/needs/goals)
* asset references (prefabs/models)
* scene context layer (optional: splats/mesh + semantics)

## Save/Load Requirements

* local save fast
* export/share package
* deterministic-ish replay (where possible)
* versioning: schema evolution

---

# 8) Gaussian Splatting + Semantic Scene Understanding (Strategic Track)

This is the “if we nail this, it becomes unfair” layer.

## Purpose

* capture real environment context (table, room)
* support occlusion + realism
* enable semantic queries:

  * “that’s a table edge”
  * “avoid the laptop”
  * “place this structure on flat surface”
* support persistent alignment across sessions (anchor + splat map)

## Required Capabilities

* splat ingestion into engine (render + depth/occlusion)
* semantic labeling or segmentation overlay
* editing tools (erase/crop, remove people, simplify)
* exportable “SplatPack” format

## “SplatterPlot Library”

I’m treating your note as:

* a library for **visualizing/editing splat point distributions**, clusters, semantic tags, gaze heatmaps, etc.
* could be an internal tool, not a user-facing thing at first.

---

# 9) Privacy + Sharing (Non-negotiable if you capture reality)

## Privacy Requirements

* **Blur/remove people** (automatic + manual override)
* remove sensitive objects (screens, photos, documents) if detected
* never share raw capture without explicit permission
* redaction is visible + reviewable (“here’s what’s being removed”)

## Access Controls

World-level permissions:

* view-only
* edit
* admin (God Mode + sharing rights)

Asset-level permissions:

* private assets vs shared packs
* attribution metadata (optional but nice)

---

# 10) Multiplayer & Shared Context

## Key Problems

* how to share location/anchor data reliably
* drift correction
* conflict resolution for edits
* keeping physics “close enough” in a shared session

## Multiplayer Design Defaults

* authoritative ownership per object (who simulates + broadcasts)
* server-hosted session or host-client (depending on scope)
* reconciliation: snap gently, not violently
* “God Mode edits” override physics if needed (authoring wins)

---

# 11) Agents Track (Second Step after MVP)

From your note: “Designing the agent core + social + splatting for scene understanding.”

## Agent Scope (Practical)

* start with **episodic, limited world models**
* agents live inside the microcosm and interact with:

  * microcosm objects
  * rules (resource, movement)
  * social: simple interactions (follow, trade, flock, build)

## Agent Architecture (initial)

* perception: world state + semantic tags + proximity triggers
* memory: episodic snapshots + short-term working memory
* policy: behavior tree / utility AI / simple planner
* world model: local + limited (don’t pretend it’s omniscient)

---

# 12) Open Questions (You should answer early)

* What’s the primary platform (Quest 3 is definitely where we are STARTING, but lock it)? 
* Is the “Minecraft frame” literal blocks, or just “toy-world vibe”? (we want to holographically stream minecraft directly from the java game) 
* Are microcosms tabletop-only in v1, or room-scale too? (both) 
* Are splats required for launch, or a parallel R&D spike? (we need it actually for this, important for feature impl.) 

(You don’t need perfect answers—just pick defaults so engineering doesn’t stall.)

---

# Research Query Pack (Deep Query Scaffold)

This is the part you wanted: **a structured set of research questions** you can dump into search engines, arXiv, GitHub, SDK docs, and internal design review.

## A) Co-located MR Multiplayer

**Goal:** shared spatial coordinate system + low latency interaction.

Queries:

* “co-located multiplayer mixed reality shared coordinate system drift correction”
* “spatial anchors multiplayer consistency quest”
* “shared world alignment in AR multi-user SLAM anchor fusion”
* “networked physics synchronization XR low latency object ownership reconciliation”

What you’re looking for:

* anchor sharing workflows
* drift mitigation
* network architectures used in MR games
* best practices for object authority

## B) Hand Tracking Authoring UX

Queries:

* “hand tracking interaction techniques XR object manipulation pinch scale rotate”
* “bimanual gestures MR authoring tools”
* “AR sandbox hand-based world building user study”

Deliverable from research:

* gesture set that’s reliable + not exhausting
* affordances that reduce accidental grabs
* comfort + precision tradeoffs

## C) Eye Tracking Mechanics + Visualization

Queries:

* “gaze-based interaction techniques in VR AR object attraction”
* “gaze heatmap visualization 3D environments cognitive map”
* “eye tracking attention maps XR real-time”

Deliverable:

* fixation detection approach
* heatmap projection methods (world space vs object space)
* gameplay patterns that don’t feel like input lag

## D) Gaussian Splatting in Real-Time Engines

Queries:

* “3D Gaussian splatting real-time rendering engine integration”
* “gaussian splatting occlusion depth sorting XR”
* “gaussian splatting compression streaming LOD”
* “interactive editing gaussian splats erase crop region”

Deliverable:

* viable rendering approach on mobile XR hardware
* LOD strategy + culling
* storage format + streaming plan

## E) Semantic Layer on Top of Splats / Scene

Queries:

* “semantic segmentation gaussian splatting”
* “3D scene understanding from radiance fields / splats”
* “open vocabulary 3D segmentation in AR scenes”
* “object removal / inpainting 3D capture privacy”

Deliverable:

* pipeline for labeling the world (even coarse)
* semantics used for gameplay + safety
* minimal model footprint strategy

## F) Privacy-Preserving Capture + Redaction

Queries:

* “privacy preserving mixed reality capture automatic person removal”
* “AR video privacy redaction segmentation real-time”
* “access control for shared 3D captures”

Deliverable:

* redaction UX patterns (review + confirm)
* threat model (what could leak?)
* safe defaults for sharing

## G) Agent Core for Micro-Worlds

Queries:

* “utility AI sandbox agents emergent behavior”
* “episodic memory agents simulation”
* “multi-agent systems small worlds social dynamics game AI”
* “world model limited perception agent architecture”

Deliverable:

* lightweight agent architecture that’s fun, not overengineered
* authoring hooks (“spawn agent with personality preset”)
* simulation tick budgeting for XR

---

# Suggested Build Order (No nonsense)

1. **Co-location + shared origin + multiplayer transforms**
2. **Hand tracking build tools**
3. **Physics toybox + 2–3 fun microcosm templates**
4. **Save/load**
5. **God Mode permissions + editing**
6. **Eye tracking mechanics + heatmap**
7. **Splats rendering spike**
8. **Semantic + privacy redaction**
9. **Agents (episodic, limited) + evolution loop**

---

also please provide: 

* a **PRD** (product requirements doc) with acceptance criteria per feature, **or**
* a **research sprint plan** (2 weeks) with “must-read papers + must-try repos + prototype milestones”.

(Quest 3 is the obvious pick). 

And provide detailed research and analysis and all the content that makes sense for us to have rich references and context regarding this: