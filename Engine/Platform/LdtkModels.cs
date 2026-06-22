using System;
using System.Text.Json.Serialization;

namespace MyGame.Engine.Platform;

public class LdtkRoot
{
    [JsonPropertyName("levels")]
    public LdtkLevel[] Levels { get; set; } = null!;
}

public class LdtkLevel
{
    [JsonPropertyName("identifier")]
    public string Identifier { get; set; } = string.Empty;

    [JsonPropertyName("pxWid")]
    public int PxWidth { get; set; }

    [JsonPropertyName("pxHei")]
    public int PxHeight { get; set; }

    [JsonPropertyName("externalRelPath")]
    public string? ExternalRelPath { get; set; }

    [JsonPropertyName("layerInstances")]
    public LdtkLayerInstance[] LayerInstances { get; set; } = Array.Empty<LdtkLayerInstance>();

    [JsonPropertyName("fieldInstances")]
    public LdtkFieldInstance[] FieldInstances { get; set; } = Array.Empty<LdtkFieldInstance>();
}

public class LdtkFieldInstance
{
    [JsonPropertyName("__identifier")]
    public string Identifier { get; set; } = string.Empty;

    [JsonPropertyName("__value")]
    public object? Value { get; set; }
}

public class LdtkLayerInstance
{
    [JsonPropertyName("__identifier")]
    public string Identifier { get; set; } = string.Empty;

    [JsonPropertyName("__type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("__gridSize")]
    public int GridSize { get; set; }

    [JsonPropertyName("__cWid")]
    public int CellsWidth { get; set; }

    [JsonPropertyName("__tilesetRelPath")]
    public string? TilesetRelPath { get; set; }

    [JsonPropertyName("intGridCsv")]
    public int[] IntGridCsv { get; set; } = Array.Empty<int>();

    [JsonPropertyName("entityInstances")]
    public LdtkEntityInstance[] EntityInstances { get; set; } = Array.Empty<LdtkEntityInstance>();

    [JsonPropertyName("autoLayerTiles")]
    public LdtkTileInstance[] AutoLayerTiles { get; set; } = Array.Empty<LdtkTileInstance>();

    [JsonPropertyName("gridTiles")]
    public LdtkTileInstance[] GridTiles { get; set; } = Array.Empty<LdtkTileInstance>();
}

public class LdtkTileInstance
{
    [JsonPropertyName("px")]
    public int[] Px { get; set; } = null!;

    [JsonPropertyName("src")]
    public int[] Src { get; set; } = null!;

    [JsonPropertyName("f")]
    public int F { get; set; }
}

public class LdtkEntityInstance
{
    [JsonPropertyName("__identifier")]
    public string Identifier { get; set; } = string.Empty;

    [JsonPropertyName("px")]
    public int[] Px { get; set; } = null!;

    // ARCHITECTURE FIX: Required for GameplayState to parse TargetDimensions
    [JsonPropertyName("fieldInstances")]
    public LdtkFieldInstance[] FieldInstances { get; set; } = Array.Empty<LdtkFieldInstance>();
}
