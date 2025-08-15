using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Hyperfocus;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;

    public ConfigWindow(Plugin plugin) : base("Hyperfocus Settings")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new(512, 288),
            MaximumSize = new(512, 288),
        };

        Configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        DrawBehavior();
        DrawLayout();
    }

    public void DrawBehavior()
    {
        if (!ImGui.CollapsingHeader("Behavior"u8, ImGuiTreeNodeFlags.DefaultOpen))
            return;
        
        var displayForTarget = Configuration.DisplayForTarget;
        if (ImGui.Checkbox("Display for Target"u8, ref displayForTarget))
        {
            Configuration.DisplayForTarget = displayForTarget;
            Configuration.Save();
        }
        
        var displayForFocusTarget = Configuration.DisplayForFocusTarget;
        if (ImGui.Checkbox("Display for Focus Target"u8, ref displayForFocusTarget))
        {
            Configuration.DisplayForFocusTarget = displayForFocusTarget;
            Configuration.Save();
        }
    }

    public void DrawLayout()
    {
        if (!ImGui.CollapsingHeader("Layout"u8, ImGuiTreeNodeFlags.DefaultOpen))
            return;

        var padding = Configuration.Padding;
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.33f);
        if (ImGui.DragFloat("Padding from Window Edge"u8, ref padding, 0.25f, 0.0f, 64.0f, "%.0f"))
        {
            Configuration.Padding = padding;
            Configuration.Save();
        }

        var width = Configuration.Width;
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.33f);
        if (ImGui.DragFloat("Cursor Width", ref width, 1.0f, 8.0f, 288.0f, "%.0f"))
        {
            Configuration.Width = width;
            Configuration.Save();
        }
    }
}
