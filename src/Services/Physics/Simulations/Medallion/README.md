# Medallion artwork

The VW and Flavor Flav renderers load their own SVGs from the `Artwork` folder.
Files in that folder are copied to the build and publish output. Rebuild and deploy after changing source artwork, and restart the application to reload cached artwork.
The SVG is used for shape and transparency; the renderer applies the silver finish.

Each SVG needs three separate groups with these case-sensitive IDs. Inkscape layer names are not used.

| Group ID    | Purpose                                         |
| ----------- | ----------------------------------------------- |
| `body`      | Pendant body                                    |
| `loop`      | Loop, with a transparent hole                   |
| `loop-hole` | Filled shape marking the chain attachment point |

To set the group IDs in Inkscape:

1. Open the XML Editor with Ctrl+Shift+X.
2. Select the `<g>` element for a layer, not the path inside it.
3. Select its `id` attribute, enter the corresponding group ID above, and press Ctrl+Enter.
4. Repeat for the other layers and save the SVG.

The hole marker may be hidden in the SVG. Its bounding-box center must be above and horizontally
aligned with the body bounding-box center. The body bounds determine the pendant's center and scale.
The SVG must have a valid `viewBox`, with width and height at most 1024 units.

The renderer draws the rear chain, loop, front chain, then body. Clock markings and hands are drawn
over the body. The loader caches the rasterized shapes when a simulation first starts.
