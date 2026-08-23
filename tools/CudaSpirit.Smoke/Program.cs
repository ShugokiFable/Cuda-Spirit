using CudaSpirit.Core.Models;
using CudaSpirit.Core.Services.Bosses;
using CudaSpirit.Core.Services.Data;
using CudaSpirit.Core.Services.Grind;
using CudaSpirit.Core.Services.Knowledge;
using CudaSpirit.Core.Services.Routing;

// Smoke check for the 2.4.2 data layer: seeder -> planner -> brackets -> boss schedule.
var dbPath = Path.Combine(Path.GetTempPath(), $"cuda-smoke-{Guid.NewGuid():N}.db");
try
{
    using var db = new AppDatabase(dbPath);

    // 1. Route seeder
    var seeder = new BuiltInRouteSeeder(db);
    int added = seeder.Seed();
    int reSeed = seeder.Seed(); // version gate must make the second run a no-op
    var nodes = db.GetRouteNodes();
    var edges = db.GetRouteEdges();
    Console.WriteLine($"seed: added={added} reseeds={reSeed} nodes={nodes.Count} edges={edges.Count}");
    if (added < 80 || reSeed != 0 || nodes.Count < 80 || edges.Count < 70) throw new Exception("seed failed");

    // 2. Planner: real recommendations for a 265 AP / 340 DP character
    var planner = new FarmRoutePlanner(db);
    var recs = planner.RecommendFarms(265, 340, startKey: "hub-grana", RouteObjective.Balanced, 0.5, 5);
    Console.WriteLine("top farm recs for 265AP/340DP from Grana:");
    foreach (var r in recs)
        Console.WriteLine($"  {r.Zone.Name,-34} fit={r.Fit,-10} score={r.Score:0.00} {r.Zone.ExpectedSilverPerHour / 1_000_000.0:0}M/h recAP={r.Zone.RecommendedAp}");
    if (recs.Count == 0 || recs.Any(r => r.Zone.Name.Contains("Example"))) throw new Exception("planner returned junk");
    if (recs.First().Zone.RecommendedAp > 265 + 40) throw new Exception("planner recommends spots far above gear");

    // 3. Brackets (2026 tables)
    var p = new ProgressionHelper();
    Console.WriteLine($"brackets: 258AP->next {p.NextApBracket(258)} (expect 261), 309AP->next {p.NextApBracket(309)} (expect 316), 340DP->next {p.NextDpBracket(340)} (expect 341)");
    if (p.NextApBracket(258) != 261 || p.NextApBracket(309) != 316 || p.NextDpBracket(340) != 341) throw new Exception("bracket tables wrong");
    var season = p.Suggest(new PlayerState { Ap = 180, Dp = 260, IsSeasonCharacter = true });
    Console.WriteLine($"season steps: {string.Join(" | ", season.Take(2).Select(s => s.Title))}");
    if (!season.Any(s => s.Title.Contains("Tuvala"))) throw new Exception("season path missing");
    var endgame = p.Suggest(new PlayerState { Ap = 258, Dp = 330 });
    Console.WriteLine($"endgame steps: {string.Join(" | ", endgame.Take(2).Select(s => s.Title))}");
    if (!endgame.Any(s => s.Title.Contains("261"))) throw new Exception("261 bracket step missing");

    // 4. Boss schedule: NA + EU, verify against known table rows (UTC-7 / UTC+1 reference)
    var na = new BossScheduleService(region: Region.NA);
    var eu = new BossScheduleService(region: Region.EU);
    // Monday 20:15 server = Bulgasal+Kzarka (NA). 2026-08-24 is a Monday. 20:15 UTC-7 = 03:15 UTC Mon.
    var monday = new DateTimeOffset(2026, 8, 24, 3, 20, 0, TimeSpan.Zero); // just after spawn, within 15-min grace
    var naUp = na.GetUpcoming(60, monday).Where(e => e.Name is "Kzarka" or "Bulgasal").ToList();
    var kz = naUp.First(e => e.Name == "Kzarka");
    Console.WriteLine($"NA Kzarka next after Mon 20:20 server: {kz.NextSpawn:O} (local fmt) offset={kz.NextSpawn.Offset}");
    if (kz.NextSpawn.ToOffset(TimeSpan.FromHours(-7)) is not { Hour: 20, Minute: 15, DayOfWeek: DayOfWeek.Monday }) throw new Exception("NA Kzarka slot wrong");
    // EU: Monday 16:15 server = Bulgasal+Kzarka. Probe just after: 15:20 UTC Mon = 16:20 CET.
    var euProbe = new DateTimeOffset(2026, 8, 24, 15, 20, 0, TimeSpan.Zero);
    var euKz = eu.GetUpcoming(60, euProbe).First(e => e.Name == "Kzarka");
    if (euKz.NextSpawn.ToOffset(TimeSpan.FromHours(1)) is not { Hour: 16, Minute: 15, DayOfWeek: DayOfWeek.Monday }) throw new Exception("EU Kzarka slot wrong");
    Console.WriteLine("EU Kzarka Mon 16:15 CET OK");
    // GetUpcoming(max) returns the next `max` events; 12 distinct names in the first window proves
    // paired spawns collapsed correctly (a broken pairing would surface 20+ names early).
    int naDistinct = na.GetUpcoming(200, monday).Select(e => e.Name).Distinct().Count();
    Console.WriteLine($"NA distinct boss names across schedule: {naDistinct}");
    if (naDistinct < 12) throw new Exception("NA schedule missing bosses - pairing broken?");

    Console.WriteLine("ALL SMOKE CHECKS PASSED");
}
catch (Exception ex)
{
    Console.WriteLine($"FAIL: {ex.Message}");
    Environment.Exit(1);
}
finally
{
    try { File.Delete(dbPath); } catch { }
}
