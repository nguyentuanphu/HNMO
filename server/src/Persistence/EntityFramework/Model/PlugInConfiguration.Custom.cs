// <copyright file="PlugInConfiguration.Custom.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.EntityFramework.Model;

using HNMO.DataModel;

/// <summary>
/// Custom additions to the generated <see cref="PlugInConfiguration"/>.
/// </summary>
internal partial class PlugInConfiguration : IAssignable, IAssignable<HNMO.PlugIns.PlugInConfiguration>, ICloneable<HNMO.PlugIns.PlugInConfiguration>
{
    /// <inheritdoc />
    public virtual HNMO.PlugIns.PlugInConfiguration Clone(DataModel.Configuration.GameConfiguration gameConfiguration)
    {
        var clone = new PlugInConfiguration();
        clone.AssignValuesOf(this, gameConfiguration);
        return clone;
    }

    /// <inheritdoc />
    public virtual void AssignValuesOf(object other, HNMO.DataModel.Configuration.GameConfiguration gameConfiguration)
    {
        if (other is HNMO.PlugIns.PlugInConfiguration typedOther)
        {
            this.AssignValuesOf(typedOther, gameConfiguration);
        }
    }

    /// <inheritdoc />
    public virtual void AssignValuesOf(HNMO.PlugIns.PlugInConfiguration other, HNMO.DataModel.Configuration.GameConfiguration gameConfiguration)
    {
        this.TypeId = other.TypeId;
        this.IsActive = other.IsActive;
        this.CustomPlugInSource = other.CustomPlugInSource;
        this.ExternalAssemblyName = other.ExternalAssemblyName;
        this.CustomConfiguration = other.CustomConfiguration;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{base.ToString()} ({this.Id})";
    }
}