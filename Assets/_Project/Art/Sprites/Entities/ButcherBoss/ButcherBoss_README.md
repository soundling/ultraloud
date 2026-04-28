# ButcherBoss Imagen Sprite Pack

Image-generated raster source sheets only. This pack intentionally contains no procedural frame folders, no SVG assets, and no Python/tools.

## Source Sheets

- `Source/ButcherBoss_Imagen_Idle_Chroma.png`
- `Source/ButcherBoss_Imagen_Walk_Chroma.png`
- `Source/ButcherBoss_Imagen_Cleaver_Chroma.png`
- `Source/ButcherBoss_Imagen_Slam_Chroma.png`
- `Source/ButcherBoss_Imagen_Roar_Chroma.png`

## Import Notes

- Chroma key: flat magenta `#ff00ff`.
- Intended angle order per sheet: `Front`, `FrontRight`, `Right`, `BackRight`, `Back`.
- Intended action timing: read columns left-to-right as animation beats for that row.
- Preserve the butcher as a tall boss-scale silhouette. Keep the foot baseline stable between clips.
- For shader work, derive glow/spec response in Unity from the painted highlights, eyes, cleaver edge, wet gore, and apron shine rather than importing generated map folders.
- Recommended Unity setup: `Sprite (2D and UI)`, `Multiple`, no compression for source import, then slice manually from the visual sheet boundaries.
