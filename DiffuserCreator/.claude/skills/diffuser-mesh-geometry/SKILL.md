---
name: diffuser-mesh-geometry
description: The hand-built geometry of a DiffuserBlock — the 8 corner points, the 24-vertex / 12-triangle layout and winding, how the four per-corner depths deform the front face, and the line-line-intersection math that tilts the face for Angle curves. Load this BEFORE editing mesh construction, vertex/triangle arrays, normals, depth application, or the angle-tilt calculation, or when a block renders inside-out, has seams, or the angle mode looks wrong.
---

# DiffuserBlock Mesh & Geometry

`DiffuserBlock` ([DiffuserBlock.cs](../../../Assets/Scripts/DiffuserBlock.cs)) builds its mesh by hand rather than deforming a Unity primitive, because each of the four front-face corners moves independently and the collider must match exactly. Verified 2026-07-06 against `master`. All coordinates are **local** to the block; `transform.localScale` (set by `SetSize`) scales the unit cube to world size, so geometry math never touches world units.

## The 8 corner points

`InitPoints()` fills `_points[0..7]`. The **back face** (z = 0, against the mounting plane) and the **front face** (z = −depth, pushed toward the viewer):

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

So `_depth[i]` corresponds to `_points[4 + i]`, and the four depths map to corners **BR, TR, TL, BL** in that order. `_bottomPoints` = back face (0-3), `_topPoints` = front face (4-7) — the names refer to depth start/end, not up/down.

Depth flows into geometry through two setters that both call `ApplyDepthToPoints()` (which recomputes `_points[4..7]` from `_depth`):
- `SetDepth(float d)` → all four equal.
- `SetDepth(d0,d1,d2,d3)` → per corner.

`CutWithSurface` and the curve methods only ever write `_depth` and then `BuildMesh()`; they never touch `_points[0..3]`.

## The 24-vertex / 12-triangle box

`BuildMesh()` assigns `Vertices` (24 entries) and `Triangles` (36 indices), then `RecalculateNormals()` and copies the mesh to the `MeshCollider`. Vertices are **duplicated per face** (4 per face × 6 faces) so each face gets flat normals — this is why there are 24 vertices for 8 corners.

`Vertices` face order and which points feed each face:

| Face | Vertex slots | Points |
|---|---|---|
| back | 0-3 | p0 p1 p2 p3 |
| front | 4-7 | p4 p5 p6 p7 |
| left | 8-11 | p2 p3 p6 p7 |
| right | 12-15 | p0 p1 p4 p5 |
| top | 16-19 | p1 p2 p5 p6 |
| bottom | 20-23 | p0 p3 p4 p7 |

`Triangles` indexes into those 24 slots (two triangles per face). The winding per face is chosen so normals face outward after `RecalculateNormals()`:

```
back:   0 1 2 / 0 2 3
front:  7 6 5 / 7 5 4
left:   9 8 10 / 9 10 11
right: 13 12 14 / 13 14 15
top:   17 16 18 / 17 18 19
bottom:20 21 23 / 20 23 22
```

**If a block renders inside-out or a face is missing:** the winding for that face block is wrong or the vertex slots don't match the table above. Note back and front use *opposite* winding (front is reversed) because they face opposite directions. `RecalculateNormals()` means you fix orientation by fixing *winding*, not by editing normals.

## Angle mode — tilting the front face

`SetDepthWithAngleCurve()` is the subtle part. Goal: instead of just making the block deeper, tilt its front face by a snapped angle so neighboring blocks form a continuous faceted curve.

1. Accumulate a target angle from the enabled curves (diagonal evaluated at `(relX+relY)/2`, horizontal at `relX`, vertical at `relY`), averaged over the count of enabled curves.
2. `Angle = (int)Snapping.Snap(horizontalAngle, _diffuserGrid.SnapAngle)` — snap to a multiple of `SnapAngle` degrees (ProBuilder `UnityEngine.ProBuilder.Snapping`, `public int Angle { get; private set; }` is read by `DiffuserGrid.PrintGrid` to histogram angles).
3. Reset to a flat `_initialDepth`, then take the right-edge front vector `dir1 = _points[5] − _points[4]`, rotate it by `Angle` about X (`Quaternion.Euler(Angle,0,0) * dir1`), and use `LineLineIntersection` to find where the rotated edge meets the plane through `_points[1]`/`_points[5]`. The distance from `_points[5]` to that intersection is the extra depth `moreDepth`.
4. `SetDepth(_initialDepth, _initialDepth, moreDepth, moreDepth)` — corners BR/TR stay at initial depth, TL/BL go to `moreDepth`, so the face tilts across the block.

`LineLineIntersection(out intersection, p1, v1, p2, v2)` is a standard coplanar-lines solver: returns false (and `Vector3.zero`) when the lines are parallel or non-coplanar (`|planarFactor| >= 0.0001` or near-zero cross product). **If angle mode produces a zero or garbage depth, check its return value** — the current caller ignores it, so a non-intersection silently yields `moreDepth = _initialDepth + |p5 - zero|`, a large bogus depth. This is a real fragility to fix when refactoring.

`SetDepthWithHeightCurve()` is the easy sibling: one flat depth `_initialDepth + value * _initialDepth`, no geometry.

## Vertex indicators

`ShowIndicators()` lazily instantiates one `VertexIndicator` per `_points` entry (8 total) at the point's local position, labeled with its index; `HideIndicators()` deactivates them. Useful when debugging which corner a depth maps to — turn them on and compare against the point table above.

## When NOT to use this skill

- Where depths come from (modes, curves, sequences) → `diffuser-architecture`.
- Clicking buttons, exporting the mesh → `diffuser-editor-workflows`.
- ProBuilder dependency / build gotchas → `diffuser-build-and-run`.

## Provenance and maintenance

Verified 2026-07-06 against `master` HEAD `93b81d8`. Re-verify:

```powershell
# Point table, vertex/triangle arrays
Select-String -Path "Assets\Scripts\DiffuserBlock.cs" -Pattern "InitPoints|Vertices =>|Triangles =>|ApplyDepthToPoints"
# Angle tilt + intersection
Select-String -Path "Assets\Scripts\DiffuserBlock.cs" -Pattern "SetDepthWithAngleCurve|LineLineIntersection|Snapping.Snap"
```

If output contradicts this file, trust the code and update this skill in the same change.
