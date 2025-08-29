using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Interface;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using CSVector3 = FFXIVClientStructs.FFXIV.Common.Math.Vector3;

namespace Hyperfocus;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ITextureSubstitutionProvider TextureSubstitutionProvider { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;

    // TODO ClientStructs-ify (#1539)
    [Signature("E8 ?? ?? ?? ?? 48 89 43 FB")]
    private GetTargetColorsDelegate getTargetColors = null!;

    [Signature("E8 ?? ?? ?? ?? 48 85 FF 0F 84 ?? ?? ?? ?? F3 0F 10 97")]
    private GetNameplateWorldPositionDelegate getNameplateWorldPosition = null!;

    private unsafe delegate ulong GetTargetColorsDelegate(GameObject* gameObject);
    private unsafe delegate float* GetNameplateWorldPositionDelegate(GameObject* gameObject, float* vector);

    private const string CommandName = "/phfocus";

    private readonly WindowSystem windowSystem = new("Hyperfocus");
    private readonly CursorProvider cursorProvider;

    public Configuration Configuration { get; init; }
    private ConfigWindow ConfigWindow { get; init; }

    public Plugin()
    {
        GameInteropProvider.InitializeFromAttributes(this);

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);
        windowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open settings",
        });

        cursorProvider = new CursorProvider(TextureProvider, PluginInterface.UiBuilder, TextureSubstitutionProvider,
                                            DataManager);

        PluginInterface.UiBuilder.Draw += DrawUi;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
    }

    public void Dispose()
    {
        windowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
        => ToggleConfigUi();

    private void DrawUi()
    {
        DrawCursors();
        windowSystem.Draw();
    }

    public void ToggleConfigUi()
        => ConfigWindow.Toggle();

    private void DrawCursors()
    {
        if (ClientState.IsPvP ||
            Condition.Any(ConditionFlag.BoundByDuty, ConditionFlag.BoundByDuty56, ConditionFlag.BoundByDuty95) &&
            Condition.Any(ConditionFlag.InCombat)) return;

        var target = TargetManager.Target;
        var focusTarget = TargetManager.FocusTarget;
        if (Configuration.DisplayForTarget && target is not null)
            DrawCursor(target, false, true);

        if (Configuration.DisplayForFocusTarget && focusTarget is not null)
            DrawCursor(focusTarget, true, focusTarget.Address == (target?.Address ?? 0));
    }

    private unsafe bool TryGetDirection(IGameObject obj, bool isSameAsTarget, out Vector2 direction)
    {
        var gameObject = (GameObject*)obj.Address;
        var position = stackalloc float[3];
        getNameplateWorldPosition(gameObject, position);
        var positionVec = new CSVector3(position[0], position[1], position[2]);
        var inView = GameGui.WorldToScreen(positionVec, out _);

        if (inView || isSameAsTarget && obj is ICharacter && GameGui.WorldToScreen(obj.Position, out _))
        {
            direction = Vector2.Zero;
            return false;
        }

        ref var camera = ref Control.Instance()->CameraManager.GetActiveCamera()->SceneCamera;
        var lookAtDirection = (camera.LookAtVector - camera.Position).Normalized;
        var plateDirection = (positionVec - camera.Position).Normalized;
        var viewDirection = CSVector3.Transform(positionVec, camera.ViewMatrix).Normalized;
        var screenDirection = Vector2.Normalize(new Vector2(viewDirection.X, -viewDirection.Y));
        var yaw = WrapAngle(MathF.Atan2(plateDirection.X, -plateDirection.Z) -
                            MathF.Atan2(lookAtDirection.X, -lookAtDirection.Z));
        var roll = MathF.Asin(plateDirection.Y) - MathF.Asin(lookAtDirection.Y);
        var angularDirection = Vector2.Normalize(new Vector2(yaw, -roll));
        var mix = Smoothstep(Saturate(viewDirection.Z * 0.5f + 0.5f));

        direction = Vector2.Lerp(screenDirection, angularDirection, mix);
        return true;
    }

    private unsafe void DrawCursor(IGameObject target, bool isFocus, bool isSameAsTarget)
    {
        if (!TryGetDirection(target, isSameAsTarget, out var screenPosCenterRelative)) return;

        var gameObject = (GameObject*)target.Address;
        var targetColors = getTargetColors(gameObject);

        var halfViewport = ImGui.GetMainViewport().Size * 0.5f;
        var center = ImGui.GetMainViewport().Pos + halfViewport;
        halfViewport -= new Vector2(Configuration.Padding);
        var ratio = MathF.Max(MathF.Abs(screenPosCenterRelative.X) / halfViewport.X,
                              MathF.Abs(screenPosCenterRelative.Y) / halfViewport.Y);
        var edgePosCenterRelative = screenPosCenterRelative / ratio;
        var edgePos = edgePosCenterRelative + center;
        var yAxis = Vector2.Normalize(edgePosCenterRelative) * Configuration.Width;
        var xAxis = new Vector2(yAxis.Y, -yAxis.X);

        var backgroundDrawList = ImGui.GetBackgroundDrawList();

        var layer = cursorProvider.GetTintedCursorPart(
            isFocus ? CursorProvider.FocusEdgePart : CursorProvider.TargetEdgePart, unchecked((uint)targetColors));
        var bottomLeft = edgePos - 0.5f * xAxis;
        var bottomRight = edgePos + 0.5f * xAxis;
        var topLeft = bottomLeft - ((float)layer.Height / layer.Width) * yAxis;
        var topRight = bottomRight - ((float)layer.Height / layer.Width) * yAxis;
        backgroundDrawList.AddImageQuad(layer.Handle, topLeft, topRight, bottomRight, bottomLeft);

        layer = cursorProvider.GetTintedCursorPart(
            isFocus ? CursorProvider.FocusFillPart : CursorProvider.TargetFillPart,
            unchecked((uint)(targetColors >> 32)));
        topLeft = bottomLeft - ((float)layer.Height / layer.Width) * yAxis;
        topRight = bottomRight - ((float)layer.Height / layer.Width) * yAxis;
        backgroundDrawList.AddImageQuad(layer.Handle, topLeft, topRight, bottomRight, bottomLeft);
    }

    private static float Saturate(float vector)
        => float.Clamp(vector, 0.0f, 1.0f);

    private static float WrapAngle(float angle)
        => angle > MathF.PI ? angle - MathF.Tau : angle <= -MathF.PI ? angle + MathF.Tau : angle;

    private static float Smoothstep(float value)
        => value * value * (3.0f - 2.0f * value);
}
