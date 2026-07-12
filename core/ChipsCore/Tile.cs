namespace ChipsCore;

/// <summary>
/// Static level tiles (the "terrain" layer). Monsters and movable blocks
/// will live on a separate actor list from M3 on, matching how the MS
/// engine (and the .DAT format's two layers) model the world. For M1 a
/// block is just an immovable Tile.
/// </summary>
public enum Tile
{
    Floor,
    Wall,
    Chip,
    Water,
    Fire,
    Dirt,
    Exit,
    Socket,
    Hint,

    KeyRed,
    KeyBlue,
    KeyYellow,
    KeyGreen,
    DoorRed,
    DoorBlue,
    DoorYellow,
    DoorGreen,

    BootsWater,
    BootsFire,
    BootsIce,
    BootsForce,
    Thief,

    // M1: rendered distinctly but with placeholder behavior (walkable
    // unless noted). Real mechanics arrive in M2/M3.
    Block,        // blocks movement
    Ice,
    ForceFloor,
    Gravel,
    Teleport,
    Bomb,
    Trap,
    Button,
    ToggleWall,   // blocks movement
    FakeWall,     // fake blue wall: vanishes when bumped
    CloneMachine, // blocks movement
    PopupWall,
}
