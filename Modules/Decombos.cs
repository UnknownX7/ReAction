using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace ReAction.Modules;

public unsafe class Decombos : PluginModule
{
    private enum ActionID : uint
    {
        Meditation = 3546,
        The_Forbidden_Chakra = 3547,
        Steel_Peak = 25761,

        Bunshin = 16493,
        Phantom_Kamaitachi = 25774,

        The_Wanderers_Minuet = 3559,

        Liturgy_of_the_Bell = 25862,

        Earthly_Star = 7439,

        Minor_Arcana = 7443,
        Lord_of_Crowns = 7444,
        Lady_of_Crowns = 7445,

        Geirskogul = 3555
    }

    /*public override bool ShouldEnable => ReAction.Config.EnableDecomboMeditation
        || ReAction.Config.EnableDecomboBunshin
        || ReAction.Config.EnableDecomboWanderersMinuet
        || ReAction.Config.EnableDecomboLiturgy
        || ReAction.Config.EnableDecomboEarthlyStar
        || ReAction.Config.EnableDecomboMinorArcana
        || ReAction.Config.EnableDecomboGeirskogul;*/

    public override bool ShouldEnable => false;

    protected override void Enable() => GetAdjustedActionIdHook.Enable();
    protected override void Disable() => GetAdjustedActionIdHook.Disable();

    private delegate ActionID GetAdjustedActionIdDelegate(ActionManager* actionManager, ActionID actionID);
    [HypostasisClientStructsInjection(typeof(ActionManager.MemberFunctionPointers), Required = true)]
    private static Hook<GetAdjustedActionIdDelegate> GetAdjustedActionIdHook;
    private static ActionID GetAdjustedActionIdDetour(ActionManager* actionManager, ActionID actionID)
    {
        var ret = GetAdjustedActionIdHook.Original(actionManager, actionID);

        switch (actionID)
        {
            case ActionID.Meditation when ReAction.Config.EnableDecomboMeditation:
                return actionID;
            case ActionID.The_Forbidden_Chakra when ReAction.Config.EnableDecomboMeditation:
            case ActionID.Steel_Peak when ReAction.Config.EnableDecomboMeditation:
                return ret != ActionID.Meditation ? ret : actionID;

            case ActionID.Bunshin when ReAction.Config.EnableDecomboBunshin:
            case ActionID.Phantom_Kamaitachi when ReAction.Config.EnableDecomboBunshin:
                return actionID;

            case ActionID.The_Wanderers_Minuet when ReAction.Config.EnableDecomboWanderersMinuet:
                return actionID;

            case ActionID.Liturgy_of_the_Bell when ReAction.Config.EnableDecomboLiturgy:
                return actionID;

            case ActionID.Earthly_Star when ReAction.Config.EnableDecomboEarthlyStar:
                return actionID;

            case ActionID.Minor_Arcana when ReAction.Config.EnableDecomboMinorArcana:
                return actionID;
            case ActionID.Lord_of_Crowns when ReAction.Config.EnableDecomboMinorArcana:
            case ActionID.Lady_of_Crowns when ReAction.Config.EnableDecomboMinorArcana:
                var minorArcanaAdjustment = GetAdjustedActionIdHook.Original(actionManager, ActionID.Minor_Arcana);
                return minorArcanaAdjustment != ActionID.Minor_Arcana ? minorArcanaAdjustment : actionID;

            case ActionID.Geirskogul when ReAction.Config.EnableDecomboGeirskogul:
                return actionID;

            default:
                return ret;
        }
    }
}