# Generates data/bdo-grind-catalog.json from the GrumpyG monster-zone table (Aug 2024 update,
# current zone roster as of 2026). Silver figures are millions/hour with blue loot scroll,
# pre-tax, Agris+loot scroll dependent. Run from repo root: python tools/gen_catalog.py
import json, math, re, os

# name, territory, level, recAp, dp, silver(M/hr), tags, risk, x, y
# Coordinates are approximate world-map positions (somethinglovely-style 0..12000 x 0..8000)
# good enough for the planner's coordinate fallback and territory grouping.
ZONES = [
    # --- Elvia Calpheon ---
    ("Quint Hill: Troll Habitat", "Elvia Calpheon", 61, 310, 400, 1021, "dehkia-lantern agris solo party", 0.65, 2900, 1500),
    ("Hexe Sanctuary", "Elvia Calpheon", 61, 300, 390, 905, "agris solo party marni", 0.60, 2600, 1100),
    ("Primal Giant Post", "Elvia Calpheon", 61, 280, 380, 775, "agris solo party marni", 0.55, 2750, 1350),
    ("Rhutum Outstation", "Elvia Calpheon", 61, 260, 360, 586, "agris solo party marni", 0.50, 2450, 1600),
    ("Saunil Camp", "Elvia Calpheon", 61, 250, 330, 546, "agris solo party marni", 0.45, 2350, 1750),
    # --- Elvia Serendia ---
    ("Orc Camp (Elvia)", "Elvia Serendia", 61, 260, 340, 693, "agris solo party marni", 0.50, 2100, 1900),
    ("Swamp Fogan Habitat (Elvia)", "Elvia Serendia", 61, 240, 330, 598, "agris solo party marni", 0.45, 1900, 2100),
    ("Glish Swamp Naga Habitat (Elvia)", "Elvia Serendia", 61, 240, 330, 543, "agris solo party marni", 0.45, 1800, 2250),
    ("Bloody Monastery (Elvia)", "Elvia Serendia", 61, 260, 340, 522, "agris solo party marni", 0.45, 2050, 2050),
    ("Biraghi Den (Elvia)", "Elvia Serendia", 61, 220, 310, 513, "agris solo party marni", 0.40, 1950, 1950),
    ("Altar Imp Habitat (Elvia)", "Elvia Serendia", 61, 220, 310, 545, "agris party-2 marni", 0.40, 2000, 2200),
    ("Castle Ruins (Elvia)", "Elvia Serendia", 61, 230, 320, 452, "agris party-3 marni", 0.40, 2150, 2150),
    # --- Mediah ---
    ("Abandoned Iron Mine", "Mediah", 48, 70, 150, 28, "beginner asula", 0.10, 2500, 2600),
    ("Helms Post", "Mediah", 48, 90, 180, 27, "beginner asula", 0.10, 2650, 2500),
    ("Elric Shrine", "Mediah", 50, 95, 180, 22, "beginner asula", 0.10, 2700, 2700),
    ("Manes Hideout", "Mediah", 48, 80, 160, 18, "beginner asula", 0.10, 2600, 2450),
    ("Wandering Rogue Den", "Mediah", 48, 80, 160, 33, "beginner asula", 0.10, 2550, 2700),
    ("Soldiers Cemetery", "Mediah", 52, 100, 180, 115, "solo", 0.20, 2750, 2400),
    ("Sausans (Shultz Guard)", "Mediah", 52, 100, 180, 32, "beginner grunil", 0.20, 2800, 2550),
    ("Sausans 3P (Shultz Guard)", "Mediah", 57, 210, 320, 372, "agris party-3 combat-exp", 0.35, 2800, 2550),
    ("Hasrah Ruins Cliff", "Mediah", 52, 110, 180, 20, "solo treasure", 0.15, 3000, 2900),
    ("Kratuga Ancient Ruins", "Mediah", 60, 250, 300, 520, "agris solo party marni", 0.45, 3050, 2950),
    # --- Calpheon (base) ---
    ("Catfishman Camp", "Calpheon", 43, 50, 120, 20, "beginner", 0.10, 2200, 1500),
    ("Traitor's Graveyard (Marie Cave)", "Calpheon", 57, 140, 300, 310, "agris combat-exp caphras marni", 0.30, 2300, 1400),
    ("Abandoned Monastery 2P", "Calpheon", 61, 260, 350, 544, "party-2", 0.45, 2400, 1250),
    ("Star's End", "Calpheon", 61, 240, 320, 511, "solo marni", 0.45, 1500, 800),
    # --- Kamasylvia ---
    ("Fadus Habitat (Loopy Tree)", "Kamasylvia", 55, 120, 180, 382, "combat-exp skill-exp marni", 0.20, 4200, 3300),
    ("Polly's Forest (Mushrooms)", "Kamasylvia", 55, 140, 250, 409, "combat-exp skill-exp marni", 0.25, 4350, 3400),
    ("Manshaum Forest", "Kamasylvia", 54, 190, 260, 443, "caphras marni", 0.35, 4500, 3600),
    ("Mirumok Ruins 2-3P", "Kamasylvia", 59, 190, 270, 400, "party-2 party-3 combat-exp", 0.40, 4600, 3200),
    ("Tooth Fairy Forest (Ronaros)", "Kamasylvia", 59, 190, 270, 428, "solo marni", 0.40, 4450, 3150),
    ("Navarn Steppe", "Kamasylvia", 56, 190, 260, 110, "solo horses", 0.30, 4550, 3450),
    ("Gyfin Rhasia Temple Upper 5P", "Kamasylvia", 57, 250, 320, 525, "party-5 agris combat-exp", 0.50, 4700, 3300),
    ("Gyfin Rhasia Temple Underground", "Kamasylvia", 62, 290, 380, 857, "party caphras marni", 0.55, 4700, 3350),
    ("Ash Forest", "Kamasylvia", 62, 300, 390, 741, "solo party marni", 0.55, 4800, 3100),
    ("Ash Forest Dehkia", "Kamasylvia", 62, 310, 400, 992, "dehkia-lantern solo party", 0.70, 4800, 3100),
    ("Thornwood Forest", "Odyllita", 57, 230, 320, 413, "caphras marni", 0.45, 5000, 3600),
    ("Thornwood Forest Dehkia", "Odyllita", 62, 310, 400, 1104, "dehkia-lantern solo party", 0.70, 5000, 3600),
    ("Tunkuta Turos 2P", "Odyllita", 60, 250, 360, 559, "party-2 agris marni", 0.55, 5100, 3700),
    ("Tunkuta Turos 2P Dehkia", "Odyllita", 62, 310, 400, 1134, "party-2 dehkia-lantern", 0.70, 5100, 3700),
    ("Olun's Valley 3P", "Odyllita", 61, 290, 380, 834, "party-3 agris caphras", 0.60, 5200, 3500),
    ("Olun's Valley 3P Dehkia", "Odyllita", 62, 310, 400, 1007, "party-3 dehkia-lantern", 0.75, 5200, 3500),
    ("Crypt of Resting Thoughts", "Odyllita", 62, 300, 400, 888, "solo party marni", 0.60, 5150, 3400),
    # --- Dreighan ---
    ("Tshira Ruins", "Dreighan", 55, 140, 160, 424, "solo marni", 0.30, 3800, 2500),
    ("Sherekhan Necropolis Day", "Dreighan", 56, 190, 230, 488, "skill-exp marni", 0.35, 3900, 2600),
    ("Sherekhan Necropolis Night", "Dreighan", 56, 210, 230, 528, "skill-exp night marni", 0.40, 3900, 2600),
    ("Blood Wolf Settlement", "Dreighan", 57, 180, 210, 536, "solo marni", 0.35, 4000, 2400),
    ("Murrowak's Labyrinth", "Mountain of Eternal Winter", 61, 260, 350, 546, "combat-exp skill-exp marni", 0.45, 4300, 2000),
    ("Jade Starlight Forest", "Mountain of Eternal Winter", 61, 260, 350, 624, "agris marni", 0.45, 4400, 1900),
    ("Winter Tree Fossil", "Mountain of Eternal Winter", 62, 260, 350, 405, "energy-cost", 0.40, 4500, 1800),
    # --- Valencia ---
    ("Bashim Base", "Valencia", 55, 100, 180, 321, "combat-exp skill-exp marni", 0.20, 3400, 3100),
    ("Titium Valley (Desert Fogans)", "Valencia", 55, 100, 180, 397, "combat-exp marni", 0.25, 3500, 3200),
    ("Desert Naga Temple", "Valencia", 55, 100, 180, 395, "combat-exp marni", 0.25, 3600, 3150),
    ("Waragon Nest 3P", "Valencia", 55, 150, 220, 135, "party-3", 0.25, 3550, 3300),
    ("Gahaz Bandits Lair", "Valencia", 55, 140, 180, 394, "agris marni", 0.30, 3700, 3050),
    ("Cadry Ruins", "Valencia", 55, 140, 160, 408, "agris marni", 0.30, 3800, 2950),
    ("Crescent Shrine", "Valencia", 55, 140, 160, 396, "agris marni", 0.30, 3900, 2850),
    ("Centaurus Herd (Taphtar Plain)", "Valencia", 56, 180, 200, 466, "agris caphras marni", 0.35, 3300, 3000),
    ("Basilisk Den 3P", "Valencia", 56, 180, 220, 110, "party-3 scroll", 0.35, 3750, 3250),
    ("Pila Ku Jail", "Valencia", 57, 190, 240, 255, "solo", 0.35, 4000, 2700),
    ("Pila Ku Jail Dehkia", "Valencia", 62, 300, 400, 715, "dehkia-lantern solo party", 0.70, 4000, 2700),
    ("Roud Sulfur Mines", "Valencia", 57, 190, 240, 242, "combat-exp marni", 0.35, 4100, 2600),
    ("Roud Sulfur Mines Dehkia", "Valencia", 62, 300, 400, 806, "dehkia-lantern", 0.70, 4100, 2600),
    ("Aakman Temple", "Valencia", 60, 230, 300, 459, "portal agris", 0.45, 4300, 2500),
    ("Aakman Temple Dehkia", "Valencia", 62, 300, 400, 801, "portal dehkia-lantern", 0.70, 4300, 2500),
    ("Hystria Ruins", "Valencia", 61, 230, 300, 460, "portal agris", 0.45, 4350, 2450),
    ("Hystria Ruins Dehkia", "Valencia", 62, 300, 400, 832, "portal dehkia-lantern", 0.70, 4350, 2450),
    ("Crescent Shrine Dehkia", "Valencia", 62, 300, 400, 954, "dehkia-lantern", 0.70, 3900, 2850),
    # --- Ulukita ---
    ("City of the Dead", "Ulukita", 62, 310, 380, 776, "dark-hunger solo party", 0.60, 5400, 2800),
    ("Tungrad Ruins", "Ulukita", 63, 320, 410, 1137, "dark-hunger solo party", 0.70, 5500, 2700),
    ("Darkseeker's Retreat", "Ulukita", 63, 310, 420, 693, "agris red-artifacts", 0.60, 5450, 2900),
    ("Yzrahid Highlands", "Ulukita", 63, 310, 420, 1000, "agris red-artifacts", 0.65, 5350, 2750),
    # --- Island/Water ---
    ("Protty Cave", "Island/Water", 57, 170, 200, 482, "combat-exp skill-exp marni", 0.30, 1600, 600),
    ("Sycraia Underwater Ruins Upper", "Island/Water", 58, 200, 260, 369, "combat-exp skill-exp", 0.35, 1550, 500),
    ("Sycraia Abyssal Lower", "Island/Water", 60, 240, 330, 667, "caphras marni", 0.50, 1550, 500),
    ("Kuit Pirates", "Island/Water", 52, 100, 180, 55, "solo trade-goods", 0.30, 1700, 700),
    ("Padix Island", "Island/Water", 62, 250, 340, 627, "agris marni", 0.50, 1650, 550),
]

