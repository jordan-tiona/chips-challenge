namespace ChipsCore;

/// <summary>
/// Mapping from MS .DAT tile byte codes to engine tiles. Reference: the
/// CC1 DAT spec on the Chip Wiki / Tile World source. Codes 0x40–0x63 are
/// monsters and 0x6C–0x6F is Chip himself; both live in the map layers in
/// the DAT format but are actors, not terrain. Blocks (0x0A, clone blocks
/// 0x0E–0x11) are also actors — GameState lifts them off the terrain at
/// load.
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
        0x05 => Tile.InvisibleWall,
        0x06 => Tile.ThinN,
        0x07 => Tile.ThinW,
        0x08 => Tile.ThinS,
        0x09 => Tile.ThinE,
        0x0B => Tile.Dirt,
        0x0C => Tile.Ice,
        0x0D => Tile.ForceS,
        0x12 => Tile.ForceN,
        0x13 => Tile.ForceE,
        0x14 => Tile.ForceW,
        0x15 => Tile.Exit,
        0x16 => Tile.DoorBlue,
        0x17 => Tile.DoorRed,
        0x18 => Tile.DoorGreen,
        0x19 => Tile.DoorYellow,
        // Ice corner codes per the CC1 spec; verify against TWS replays in M4.
        0x1A => Tile.IceSE,
        0x1B => Tile.IceSW,
        0x1C => Tile.IceNW,
        0x1D => Tile.IceNE,
        0x1E => Tile.FakeWall,   // blue wall (fake)
        0x1F => Tile.Wall,       // blue wall (real)
        0x21 => Tile.Thief,
        0x22 => Tile.Socket,
        0x23 => Tile.ButtonGreen,
        0x24 => Tile.ButtonRed,
        0x25 => Tile.ToggleClosed,
        0x26 => Tile.ToggleOpen,
        0x27 => Tile.ButtonBrown,
        0x28 => Tile.ButtonBlue,
        0x29 => Tile.Teleport,
        0x2A => Tile.Bomb,
        0x2B => Tile.Trap,
        0x2C => Tile.AppearingWall,
        0x2D => Tile.Gravel,
        0x2E => Tile.PopupWall,
        0x2F => Tile.Hint,
        0x30 => Tile.ThinSE,
        0x31 => Tile.CloneMachine,
        0x32 => Tile.ForceRandom,
        0x64 => Tile.KeyBlue,
        0x65 => Tile.KeyRed,
        0x66 => Tile.KeyGreen,
        0x67 => Tile.KeyYellow,
        0x68 => Tile.BootsWater,
        0x69 => Tile.BootsFire,
        0x6A => Tile.BootsIce,
        0x6B => Tile.BootsForce,
        _ => Tile.Floor,         // actors, splashes, unused codes
    };

    public static bool IsBlock(byte code) => code == 0x0A || code is >= 0x0E and <= 0x11;

    public static bool IsChipStart(byte code) => code is >= 0x6C and <= 0x6F;

    public static bool IsMonster(byte code) => code is >= 0x40 and <= 0x63;

    /// <summary>True if the code is an actor that sits on top of terrain
    /// in the DAT's top layer (the terrain is in the bottom layer).</summary>
    public static bool IsActor(byte code) => IsChipStart(code) || IsMonster(code) || IsBlock(code);
}
