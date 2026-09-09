using Godot;
using System.Collections.Generic;

public enum PlayerPerk
{
    None = 0,
    BargainHunter = 1,
    PowerArm = 2,
    Tank = 3,
    Scavenger = 4,
    SpeedDemon = 5,
    StickyFingers = 6
}

public class PerkDefinition
{
    public PlayerPerk Perk { get; set; }
    public string Name { get; set; }
    public string Icon { get; set; }
    public string Tagline { get; set; }
    public string Description { get; set; }
    public Color ThemeColor { get; set; }
}

public static class PerkDatabase
{
    public static readonly Dictionary<PlayerPerk, PerkDefinition> Perks = new()
    {
        [PlayerPerk.BargainHunter] = new PerkDefinition
        {
            Perk = PlayerPerk.BargainHunter,
            Name = "Bargain Hunter",
            Icon = "💰",
            Tagline = "25% Off Everything",
            Description = "All store products cost 25% less cash. Fill your cart with premium hardware, explosives, and groceries on a budget!",
            ThemeColor = new Color(0.25f, 0.9f, 0.45f)
        },
        [PlayerPerk.PowerArm] = new PerkDefinition
        {
            Perk = PlayerPerk.PowerArm,
            Name = "Power Arm",
            Icon = "💪",
            Tagline = "+30% Velocity & +25% Damage",
            Description = "Hurl products with tremendous velocity and devastating kinetic force. Thrown items travel faster and deal +25% bonus damage!",
            ThemeColor = new Color(1.0f, 0.45f, 0.15f)
        },
        [PlayerPerk.Tank] = new PerkDefinition
        {
            Perk = PlayerPerk.Tank,
            Name = "Tank",
            Icon = "🛡️",
            Tagline = "190 Max HP (+40 Health)",
            Description = "Heavily fortified shopper! Start every round with 190 HP instead of 150 HP, shrugging off hits and surviving lethal skirmishes.",
            ThemeColor = new Color(0.35f, 0.7f, 1.0f)
        },
        [PlayerPerk.Scavenger] = new PerkDefinition
        {
            Perk = PlayerPerk.Scavenger,
            Name = "Scavenger",
            Icon = "🎒",
            Tagline = "Raid Locked Shelves in Battle",
            Description = "Never run out of ammo! While supermarket shelves lock down for other players, you can freely scavenge locked stock during Battle Royale.",
            ThemeColor = new Color(1.0f, 0.82f, 0.2f)
        },
        [PlayerPerk.SpeedDemon] = new PerkDefinition
        {
            Perk = PlayerPerk.SpeedDemon,
            Name = "Speed Demon",
            Icon = "⚡",
            Tagline = "+20% Movement Speed",
            Description = "Sprint and maneuver 20% faster! Outrun the Groomba hazard, evade the contracting safe zone, and flank opponents in the aisles.",
            ThemeColor = new Color(0.95f, 0.35f, 0.95f)
        },
        [PlayerPerk.StickyFingers] = new PerkDefinition
        {
            Perk = PlayerPerk.StickyFingers,
            Name = "Sticky Fingers",
            Icon = "🧤",
            Tagline = "6 Inventory Slots (Extra Pocket)",
            Description = "Unlocks an extra 6th inventory slot! Carry more weapons, tools, and healing consumables into battle than any other shopper.",
            ThemeColor = new Color(0.25f, 0.95f, 0.9f)
        }
    };
}
