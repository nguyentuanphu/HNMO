// <copyright file="WebsitePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// Settings of the public website: the secret address under which the website opens the admin panel,
/// and the Google client id for "Sign in with Google".
/// </summary>
[PlugIn]
[Display(Name = nameof(Properties.PlugInResources.WebsitePlugIn_Name), Description = nameof(Properties.PlugInResources.WebsitePlugIn_Description), ResourceType = typeof(Properties.PlugInResources))]
[Guid("7A3E9C51-2D84-4B6F-9E07-51C8D2A4F6B3")]
public class WebsitePlugIn : IFeaturePlugIn, ISupportCustomConfiguration<WebsiteConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <inheritdoc />
    public WebsiteConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig()
    {
        return new WebsiteConfiguration();
    }
}
