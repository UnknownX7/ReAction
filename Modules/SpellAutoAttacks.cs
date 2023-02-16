using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;

namespace ReAction.Modules;

public class SpellAutoAttacks : PluginModule
{
    public override bool ShouldEnable => ReAction.Config.EnableSpellAutoAttacks && !ReAction.Config.EnableSpellAutoAttacksOutOfCombat;

    protected override bool Validate() => Game.spellAutoAttackPatch.IsValid;
    protected override void Enable() => DalamudApi.Framework.Update += Update;
    protected override void Disable() => DalamudApi.Framework.Update -= Update;

    private static void Update(Framework framework)
    {
        if (ReAction.Config.EnableSpellAutoAttacks)
        {
            if (Game.spellAutoAttackPatch.IsEnabled != DalamudApi.Condition[ConditionFlag.InCombat])
                Game.spellAutoAttackPatch.Toggle();
        }
        else
        {
            Game.spellAutoAttackPatch.Disable();
        }
    }
}