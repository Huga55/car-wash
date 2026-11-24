# STEP 5 — root-cause analysis (before code changes)

Scope: the active `Assets/LittleGarage` gameplay path booted by `Assets/Scenes/Game.unity`.
Legacy `Assets/Scripts/GameController.cs` and `Assets/Editor/VisualVerification.cs` are not
active and are not used as evidence for the fixes below.

Status of this analysis: code inspection + offline vector/frustum checks. No Unity Play Mode
run or screenshot was available locally, so visual symptoms are treated as reports to explain,
not as locally reproduced facts.

## 1. Root causes of the invisible / unconvincing vehicle

1. **Inverted procedural body normals.** `VehicleFactory.AddQuad()` stores
   `cross(b-a, d-a)` while emitting triangles with the opposite geometric normal
   (`VehicleFactory.cs:671-680`). `MeshData.ToMesh()` copies those normals instead of
   recalculating them (`:51-62`). Offline dot-product checks give
   `dot(storedNormal, outward) = -1` for all six body faces and bevel strips: faces are
   outward-wound but lit as if inward-facing, so the fully dirty car renders nearly black.
2. **Inside-out wheel/rim geometry.** `AddCylinder()` stores outward normals but emits
   inward-facing triangles (`VehicleFactory.cs:805-829`); tire/rim outer surfaces are
   back-face-culled from outside. Corner triangles and some wheel-arch trims are also
   inconsistently wound (`:262-290`, `:744-763`).
3. **Orthographic scene camera.** `Game.unity:53-59` has `orthographic: 1`;
   `GarageCamera.EnsureCamera()` sets perspective FOV/near/far but never sets
   `orthographic = false` (`GarageCamera.cs:71-88`). FOV and zoom therefore do not work as
   intended.
4. **Stateful material is fragile in builds.** `ApplySurfaceMaterial()` finds the custom
   shader only by string and silently falls back to `Standard`
   (`GameManager.cs:789-833`). `LittleGarage/CarSurface` is absent from
   `ProjectSettings/GraphicsSettings.asset:29-37` Always Included Shaders and is not
   referenced by any material asset, so it can be stripped in a player; then `_StateMap`
   has no visual effect.
5. **Material replacement is too narrow.** Only renderers with a `MeshCollider` and material
   names containing `Body` are upgraded (`GameManager.cs:817-830`); accent panels and other
   registered paint targets keep static materials.
6. **UV atlas concern — ruled out for painting.** Trim and wheel meshes reuse a small
   rectangle, but they are registered as non-paint visuals. The ten registered paint meshes
   (`Body`, `Cabin`, `Roof`, `Hood`, `Rear`, two doors, two fenders, rocker) occupy disjoint
   quadrants of the 256×256 state map. Existing coverage checks confirm full atlas reachability;
   no additional UV remapping was needed for the active wash path.

## 2. Root causes of the missing water stream

1. **No stream renderer exists in the active path.** `_waterFx`/`_foamFx` are pooled
   particle systems (`GameManager.cs:95-96`) that are never continuously played; only
   alternating `EffectFx.Burst()` calls emit impact particles (`:741-757`,
   `Effects/EffectFx.cs:44-66`). There is no `LineRenderer`, nozzle, hose, or aimed stream.
2. **No visible tool.** `ToolController.CreateHandTool()` contains a pressure-gun/nozzle
   model but is never called by `GameManager` (`ToolController.cs:38-85`;
   `GameManager.cs:135-136,285-350`).
3. **Holding still stops feedback.** `InputRouter` emits `PointerMove` only after a position
   threshold (`InputRouter.cs:81-88,158-165`), and `GameManager.Update()` never repaints or
   refreshes effects while the pointer remains down (`GameManager.cs:148-173`).
4. **Raycasts accept non-paint colliders.** `TryHit()` accepts any collider under the vehicle
   (`GameManager.cs:724-739`), including lights and bumpers whose UVs do not correspond to
   the state atlas.
5. **Six tools are state-distinct but visually undifferentiated.** Pressure/rinse share the
   same burst pool; foam/sponge share another; dryer/polisher emit two particles
   (`GameManager.cs:752-757`). No dedicated airflow, sponge, foam gun, or polishing pad.

## 3. Root causes of the reported task-selection leakage

- The reported screenshot most likely came from the older checkout that was still at
  `22704fb`: its `StartJob()` sets `_screen = Play` but returns before `_ui.ShowPlay(...)`
  when a task has no tools (`old GameManager.cs:200,251-252,276`), leaving Garage visible.
  The STEP 4 commit already fixed the ordering and routes all visibility through
  `UIManager.ApplyVisibility()` (`UIManager.cs:512-539`), with a regression test at
  `Tests/PlayMode/UiFlowTests.cs:46-65`. No path in the current clone reactivates Garage
  during gameplay.
- We will not add an opaque cover or weaken the existing regression test. Instead we will add
  runtime diagnostics and real PlayMode screenshot captures that record active screen state,
  primary-screen count, canvas count, and visible job-card count at gameplay time.
- One real remaining HUD gap: the instruction band never hides after the first successful
  interaction, although the prompt allows/asks for it (`UIManager.cs:528-530`).

## 4. Planned fix directions

- Correct all procedural triangle winding/normals and add mesh-normal consistency tests.
- Force the active gameplay camera to perspective; verify vehicle bounds in the frustum.
- Add an always-included shader entry and use explicit paint renderers from `VehicleInstance`.
- Add a reusable `WaterStreamFX` with nozzle, LineRenderer stream, impact particles and
  per-tool colours/shapes; drive it continuously from `Update()` while the pointer is held.
- Restrict painting raycasts to registered paint colliders.
- Compact the HUD bands and bottom tool bar without reducing touch targets below 96 units.
- Add PlayMode tests for renderers/bounds/frustum/materials, stream lifecycle, raycast/UV,
  surface change, screen isolation, and deterministic RenderTexture screenshots at 16:9,
  16:10 and 4:3 through the existing CI artifact path.
