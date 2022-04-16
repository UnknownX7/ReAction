using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game;
using Dalamud.Logging;
using Dalamud.Plugin;

namespace ReAction;

public class ReAction : IDalamudPlugin
{
    public string Name => "ReAction";
    public static ReAction Plugin { get; private set; }
    public static Configuration Config { get; private set; }

    public static Dictionary<uint, Lumina.Excel.GeneratedSheets.Action> actionSheet;
    public static Dictionary<uint, Lumina.Excel.GeneratedSheets.Action> mountActionsSheet;

    public ReAction(DalamudPluginInterface pluginInterface)
    {
        Plugin = this;
        DalamudApi.Initialize(this, pluginInterface);

        Config = (Configuration)DalamudApi.PluginInterface.GetPluginConfig() ?? new();
        Config.Initialize();

        try
        {
            Game.Initialize();

            actionSheet = DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.Action>()?.Where(i => i.ClassJobCategory.Row > 0 && i.ActionCategory.Row <= 4 && i.RowId is not 7).ToDictionary(i => i.RowId, i => i);
            mountActionsSheet = DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.Action>()?.Where(i => i.ActionCategory.Row == 12).ToDictionary(i => i.RowId, i => i);
            if (actionSheet == null || mountActionsSheet == null)
                throw new ApplicationException("Action sheet failed to load!");

            DalamudApi.Framework.Update += Update;
            DalamudApi.PluginInterface.UiBuilder.Draw += Draw;
            DalamudApi.PluginInterface.UiBuilder.OpenConfigUi += ToggleConfig;
        }
        catch (Exception e)
        {
            PluginLog.Error($"Failed loading ReAction\n{e}");
        }
    }

    public void ToggleConfig() => PluginUI.isVisible ^= true;

    [Command("/reaction")]
    [HelpMessage("Opens / closes the config.")]
    private void ToggleConfig(string command, string argument) => ToggleConfig();

    [Command("/macroqueue")]
    [Aliases("/mqueue")]
    [HelpMessage("[on|off] - Toggles (with no argument specified), enables or disables /ac queueing in the current macro.")]
    private void OnMacroQueue(string command, string argument)
    {
        if (!Game.IsMacroRunning)
        {
            PrintError("This command requires a macro to be running.");
            return;
        }

        switch (argument)
        {
            case "on":
            case "" when !Game.queueACCommandReplacer.IsEnabled:
                Game.queueACCommandReplacer.Enable();
                break;
            case "off":
            case "" when Game.queueACCommandReplacer.IsEnabled:
                Game.queueACCommandReplacer.Disable();
                break;
            default:
                PrintError("Invalid usage.");
                break;
        }
    }

    public static void PrintEcho(string message) => DalamudApi.ChatGui.Print($"[ReAction] {message}");
    public static void PrintError(string message) => DalamudApi.ChatGui.PrintError($"[ReAction] {message}");

    private void Update(Framework framework)
    {
        if (Game.queueACCommandReplacer.IsEnabled && !Game.IsMacroRunning)
            Game.queueACCommandReplacer.Disable();

        ActionStackManager.Update();
    }

    private void Draw() => PluginUI.Draw();

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;

        Config.Save();

        DalamudApi.Framework.Update -= Update;
        DalamudApi.PluginInterface.UiBuilder.Draw -= Draw;
        DalamudApi.PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfig;
        DalamudApi.Dispose();

        Game.Dispose();
        Memory.Dispose();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}