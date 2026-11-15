# PlanAstro for N.I.N.A.

One-tap handoff of framed targets from [PlanAstro](https://planastro.com) to [N.I.N.A.](https://nighttime-imaging.eu)

Plan and frame a target in PlanAstro on your phone or computer, tap **Send to NINA**, and it arrives in this plugin's inbox inside NINA, ready to open in the Framing Assistant.

> **Status: first version (0.1), in testing.** The PlanAstro side (Connect NINA, Send to NINA) is being built in parallel; until it is live, use **Add test target (M31)** to try the Framing Assistant handoff.

## What it does

- **Pair once.** In PlanAstro open *Menu > Connect NINA*, then type the 6-character code on this plugin's page in NINA.
- **Inbox.** Targets you send appear on the plugin's page with coordinates, rotation, mosaic layout and rig.
- **Framing Assistant.** Open any target with one click, or let the plugin open new arrivals automatically.

Privacy and security:

- The plugin only makes outgoing HTTPS requests to planastro.com. It opens no ports on your PC.
- The device token is encrypted for your Windows user (DPAPI) and kept in `%LOCALAPPDATA%\NINA\PlanAstro\settings.json`.
- Disconnect from either side at any time: here, or in PlanAstro under *Menu > Connect NINA*.

## Build and test on Windows

You need Windows 10 or 11, N.I.N.A. 3.1 or later, the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and Visual Studio 2022 or later with the **.NET desktop development** workload.

1. Close N.I.N.A.
2. Clone this repository and open `src/PlanAstro.NINA/PlanAstro.NINA.csproj` in Visual Studio.
3. Build (Debug or Release). The build copies the plugin into `%LOCALAPPDATA%\NINA\Plugins\3.0.0\PlanAstro for NINA\`.
4. Start N.I.N.A. and open *Plugins > Installed > PlanAstro for NINA*.
5. Click **Add test target (M31)**, then **Open in Framing Assistant**. Check that:
   - the Framing Assistant opens centred on M31 and loads the sky image by itself (if NINA has no telescope focal length set, the plugin uses the rig sent from PlanAstro);
   - the rotation reads 325° (PlanAstro's 35°, which NINA counts the other way), with the frame along the galaxy's long axis;
   - the mosaic shows 2 × 1 panels with 15% overlap.

If the build fails or a step doesn't behave as described, copy the error (or the lines containing `[PlanAstro]` from NINA's log in `%LOCALAPPDATA%\NINA\Logs`) into an issue or the PlanAstro chat.

## Releasing

Pushing a tag such as `v0.1.0.0` runs the GitHub Action, which builds the plugin on Windows and publishes a release containing:

- `PlanAstro.NINA.zip`: the plugin NINA downloads;
- `manifest.json`: the entry for [NINA's plugin manifest repository](https://github.com/isbeorn/nina.plugin.manifests), with the download URL and SHA256 checksum filled in.

Bump `AssemblyVersion` in `src/PlanAstro.NINA/Properties/AssemblyInfo.cs` before tagging. A released zip must never be replaced, or its checksum stops matching.

## Format of a sent target

```json
{
  "v": 1,
  "id": "b7f1c2e4-…",
  "name": "M31 Andromeda Galaxy",
  "ra": 10.6847,
  "dec": 41.2690,
  "rotation": 35.0,
  "mosaic": { "columns": 2, "rows": 1, "overlapPercent": 15 },
  "panels": [ { "name": "M31 Panel 1", "ra": 10.12, "dec": 41.27 } ],
  "fov": { "widthDeg": 2.80, "heightDeg": 1.87 },
  "surveyFovDeg": 3.41,
  "rig": { "telescope": "Evostar 80ED", "reducer": 0.8, "focalLengthMm": 480, "camera": "ASI2600MC Pro", "pixelSizeUm": 3.76, "widthPx": 6248, "heightPx": 4176 },
  "filters": [ { "filter": "Ha", "count": 30, "seconds": 300 } ],
  "note": "Frame with the dust lane diagonal",
  "sentAt": "2026-11-12T23:40:00Z"
}
```

Coordinates are J2000 degrees and are the centre of the frame as composed (after any Shift X/Y), not the catalog position; `surveyFovDeg` (optional) is the sky image width from Survey Zoom; rotation is the angle from PlanAstro's Framing Assistant Rotate slider in degrees (0 = camera long side horizontal, increasing clockwise on screen with north up). NINA turns the other way, so the plugin opens it in NINA as 360 − rotation (35° in PlanAstro shows as 325° in NINA).

## Licence

MIT. See [LICENSE](LICENSE).
