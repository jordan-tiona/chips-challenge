namespace ChipsCore;

/// <summary>
/// Mapping from MS .DAT tile byte codes to engine tiles. Reference: the
/// CC1 DAT spec on the Chip Wiki / Tile World source. Codes 0x40–0x63 are
/// monsters and 0x6C–0x6F is Chip himself; both live in the map layers in
/// the DAT format but are actors, not terrain.
/// </summary>
public static class TileCodes
{
    public static Tile ToTile(byte code) => code switch
    {
        0x00 => Tile.Floor,
        0x01 => Tile.Wall,
        0x02 => Tile.Chip,
        0x03 => Tile.Water,
        0x04 => Tile.Fire,
        0x05 => Tile.Wall,                       // invisible wall (permanent)
        >= 0x06 and <= 0x09 => Tile.Wall,        // thin walls — full wall until M2
        0x0A => Tile.Block,
        0x0B => Tile.Dirt,
        0x0C => Tile.Ice,
        0x0D => Tile.ForceFloor,                 // force south
        >= 0x0E and <= 0x11 => Tile.Block,       // cloning blocks N/W/S/E
        >= 0x12 and <= 0x14 => Tile.ForceFloor,  // force north/east/west
        0x15 => Tile.Exit,
        0x16 => Tile.DoorBlue,
        0x17 => Tile.DoorRed,
        0x18 => Tile.DoorGreen,
        0x19 => Tile.DoorYellow,
        >= 0x1A and <= 0x1D => Tile.Ice,         // ice corners
        0x1E => Tile.FakeWall,                   // blue wall (fake)
        0x1F => Tile.Wall,                       // blue wall (real)
        0x21 => Tile.Thief,
        0x22 => Tile.Socket,
        0x23 => Tile.Button,                     // green
        0x24 => Tile.Button,                     // red
        0x25 => Tile.ToggleWall,                 // toggle wall (closed)
        0x26 => Tile.Floor,                      // toggle wall (open)
        0x27 => Tile.Button,                     // brown
        0x28 => Tile.Button,                     // blue
        0x29 => Tile.Teleport,
        0x2A => Tile.Bomb,
        0x2B => Tile.Trap,
        0x2C => Tile.Wall,                       // appearing wall
        0x2D => Tile.Gravel,
        0x2E => Tile.PopupWall,
        0x2F => Tile.Hint,
        0x30 => Tile.Wall,                       // thin wall SE
        0x31 => Tile.CloneMachine,
        0x32 => Tile.ForceFloor,                 // random force floor
        0x64 => Tile.KeyBlue,
        0x65 => Tile.KeyRed,
        0x66 => Tile.KeyGreen,
        0x67 => Tile.KeyYellow,
        0x68 => Tile.BootsWater,
        0x69 => Tile.BootsFire,
        0x6A => Tile.BootsIce,
        0x6B => Tile.BootsForce,
        _ => Tile.Floor,                         // actors, splashes, unused codes
    };

    public static bool IsChipStart(byte code) => code is >= 0x6C and <= 0x6F;

    public static bool IsMonster(byte code) => code is >= 0x40 and <= 0x63;

    /// <summary>True if the code is an actor that sits on top of terrain
    /// in the DAT's top layer (the terrain is in the bottom layer).</summary>
    public static bool IsActor(byte code) => IsChipStart(code) || IsMonster(code);
}
