using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Interface;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace Hyperfocus;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;

    [Signature("E8 ?? ?? ?? ?? 48 89 43 FB")]
    private GetTargetColorsDelegate GetTargetColors = null!;

    [Signature("E8 ?? ?? ?? ?? 48 85 FF 0F 84 ?? ?? ?? ?? F3 0F 10 97")]
    private GetNameplateWorldPositionDelegate GetNameplateWorldPosition = null!;
    
    private unsafe delegate ulong GetTargetColorsDelegate(GameObject* gameObject);
    private unsafe delegate float* GetNameplateWorldPositionDelegate(GameObject* gameObject, float* vector);

    private const string CommandName = "/phfocus";

    private readonly WindowSystem WindowSystem = new("Hyperfocus");
    private readonly UldWrapper TargetCursorUld;
    
    public Configuration Configuration { get; init; }
    private ConfigWindow ConfigWindow { get; init; }

    public Plugin()
    {
        GameInteropProvider.InitializeFromAttributes(this);
        
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open settings",
        });
        
        TargetCursorUld = PluginInterface.UiBuilder.LoadUld("ui/uld/TargetCursor.uld");

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
        => ToggleConfigUI();

    private void DrawUI()
    {
        DrawCursors();
        WindowSystem.Draw();
    }

    public void ToggleConfigUI()
        => ConfigWindow.Toggle();

    private void DrawCursors()
    {
        if (ClientState.IsPvP) return;
        
        var target = TargetManager.Target;
        var focusTarget = TargetManager.FocusTarget;
        if (Configuration.DisplayForTarget && target is not null)
            DrawCursor(target, false, true);

        if (Configuration.DisplayForFocusTarget && focusTarget is not null)
            DrawCursor(focusTarget, true, focusTarget.Address == (target?.Address ?? 0));
    }
    
    private unsafe void DrawCursor(IGameObject target, bool isFocus, bool isSameAsTarget)
    {
        var gameObject = (GameObject*)target.Address;
        var fillColor = ColorHelpers.RgbaUintToVector4(unchecked((uint)(GetTargetColors(gameObject) >> 32)));
        var position = stackalloc float[3];
        GetNameplateWorldPosition(gameObject, position);
        var inView =
            GameGui.WorldToScreen(new Vector3(position[0], position[1], position[2]), out var screenPos);
        if (inView) return;
        if (isSameAsTarget && GameGui.WorldToScreen(target.Position, out _)) return;

        var halfViewport = ImGui.GetMainViewport().Size * 0.5f;
        var center = ImGui.GetMainViewport().Pos + halfViewport;
        halfViewport -= new Vector2(Configuration.Padding);
        var screenPosCenterRelative = screenPos - center;
        var ratio = MathF.Max(MathF.Abs(screenPosCenterRelative.X) / halfViewport.X,
                              MathF.Abs(screenPosCenterRelative.Y) / halfViewport.Y);
        var edgePosCenterRelative = screenPosCenterRelative / ratio;
        var edgePos = edgePosCenterRelative + center;
        var yAxis = Vector2.Normalize(edgePosCenterRelative) * Configuration.Width;
        var xAxis = new Vector2(yAxis.Y, -yAxis.X);
        
        var backgroundDrawList = ImGui.GetBackgroundDrawList();

        var layer1 = TargetCursorUld.LoadTexturePart("ui/uld/TargetCursor.tex", isFocus ? 4 : 2);
        if (layer1 is not null)
        {
            var bottomLeft = edgePos - 0.5f * xAxis;
            var bottomRight = edgePos + 0.5f * xAxis;
            var topLeft = bottomLeft - ((float)layer1.Height / layer1.Width) * yAxis;
            var topRight = bottomRight - ((float)layer1.Height / layer1.Width) * yAxis;
            backgroundDrawList.AddImageQuad(layer1.Handle, topLeft, topRight, bottomRight, bottomLeft);
        }
        
        var layer2 = TargetCursorUld.LoadTexturePart("ui/uld/TargetCursor.tex", isFocus ? 5 : 3);
        if (layer2 is not null)
        {
            var bottomLeft = edgePos - 0.5f * xAxis;
            var bottomRight = edgePos + 0.5f * xAxis;
            var topLeft = bottomLeft - ((float)layer2.Height / layer2.Width) * yAxis;
            var topRight = bottomRight - ((float)layer2.Height / layer2.Width) * yAxis;
            backgroundDrawList.AddImageQuad(layer2.Handle, topLeft, topRight, bottomRight, bottomLeft,
                                            ColorHelpers.RgbaVector4ToUint(Saturate(fillColor + new Vector4(0.3725f))));
        }
    }

    private static Vector4 Saturate(Vector4 vector)
        => Vector4.Clamp(vector, Vector4.Zero, Vector4.One);
}
