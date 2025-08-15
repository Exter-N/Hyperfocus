using Dalamud.Configuration;
using System;

namespace Hyperfocus;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool DisplayForTarget { get; set; } = true;
    public bool DisplayForFocusTarget { get; set; } = true;
    
    public float Width { get; set; } = 72.0f;
    public float Padding { get; set; } = 8.0f;

    public void Save()
        => Plugin.PluginInterface.SavePluginConfig(this);
}
