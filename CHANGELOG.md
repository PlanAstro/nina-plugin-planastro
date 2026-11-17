# Changelog

## 0.1.8.0 (in testing)

- Fixes "The JSON value could not be converted … panels": PlanAstro sends `panels` as a count; the plugin now accepts a count or a list (it was never used — NINA computes the panels).
- Each target is read on its own: one target in an unexpected shape no longer blocks the queue. It is reported back to PlanAstro as failed and NINA shows a notice.
- Number fields are read tolerantly (numbers as text, decimals for pixel counts, missing values).
- No longer sends a "delivered" acknowledgement, which PlanAstro doesn't accept (it marks targets delivered when the plugin fetches them). Targets are acknowledged as imported or failed after opening.

## 0.1.7.0 (in testing)

- Rotation numbers now match between PlanAstro and NINA. PlanAstro's Rotate slider counts counterclockwise from 0 to 360 like NINA (payload `"v": 2`), so the plugin passes the angle straight through: 325° in PlanAstro is 325° in NINA.
- Targets from older PlanAstro builds (`"v": 1`, clockwise) are still converted, so nothing breaks while both are in use.
- The inbox shows rotation in NINA's numbers.

## 0.1.6.0 (2026-11-14)

- Uses the sky image width from PlanAstro's Survey Zoom (`surveyFovDeg`) for NINA's Field of view, widened only if the mosaic wouldn't fit.

## 0.1.5.0

- Rotation now turns the same way as in PlanAstro. PlanAstro's Rotate slider turns the frame clockwise and NINA's turns it counterclockwise, so the plugin opens PlanAstro's angle in NINA as 360° − angle (35° in PlanAstro shows as 325° in NINA, and the frame points the same way in both).
- The test target (M31, 35°) now frames along the galaxy's long axis, as it does in PlanAstro.

## 0.1.4.0

- Rotation now matches the PlanAstro plan: NINA stores the frame rotation as 360 − position angle, so 35° was showing as 325°. The plugin now lets NINA apply the position angle itself, the same way NINA does for its own targets.
- Mosaic overlap fixed: NINA keeps overlap as a fraction (0.15 = 15%), so the earlier value of 15 meant 1500%, which threw the mosaic panel coordinates far off. The overlap box also refreshes to show the new value.
- The sky image loads once instead of twice: the rig, panels and overlap are set before NINA loads the image.
- The field of view widens when needed so the whole mosaic fits in the sky image (it is never narrowed).
- After loading, the plugin checks panels, overlap and rotation against the plan and re-applies them if NINA changed them.

## 0.1.3.0

- Mosaic panels and rotation now stick: the plugin waits for the sky image to finish loading (NINA resets the frame to 1×1 and 0° when it does), applies them, checks they held and re-applies if not.
- Settings read as full sentences: "Open targets in the Framing Assistant when they arrive" and "Frame with PlanAstro My Astro Rig". (NINA shows checkboxes as ON/OFF switches without their text, so the label carries the meaning.)

## 0.1.2.0

- Frames with the rig the target was planned with in PlanAstro by default, so NINA shows exactly what was framed. A setting switches to NINA's own telescope and camera.
- Mosaic overlap now applies correctly (sets NINA's overlap unit to % and its value), instead of staying at NINA's 20% default.
- Rotation is set through the Framing Assistant's rotation slider (RectangleRotation).

## 0.1.1.0

- When NINA has no telescope focal length set, the Framing Assistant uses the rig sent from PlanAstro (focal length, pixel size, sensor size), so the sky image loads instead of showing an error.
- Opening a target now loads the sky image automatically, then applies the mosaic layout and rotation.
- Sent targets can include the camera's resolution (`widthPx`, `heightPx`).
- Writes the Framing Assistant's settable properties to NINA's log once, to pin down rotation in the next version.

## 0.1.0.0

- Pair with a PlanAstro account using a 6-character code.
- PlanAstro inbox on the plugin page: sent targets with coordinates, rotation, mosaic and rig.
- Open in Framing Assistant, by hand or automatically on arrival.
- Add test target (M31) for trying the Framing Assistant handoff without an account.
