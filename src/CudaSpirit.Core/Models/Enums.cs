namespace CudaSpirit.Core.Models;

/// <summary>Region of the account; affects market tax, boss schedule and prices.</summary>
public enum Region
{
    NA,
    EU,
    SA,
    MENA,
    Asia,
    Console
}

/// <summary>Broad gear slot classification used by the progression helper.</summary>
public enum GearSlot
{
    MainWeapon,
    Awakening,
    Sub,
    Helmet,
    Armor,
    Gloves,
    Shoes,
    Ring,
    Earring,
    Necklace,
    Belt,
    LightstoneCombo,
    Other
}

/// <summary>Enhancement grade. Grunil/accessory levels collapse onto the same scale for UI.</summary>
public enum EnhanceGrade
{
    Base = 0,
    Plus1, Plus2, Plus3, Plus4, Plus5,
    Plus6, Plus7, Plus8, Plus9, Plus10,
    Plus11, Plus12, Plus13, Plus14, Plus15,
    PRI, DUO, TRI, TET, PEN
}

/// <summary>Which family of enhancement table applies. Drives base rates in the simulator.</summary>
public enum EnhanceKind
{
    /// <summary>Kzarka/Blackstar/boss weapons and armor (PRI..PEN via reblath-style curve).</summary>
    Weapon,
    Armor,
    /// <summary>Accessories (rings, earrings, necklace, belt) - harsher curve, no cron on some.</summary>
    Accessory,
    /// <summary>Life-skill / tool tier.</summary>
    Tool
}

public enum AiTaskKind
{
    /// <summary>Light data normalization / parsing anomalies. Cheap/fast model.</summary>
    Background,
    /// <summary>Chat, combo optimization, market and enhancement reasoning. Strong model.</summary>
    Reasoning,
    /// <summary>Image analysis (gear/crystal screenshots). Vision model.</summary>
    Vision
}

public enum BossKind
{
    World,
    Field,
    Guild
}
