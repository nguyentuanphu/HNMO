// <copyright file="SharedAssemblyInfo.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System.Reflection;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("HNMO")]
[assembly: AssemblyProduct("HNMO")]
[assembly: AssemblyCopyright("Copyright © HNMO 2025")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// HNMO build stamp: a compiled-in ownership marker baked into every HNMO assembly.
// Shape: HNMO-<projectId>-<buildDate>. The project id lives in PROJECT_ID.txt at the
// repository root. See client/web/tools/hnmo/PROTECTION.md for how to read it from a
// suspect build.
[assembly: AssemblyMetadata("HNMO.ProjectId", "1abdb72e-f72e-457c-93d5-3141ad991109")]
[assembly: AssemblyMetadata("HNMO.BuildStamp", "HNMO-1abdb72e-f72e-457c-93d5-3141ad991109-20261114")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The version comes from the VERSION file at the repository's root (Directory.Build.props, HnmoVersionInfo).
