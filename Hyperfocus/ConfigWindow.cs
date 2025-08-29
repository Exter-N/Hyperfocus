using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Style;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace Hyperfocus;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin) : base("Hyperfocus Settings")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new(512, 288),
            MaximumSize = new(512, 288),
        };

        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        DrawBehavior();
        DrawLayout();

        ImGui.Dummy(new(16.0f, 16.0f));
        using (ImRaii.PushColor(ImGuiCol.Text, ColorHelpers.RgbaVector4ToUint(ImGuiColors.DalamudOrange)))
        {
            ImGui.TextWrapped(
                "Please note that this plugin will not work in PvP or in duties, so as not to give an unfair advantage to its users."u8);
        }
    }

    public void DrawBehavior()
    {
        if (!ImGui.CollapsingHeader("Behavior"u8, ImGuiTreeNodeFlags.DefaultOpen))
            return;

        var displayForTarget = configuration.DisplayForTarget;
        if (ImGui.Checkbox("Display for Target"u8, ref displayForTarget))
        {
            configuration.DisplayForTarget = displayForTarget;
            configuration.Save();
        }

        using (ImRaii.PushIndent())
        {
            var displayDespiteTargetCircle = configuration.DisplayDespiteTargetCircle;
            if (ImGui.Checkbox("Even if the Target Circle is in view"u8, ref displayDespiteTargetCircle))
            {
                configuration.DisplayDespiteTargetCircle = displayDespiteTargetCircle;
                configuration.Save();
            }
        }

        var displayForFocusTarget = configuration.DisplayForFocusTarget;
        if (ImGui.Checkbox("Display for Focus Target"u8, ref displayForFocusTarget))
        {
            configuration.DisplayForFocusTarget = displayForFocusTarget;
            configuration.Save();
        }
    }

    public void DrawLayout()
    {
        if (!ImGui.CollapsingHeader("Layout"u8, ImGuiTreeNodeFlags.DefaultOpen))
            return;

        var padding = configuration.Padding;
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.33f);
        if (ImGui.DragFloat("Padding from Window Edge"u8, ref padding, 0.25f, 0.0f, 64.0f, "%.0f"))
        {
            configuration.Padding = padding;
            configuration.Save();
        }

        var width = configuration.Width;
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.33f);
        if (ImGui.DragFloat("Cursor Width", ref width, 1.0f, 8.0f, 288.0f, "%.0f"))
        {
            configuration.Width = width;
            configuration.Save();
        }
    }
}
