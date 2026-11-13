# Changelog

## 0.1.3.0 (in testing)

- Mosaic panels and rotation now stick: the plugin waits for the sky image to finish loading (NINA resets the frame to 1×1 and 0° when it does), applies them, checks they held and re-applies if not.
- Settings read as full sentences: "Open targets in the Framing Assistant when they arrive" and "Frame with PlanAstro My Astro Rig". (NINA shows checkboxes as ON/OFF switches without their text, so the label carries the meaning.)

## 0.1.2.0 (in testing)

- Frames with the rig the target was planned with in PlanAstro by default, so NINA shows exactly what was framed. A setting switches to NINA's own telescope and camera.
- Mosaic overlap now applies correctly (sets NINA's overlap unit to % and its value), instead of staying at NINA's 20% default.
- Rotation is set through the Framing Assistant's rotation slider (RectangleRotation).

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
