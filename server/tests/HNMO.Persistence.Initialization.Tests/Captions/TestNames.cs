// <copyright file="TestNames.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests.Captions;

using System.Resources;

/// <summary>
/// Strongly typed test resources: Lorencia (zh-CN), Devias (zh-CN, de).
/// </summary>
public static class TestNames
{
    /// <summary>
    /// Gets the resource manager.
    /// </summary>
    public static ResourceManager ResourceManager { get; } = new("HNMO.Persistence.Initialization.Tests.Captions.TestNames", typeof(TestNames).Assembly);

    /// <summary>
    /// Gets the Lorencia name.
    /// </summary>
    public static string Lorencia => ResourceManager.GetString(nameof(Lorencia))!;

    /// <summary>
    /// Gets the Devias name.
    /// </summary>
    public static string Devias => ResourceManager.GetString(nameof(Devias))!;
}
