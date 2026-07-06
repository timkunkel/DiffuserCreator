---
name: diffuser-mesh-geometry
description: The hand-built geometry of a DiffuserBlock — the 8 corner points, the 24-vertex / 12-triangle layout and winding, how the four per-corner depths deform the front face, and the line-line-intersection math (now in GeometryUtils, called by CurveDepthShaper) that tilts the face for Angle curves. Load this BEFORE editing mesh construction, vertex/triangle arrays, normals, depth application, or the angle-tilt calculation, or when a block renders inside-out, has seams, or Angle mode looks wrong.
---

# DiffuserBlock Mesh & Geometry

`DiffuserBlock` ([DiffuserBlock.cs](../../../Assets/Scripts/DiffuserBlock.cs)) builds its mesh by hand rather than deforming a Unity primitive, because each of the four front-face corners moves independently and the collider must match exactly. Verified 2026-07-06 against `master` on Unity 6000.3.9f1. All coordinates are **local** to the block; `transform.localScale` (set by `SetSize`) scales the unit cube to world size, so geometry math never touches world units.

The block is now pure geometry — it exposes `SetUniformDepth(float)` and `SetDepths(d0,d1,d2,d3)` (both apply + rebuild), `InitialDepth`, `NormalizedPosition`, `Angle`, and the static `BackCorners`. It does not decide depths; a `DepthShaper` does (see `diffuser-architecture`).

## The 8 corner points

`InitPoints()` sets the **back face** (z = 0, against the mounting plane) from `BackCorners`; `ApplyDepthToPoints()` derives the **front face** (z = −depth):

| Index | Local XY | Z | Corner |
|---|---|---|---|
| 0 | ( 0.5, −0.5) | 0 | back bottom-right |
| 1 | ( 0.5,  0.5) | 0 | back top-right |
| 2 | (−0.5,  0.5) | 0 | back top-left |
| 3 | (−0.5, −0.5) | 0 | back bottom-left |
| 4 | ( 0.5, −0.5) | −`_depth[0]` | front bottom-right |
| 5 | ( 0.5,  0.5) | −`_depth[1]` | front top-right |
| 6 | (−0.5,  0.5) | −`_depth[2]` | front top-left |
| 7 | (−0.5, −0.5) | −`_depth[3]` | front bottom-left |

So `_depth[i]` corresponds to `_points[4 + i]`, and the four depths map to corners **BR, TR, TL, BL** in that order. `public static readonly Vector3[] BackCorners` holds points 0-3 and is the single source both the mesh and `CuttingDepthShaper` (ray origins) use.

`SetDepths`/`SetUniformDepth` write `_depth`, call `ApplyDepthToPoints()`, then `BuildMesh()`. Nothing ever moves points 0-3.

## The 24-vertex / 12-triangle box

`BuildMesh()` assigns `Vertices` (24 entries) and `Triangles` (36 indices), then `RecalculateNormals()` and copies the mesh to the `MeshCollider`. Vertices are **duplicated per face** (4 per face × 6 faces) so each face gets flat normals — hence 24 vertices for 8 corners.

`Vertices` face order and which points feed each face:

| Face | Vertex slots | Points |
|---|---|---|
| back | 0-3 | p0 p1 p2 p3 |
| front | 4-7 | p4 p5 p6 p7 |
| left | 8-11 | p2 p3 p6 p7 |
| right | 12-15 | p0 p1 p4 p5 |
| top | 16-19 | p1 p2 p5 p6 |
| bottom | 20-23 | p0 p3 p4 p7 |

`Triangles` indexes into those 24 slots (two triangles per face). Winding is chosen so normals face outward after `RecalculateNormals()`:

```
back:   0 1 2 / 0 2 3
front:  7 6 5 / 7 5 4
left:   9 8 10 / 9 10 11
right: 13 12 14 / 13 14 15
top:   17 16 18 / 17 18 19
bottom:20 21 23 / 20 23 22
```

