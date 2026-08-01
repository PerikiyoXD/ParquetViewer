using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

[assembly: AssemblyTitle("ParquetViewer (PerikiyoXD fork)")]
[assembly: AssemblyDescription("Simple Windows desktop application for viewing Apache Parquet files\r\n" +
    "Unofficial fork: https://github.com/PerikiyoXD/ParquetViewer\r\n" +
    "Upstream: https://github.com/mukunku/ParquetViewer\r\n\r\n" +
    "Privacy policy: https://github.com/mukunku/ParquetViewer/wiki/Privacy-Policy")]
[assembly: AssemblyConfiguration("fork")]
[assembly: AssemblyCompany("Author: Mukunku; fork maintained by PerikiyoXD")]
[assembly: AssemblyProduct("ParquetViewer")]
[assembly: AssemblyCopyright("GNU General Public License v3.0")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: SupportedOSPlatform("windows")]
[assembly: ComVisible(false)]
[assembly: Guid("134e9435-2fb2-4297-ac49-d44477a427bc")]

// Version information for an assembly consists of the following four values:
//      Major Version
//      Minor Version
//      Patch Version
//      Revision
//
// Fork builds keep the upstream major.minor.patch they were branched from and use a revision of 100 or
// above, which upstream has never used. That keeps fork builds identifiable and avoids ever claiming a
// version number upstream might later release. SemanticVersion only parses numbers, so the fork can't be
// marked with a suffix here; AssemblyInformationalVersion below carries the human readable label.
[assembly: AssemblyVersion("4.2.0.100")]
[assembly: AssemblyInformationalVersion("4.2.0.100+fork.PerikiyoXD")]