# Territory hubs: city/storage hub players route through. Keys must be unique vs zone slugs.
HUBS = [
    ("hub-calph", "Calpheon City", "Calpheon", 2300, 1400),
    ("hub-serend", "Heidel", "Serendia", 2000, 2000),
    ("hub-mediah", "Tarif", "Mediah", 2600, 2650),
    ("hub-grana", "Grana", "Kamasylvia", 4500, 3500),
    ("hub-odyl", "Odraxtia", "Odyllita", 5150, 3650),
    ("hub-drieg", "Duvencrune", "Dreighan", 3850, 2550),
    ("hub-winter", "Eilton", "Mountain of Eternal Winter", 4350, 1950),
    ("hub-ale", "Alessa (Ulukita)", "Ulukita", 5400, 2850),
    ("hub-valen", "Valencia City", "Valencia", 3450, 3000),
    ("hub-velia", "Velia", "Balenos", 1700, 1700),
]

def slug(name):
    s = re.sub(r"[^a-z0-9]+", "-", name.lower()).strip("-")
    return s

def build_nodes():
    nodes = []
    for name, terr, lvl, ap, dp, silver_m, tags, risk, x, y in ZONES:
        nodes.append({
            "key": slug(name), "name": name, "kind": "grind-zone", "territory": terr,
            "level": lvl, "recommendedAp": ap, "recommendedDp": dp,
            # ponytail: silver is pre-tax M/hr with blue scroll + Agris; planner treats it as relative ranking only
            "expectedSilverPerHour": int(silver_m * 1_000_000),
            "risk": risk, "tags": tags, "x": x, "y": y,
            "updatedAt": "2026-08-21T00:00:00Z",
        })
    for key, name, terr, x, y in HUBS:
        nodes.append({"key": key, "name": name, "kind": "node", "territory": terr,
                      "x": x, "y": y, "risk": 0.0, "tags": "city hub storage",
                      "updatedAt": "2026-08-21T00:00:00Z"})
    return nodes

