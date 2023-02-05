using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game;
using Dalamud.Plugin;

namespace ReAction;

public class ReAction : DalamudPlugin<ReAction, Configuration>, IDalamudPlugin
{
    public override string Name => "ReAction";

    public static Dictionary<uint, Lumina.Excel.GeneratedSheets.Action> actionSheet;
    public static Dictionary<uint, Lumina.Excel.GeneratedSheets.Action> mountActionsSheet;

    public ReAction(DalamudPluginInterface pluginInterface) : base(pluginInterface) { }

    protected override void Initialize()
    {
        Game.Initialize();

        actionSheet = DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.Action>()?.Where(i => i.ClassJobCategory.Row > 0 && i.ActionCategory.Row <= 4 && i.RowId is not 7).ToDictionary(i => i.RowId, i => i);
        mountActionsSheet = DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.Action>()?.Where(i => i.ActionCategory.Row == 12).ToDictionary(i => i.RowId, i => i);
        if (actionSheet == null || mountActionsSheet == null)
            throw new ApplicationException("Action sheet failed to load!");
    }

    protected override void ToggleConfig() => PluginUI.IsVisible ^= true;

    [PluginCommand("/reaction", HelpMessage = "Opens / closes the config.")]
    private void ToggleConfig(string command, string argument) => ToggleConfig();

    [PluginCommand("/macroqueue", "/mqueue", HelpMessage = "[on|off] - Toggles (with no argument specified), enables or disables /ac queueing in the current macro.")]
    private void OnMacroQueue(string command, string argument)
    {
        if (!Common.IsMacroRunning)
        {
            PrintError("This command requires a macro to be running.");
            return;
        }

        switch (argument)
        {
            case "on":
                Game.queueACCommandEdit.Enable();
                break;
            case "off":
                Game.queueACCommandEdit.Disable();
                break;
            case "":
                Game.queueACCommandEdit.Toggle();
                break;
            default:
                PrintError("Invalid usage.");
                break;
        }
    }

    protected override void Update(Framework framework)
    {
        if (Game.queueACCommandEdit.IsEnabled && !Common.IsMacroRunning)
            Game.queueACCommandEdit.Disable();
    }

    protected override void Draw() => PluginUI.Draw();

    protected override void Dispose(bool disposing)
    {
        if (!disposing) return;
        Game.Dispose();
    }
}