# Dog Imagen Sprite Pack

Image-generated raster source sheets only. This pack intentionally contains no procedural frame folders, no SVG assets, and no Python/tools.

## Source Sheets

- `Source/Dog_Imagen_Idle_Chroma.png`
- `Source/Dog_Imagen_Trot_Chroma.png`
- `Source/Dog_Imagen_Sprint_Chroma.png`
- `Source/Dog_Imagen_Bite_Chroma.png`
- `Source/Dog_Imagen_Howl_Chroma.png`

## Import Notes

- Chroma key: flat magenta `#ff00ff`.
- Intended angle order per sheet: `Front`, `FrontRight`, `Right`, `BackRight`, `Back`.
- Intended action timing: read columns left-to-right as animation beats for that row.
- Preserve the dog as a lean, medium-sized quadruped silhouette. Keep the paw baseline stable between clips.
- Unity-generated frames/maps, if present, are import artifacts derived from these Imagen sheets.
