# Changelog

## 0.1.1.0 (in testing)

- When NINA has no telescope focal length set, the Framing Assistant uses the rig sent from PlanAstro (focal length, pixel size, sensor size), so the sky image loads instead of showing an error.
- Opening a target now loads the sky image automatically, then applies the mosaic layout and rotation.
- Sent targets can include the camera's resolution (`widthPx`, `heightPx`).
- Writes the Framing Assistant's settable properties to NINA's log once, to pin down rotation in the next version.

## 0.1.0.0 (in testing)

- Pair with a PlanAstro account using a 6-character code.
- PlanAstro inbox on the plugin page: sent targets with coordinates, rotation, mosaic and rig.
- Open in Framing Assistant, by hand or automatically on arrival.
- Add test target (M31) for trying the Framing Assistant handoff without an account.
