using System.Reflection;
using System.Runtime.InteropServices;

// NINA reads these values to show the plugin in its Plugins tab.
// [Guid] is the plugin's permanent identifier: never change it.
[assembly: Guid("fd400fc7-093c-4f75-94f4-fc6fda5683ad")]

[assembly: AssemblyTitle("PlanAstro for NINA")]
[assembly: AssemblyDescription("Send framed targets from PlanAstro to N.I.N.A.")]
[assembly: AssemblyCompany("PlanAstro")]
[assembly: AssemblyProduct("PlanAstro for NINA")]
[assembly: AssemblyCopyright("Copyright © 2026 PlanAstro")]

// Major.Minor.Patch.Build
[assembly: AssemblyVersion("0.1.2.0")]
[assembly: AssemblyFileVersion("0.1.2.0")]

[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.1.2.9001")]
[assembly: AssemblyMetadata("License", "MIT")]
[assembly: AssemblyMetadata("LicenseURL", "https://github.com/PlanAstro/nina-plugin-planastro/blob/main/LICENSE")]
[assembly: AssemblyMetadata("Repository", "https://github.com/PlanAstro/nina-plugin-planastro")]
[assembly: AssemblyMetadata("Homepage", "https://planastro.com")]
[assembly: AssemblyMetadata("Tags", "PlanAstro,Framing,Planning,Targets,Mosaic")]
[assembly: AssemblyMetadata("ChangelogURL", "https://github.com/PlanAstro/nina-plugin-planastro/blob/main/CHANGELOG.md")]
[assembly: AssemblyMetadata("FeaturedImageURL", "")]
[assembly: AssemblyMetadata("ScreenshotURL", "")]
[assembly: AssemblyMetadata("AltScreenshotURL", "")]
[assembly: AssemblyMetadata("LongDescription", @"Plan and frame a target in PlanAstro on your phone or computer, tap Send to NINA, and it arrives here.

* Pair once with a 6-character code from PlanAstro (Menu > Connect NINA).
* Sent targets appear in the PlanAstro inbox on this plugin's page, with coordinates, rotation, mosaic and rig.
* Open any target in the Framing Assistant with one click, or let the plugin open new arrivals automatically.

The plugin only makes outgoing HTTPS requests to planastro.com. It opens no ports and stores its device token encrypted for your Windows user.")]

[assembly: ComVisible(false)]
