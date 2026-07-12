namespace ChipsCore;

/// <summary>
/// Static level terrain. Movable blocks are entities tracked separately by
/// GameState (matching the DAT format's two layers); monsters join them in
/// M3. Trap/clone/tank wiring (brown, red, blue buttons) is deferred to M3
/// alongside monsters — those buttons are walkable no-ops for now.
/// </summary>
public enum Tile
{
    Floor,
    Wall,
    InvisibleWall,   // permanently invisible; drawn as floor
    AppearingWall,   // drawn as floor until bumped, then becomes Wall
    FakeWall,        // fake blue wall: vanishes when bumped
    Chip,
    Water,
    Fire,
    Dirt,            // turns to floor when stepped on
    Gravel,
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

    BootsWater,      // flippers
    BootsFire,
    BootsIce,        // skates
    BootsForce,      // suction boots
    Thief,

    Ice,
    IceNW,           // ice corner, walls on N and W (curve connects S and E)
    IceNE,
    IceSW,
    IceSE,

    ForceN,
    ForceS,
    ForceE,
    ForceW,
    ForceRandom,

    Teleport,
    Bomb,
    Trap,            // walkable no-op until wiring lands in M3

    ButtonGreen,     // toggles ToggleOpen/ToggleClosed
    ButtonRed,       // clone machines (M3)
    ButtonBrown,     // traps (M3)
    ButtonBlue,      // tanks (M3)
    ToggleClosed,
    ToggleOpen,

    CloneMachine,
    PopupWall,       // walkable once; becomes Wall when stepped off

    ThinN,           // thin wall on the north edge; tile itself is floor
    ThinW,
    ThinS,
    ThinE,
    ThinSE,          // thin walls on south and east edges
}