def dist(a, b):
    return math.hypot(a["x"] - b["x"], a["y"] - b["y"])

def travel_minutes(d):
    # ~450 units/min horse+map fast travel blend; clamp 2..25 min. Advisory only.
    return round(min(25.0, max(2.0, d / 450.0)), 1)

def build_edges(nodes):
    by_key = {n["key"]: n for n in nodes}
    edges = []
    # 1) every zone <-> its territory hub
    for n in nodes:
        hub = next((h for h in HUBS if h[2] == n["territory"]), None)
        if hub is None:  # territory with no hub (e.g. Balenos) — skip, planner falls back to coordinates
            continue
        d = dist(n, by_key[hub[0]])
        edges.append({"from": hub[0], "to": n["key"], "travelMinutes": travel_minutes(d),
                      "risk": round(min(0.5, n["risk"] * 0.4), 2), "bidirectional": True,
                      "transport": "ground"})
    # 2) hub-to-hub spine
    spine = [("hub-velia", "hub-serend", 6), ("hub-serend", "hub-calph", 8),
             ("hub-serend", "hub-mediah", 10), ("hub-calph", "hub-grana", 18),
             ("hub-grana", "hub-odyl", 12), ("hub-drieg", "hub-winter", 8),
             ("hub-mediah", "hub-valen", 22), ("hub-valen", "hub-drieg", 14),
             ("hub-drieg", "hub-ale", 16), ("hub-ale", "hub-valen", 18)]
    for a, b, m in spine:
        edges.append({"from": a, "to": b, "travelMinutes": m, "risk": 0.05,
                      "bidirectional": True, "transport": "horse"})
    return edges

def main():
    nodes = build_nodes()
    edges = build_edges(nodes)
    catalog = {
        "schema": "cuda-spirit-route-v1",
        "notice": ("Community-sourced reference values (GrumpyG monster-zone table, Aug 2024 roster; "
                   "verified against current 2026 zone list). Silver = millions/hour with blue loot "
                   "scroll, pre-tax, Agris-dependent. Treat as ranking guidance, not guaranteed income."),
        "sourceUrl": "https://grumpygreen.cricket/bdo-grinding-spots/",
        "nodes": nodes, "edges": edges,
    }
    out = os.path.join(os.path.dirname(__file__), "..", "data", "bdo-grind-catalog.json")
    with open(os.path.abspath(out), "w", encoding="utf-8") as f:
        json.dump(catalog, f, indent=1, ensure_ascii=False)
    print(f"nodes={len(nodes)} zones={len(ZONES)} hubs={len(HUBS)} edges={len(edges)}")

if __name__ == "__main__":
    main()
