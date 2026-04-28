# Rat Imagen Sprite Pack

Image-generated raster source sheets only. This pack intentionally contains no procedural frame folders, no SVG assets, and no Python/tools.

## Source Sheets

- `Source/Rat_Imagen_Idle_Chroma.png`
- `Source/Rat_Imagen_Scurry_Chroma.png`
- `Source/Rat_Imagen_Pounce_Chroma.png`
- `Source/Rat_Imagen_Bite_Chroma.png`
- `Source/Rat_Imagen_Sniff_Chroma.png`

## Import Notes

- Chroma key: flat magenta `#ff00ff`.
- Intended angle order per sheet: `Front`, `FrontRight`, `Right`, `BackRight`, `Back`.
- Intended action timing: read columns left-to-right as animation beats for that row.
- Preserve the rat as a small, low silhouette. Keep the belly/paw baseline stable between clips.
- Unity-generated frames/maps, if present, are import artifacts derived from these Imagen sheets.
