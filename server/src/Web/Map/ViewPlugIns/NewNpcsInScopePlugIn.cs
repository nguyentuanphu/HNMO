// <copyright file="NewNpcsInScopePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Map.ViewPlugIns;

using Microsoft.JSInterop;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.Views.World;
using HNMO.Web.Map.Map;

/// <summary>
/// Implementation of <see cref="INewNpcsInScopePlugIn"/> which uses the javascript map app.
/// </summary>
public class NewNpcsInScopePlugIn : JsViewPlugInBase, INewNpcsInScopePlugIn
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NewNpcsInScopePlugIn" /> class.
    /// </summary>
    /// <param name="jsRuntime">The js runtime.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="worldAccessor">The world accessor.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public NewNpcsInScopePlugIn(IJSRuntime jsRuntime, ILoggerFactory loggerFactory, string worldAccessor, CancellationToken cancellationToken)
        : base(jsRuntime, loggerFactory, $"{worldAccessor}.addOrUpdateNpc", cancellationToken)
    {
    }

    /// <inheritdoc />
    public async ValueTask NewNpcsInScopeAsync(IEnumerable<NonPlayerCharacter> newObjects, bool isSpawned = true)
    {
        try
        {
            foreach (var npc in newObjects)
            {
                if (this.CancellationToken.IsCancellationRequested)
                {
                    return;
                }

                await this.InvokeAsync(npc.CreateMapObject()).ConfigureAwait(false);
            }
        }
        catch (TaskCanceledException)
        {
            // don't need to handle that.
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, $"Error in {nameof(this.NewNpcsInScopeAsync)}");
        }
    }
}