# AbominationMonster Imagen Sprite Pack

Image-generated raster source sheets only. This pack intentionally contains no procedural frame folders, no SVG assets, and no Python/tools.

## Source Sheets

- `Source/AbominationMonster_Imagen_Idle_Chroma.png`
- `Source/AbominationMonster_Imagen_Scuttle_Chroma.png`
- `Source/AbominationMonster_Imagen_Lunge_Chroma.png`
- `Source/AbominationMonster_Imagen_Spit_Chroma.png`
- `Source/AbominationMonster_Imagen_Roar_Chroma.png`

## Import Notes

- Chroma key: flat magenta `#ff00ff`.
- Intended angle order per sheet: `Front`, `FrontRight`, `Right`, `BackRight`, `Back`.
- Intended action timing: read columns left-to-right as animation beats for that row.
- Preserve the abomination as a low, wide, ground-hugging silhouette. Keep the belly/foot baseline stable between clips.
- For shader work, derive emissive pulsing in Unity from the yellow-green sacs, eyes, spit, and wet tissue highlights rather than importing generated map folders.
- Recommended Unity setup: `Sprite (2D and UI)`, `Multiple`, no compression for source import, then slice manually from the visual sheet boundaries.
