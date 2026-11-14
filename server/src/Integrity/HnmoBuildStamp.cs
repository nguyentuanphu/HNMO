// <copyright file="HnmoBuildStamp.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Integrity;

using System.Reflection;

/// <summary>
/// The HNMO build stamp: a fixed, compiled-in ownership marker for this build.
/// It is made of the project identifier (see <c>PROJECT_ID.txt</c> in the source
/// repository), the build date and the literal <c>HNMO</c>. The stamp is baked into
/// the compiled assembly so it travels with the shipped binaries and can be used to
/// prove that a given folder is a build of this project.
/// </summary>
public static class HnmoBuildStamp
{
    /// <summary>
    /// The fixed project identifier for this build. Generated once and kept in
    /// <c>PROJECT_ID.txt</c> at the repository root.
    /// </summary>
    public const string ProjectId = "1abdb72e-f72e-457c-93d5-3141ad991109";

    /// <summary>
    /// The date (UTC, <c>yyyyMMdd</c>) this build stamp was minted.
    /// </summary>
    public const string BuildDate = "20261114";

    /// <summary>
    /// The full build stamp string compiled into the server. Shape:
    /// <c>HNMO-&lt;projectId&gt;-&lt;buildDate&gt;</c>.
    /// </summary>
    public const string Stamp = "HNMO-" + ProjectId + "-" + BuildDate;

    /// <summary>
    /// Gets the build stamp, preferring the assembly metadata attribute when present
    /// (which lets tooling read it without referencing this type), and falling back to
    /// the compiled-in constant.
    /// </summary>
    /// <returns>The build stamp string.</returns>
    public static string Resolve()
    {
        var fromAttribute = typeof(HnmoBuildStamp).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "HNMO.BuildStamp")?.Value;
        return string.IsNullOrEmpty(fromAttribute) ? Stamp : fromAttribute;
    }
}
