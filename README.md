# PlanAstro for N.I.N.A.

One-tap handoff of framed targets from [PlanAstro](https://planastro.com) to [N.I.N.A.](https://nighttime-imaging.eu)

Plan and frame a target in PlanAstro on your phone or computer, tap **Send framing to N.I.N.A.**, and it arrives in this plugin's inbox inside NINA, ready to open in the Framing Assistant.

> **Status: 1.0.** Install it from NINA's plugin list (*Plugins > Available > PlanAstro for NINA*) or from the [releases page](https://github.com/PlanAstro/nina-plugin-planastro/releases). Support: open an issue here or email support.planastro@gmail.com.

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
5. In PlanAstro, open *Menu > Connect NINA > Get pairing code*, type the code on the plugin's page and press **Pair**.
6. Frame a target in PlanAstro (for example M31 with a 2×1 mosaic) and tap **Send framing to N.I.N.A.** Within one check interval it should appear in the inbox and open in the Framing Assistant with:
   - the same centre, rotation number, mosaic and overlap as in PlanAstro;
   - your rig's focal length, pixel size and sensor size under Camera Parameters (when *Frame with PlanAstro My Astro Rig* is on).

If the build fails or a step doesn't behave as described, copy the error (or the lines containing `[PlanAstro]` from NINA's log in `%LOCALAPPDATA%\NINA\Logs`) into an issue or the PlanAstro chat.

## Releasing

Pushing a tag such as `v0.1.0.0` runs the GitHub Action, which builds the plugin on Windows and publishes a release containing:

- `PlanAstro.NINA.zip`: the plugin NINA downloads;
- `manifest.json`: the entry for [NINA's plugin manifest repository](https://github.com/isbeorn/nina.plugin.manifests), with the download URL and SHA256 checksum filled in.

Bump `AssemblyVersion` in `src/PlanAstro.NINA/Properties/AssemblyInfo.cs` before tagging. A released zip must never be replaced, or its checksum stops matching.

## Format of a sent target

```json
{
  "v": 2,
  "id": "b7f1c2e4-…",
  "name": "M31 Andromeda Galaxy",
  "ra": 10.6847,
  "dec": 41.2690,
  "rotation": 325.0,
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

Coordinates are J2000 degrees and are the centre of the frame as composed (after any Shift X/Y), not the catalog position; `surveyFovDeg` (optional) is the sky image width from Survey Zoom; rotation is the Rotate value from PlanAstro's Framing Assistant in degrees: 0 = camera long side horizontal with north up, increasing counterclockwise from 0 to 360 — the same numbers NINA and plate solvers use, so the plugin passes it straight through. Payloads with `"v": 1` (older PlanAstro builds counted clockwise) are converted with 360 − rotation.

## Licence

MIT. See [LICENSE](LICENSE).
