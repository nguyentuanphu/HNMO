// <copyright file="CharacterClassInitialization.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Version075;

using HNMO.DataModel.Configuration;
using HNMO.Interfaces;
using HNMO.Persistence.Initialization.CharacterClasses;
using HNMO.Persistence.Initialization.Properties;

/// <summary>
/// Initialization of character classes data for Version 0.75.
/// </summary>
internal class CharacterClassInitialization : Initialization.CharacterClasses.CharacterClassInitialization
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CharacterClassInitialization"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    public CharacterClassInitialization(IContext context, GameConfiguration gameConfiguration)
        : base(context, gameConfiguration)
    {
    }

    /// <inheritdoc />
    protected override bool UseClassicPvp => true;

    /// <inheritdoc />
    public override void Initialize()
    {
        this.CreateDarkKnight(CharacterClassNumber.DarkKnight, LocalizedString.FromResource(() => CharacterClassNames.DarkKnight), false, null, true);
        this.CreateDarkWizard(CharacterClassNumber.DarkWizard, LocalizedString.FromResource(() => CharacterClassNames.DarkWizard), false, null, true);
        this.CreateFairyElf(CharacterClassNumber.FairyElf, LocalizedString.FromResource(() => CharacterClassNames.FairyElf), false, null, true);
    }
}