**If a block renders inside-out or a face is missing:** the winding for that face is wrong or the vertex slots don't match the table. Back and front use *opposite* winding (front reversed) because they face opposite directions. `RecalculateNormals()` means you fix orientation by fixing *winding*, not by editing normals.

## Angle mode — tilting the front face (in CurveDepthShaper)

The tilt math lives in `CurveDepthShaper.ShapeWithAngle` ([DepthShaper.cs](../../../Assets/Scripts/DepthShaper.cs)), not the block. Goal: instead of just deepening the block, tilt its front face by a snapped angle so neighboring blocks form a continuous faceted curve.

1. Accumulate a target angle from the enabled curves (diagonal at `(x+y)/2`, horizontal at `x`, vertical at `y`), averaged over the enabled count.
2. `angle = (int)Snapping.Snap(angleValue, Mathf.Max(1, settings.SnapAngle))` — snap to a multiple of `SnapAngle` degrees (ProBuilder `UnityEngine.ProBuilder.Snapping`). Stored in `block.Angle` (read by `DiffuserGrid.PrintGrid` to histogram angles). Note the `Max(1, …)` guard against a zero snap step.
3. Compute the front right-edge from the block's **initial depth** analytically (`frontBottom`/`frontTop` at `z = −InitialDepth`, `backTopRight` at `z = 0`), rotate the edge by `angle` about X, and use `GeometryUtils.LineLineIntersection` to find where the rotated edge meets the plane through the back/front top-right corners. The distance from the front-top corner to that intersection is the extra depth.
4. `block.SetDepths(initial, initial, initial + extra, initial + extra)` — corners BR/TR stay at initial depth, TL/BL go deeper, so the face tilts across the block. **If the lines don't intersect** (`LineLineIntersection` returns false), it falls back to `block.SetUniformDepth(initial)` instead of applying a bogus depth — this guard is deliberate; the original prototype ignored the return value and produced a garbage depth from a `Vector3.zero` non-intersection.

`GeometryUtils.LineLineIntersection` ([GeometryUtils.cs](../../../Assets/Scripts/GeometryUtils.cs)) is a standard coplanar-lines solver: returns false (and `Vector3.zero`) when the lines are parallel or non-coplanar (`|planarFactor| >= 0.0001` or near-zero cross product).

`CurveDepthShaper.ShapeWithHeight` is the easy sibling: one flat depth `InitialDepth * clamp01(value)` (the curve value is a fraction of the block's depth), no geometry.

## Vertex indicators

`ShowIndicators()` lazily instantiates one `VertexIndicator` per `_points` entry (8 total) at the point's local position, labeled with its index (guarded against a null `_vertexIndicatorPrefab`); `HideIndicators()` deactivates them. Turn them on to see which corner a depth maps to and compare against the point table.

## When NOT to use this skill

- Where depths come from (strategies, curves, settings) → `diffuser-architecture`.
- Clicking buttons, the UI panel, exporting → `diffuser-editor-workflows`.
- ProBuilder dependency / build gotchas → `diffuser-build-and-run`.

## Provenance and maintenance

Verified 2026-07-06 against `master` HEAD `93b81d8`. Re-verify:

```powershell
# Point table, vertex/triangle arrays, depth API
Select-String -Path "Assets\Scripts\DiffuserBlock.cs" -Pattern "InitPoints|Vertices =>|Triangles =>|ApplyDepthToPoints|BackCorners|SetDepths"
# Angle tilt + intersection (now in the shaper + util)
Select-String -Path "Assets\Scripts\DepthShaper.cs" -Pattern "ShapeWithAngle|Snapping.Snap|LineLineIntersection"
Select-String -Path "Assets\Scripts\GeometryUtils.cs" -Pattern "LineLineIntersection"
```

If output contradicts this file, trust the code and update this skill in the same change.
