using CudaSpirit.Core.Models;
using Microsoft.Data.Sqlite;

namespace CudaSpirit.Core.Services.Data;

/// <summary>
/// Thread-safe local SQLite store. Besides player state and market caches, v2 adds a normalized,
/// provenance-aware knowledge base, source health, market history, and a route graph. Full-text
/// search is used when SQLite FTS5 is available and transparently falls back to LIKE otherwise.
/// </summary>
public sealed partial class AppDatabase : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly object _gate = new();
    private bool _ftsAvailable;

    public string DatabasePath { get; }

    public AppDatabase(string? dbPath = null)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CudaSpirit");
        Directory.CreateDirectory(dir);
        DatabasePath = dbPath ?? Path.Combine(dir, "cudaspirit.db");

        _conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true,
            Pooling = false
        }.ToString());
        _conn.Open();
        Initialize();
    }

    private void Initialize()
    {
        lock (_gate)
        {
            ExecNoLock("""
                PRAGMA journal_mode=WAL;
                PRAGMA synchronous=NORMAL;
                PRAGMA temp_store=MEMORY;
                PRAGMA busy_timeout=5000;
                PRAGMA foreign_keys=ON;

                CREATE TABLE IF NOT EXISTS schema_info (
                    key   TEXT PRIMARY KEY,
                    value TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS ai_cache (
                    key        TEXT PRIMARY KEY,
                    model      TEXT NOT NULL,
                    response   TEXT NOT NULL,
                    created_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS gear (
                    id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    name         TEXT NOT NULL,
                    slot         INTEGER NOT NULL,
                    kind         INTEGER NOT NULL,
                    grade        INTEGER NOT NULL,
                    caphras      INTEGER NOT NULL DEFAULT 0,
                    ap           INTEGER NOT NULL DEFAULT 0,
                    dp           INTEGER NOT NULL DEFAULT 0,
                    market_id    INTEGER,
                    market_value INTEGER NOT NULL DEFAULT 0,
                    equipped     INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS grind_log (
                    id         INTEGER PRIMARY KEY AUTOINCREMENT,
                    zone       TEXT NOT NULL,
                    started_at TEXT NOT NULL,
                    ended_at   TEXT NOT NULL,
                    trash      INTEGER NOT NULL,
                    silver     INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS price_alert (
                    id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    item_id     INTEGER NOT NULL,
                    sid         INTEGER NOT NULL DEFAULT 0,
                    item_name   TEXT NOT NULL,
                    target      INTEGER NOT NULL,
                    on_restock  INTEGER NOT NULL DEFAULT 1,
                    enabled     INTEGER NOT NULL DEFAULT 1,
                    last_fired  TEXT
                );

                CREATE TABLE IF NOT EXISTS market_cache (
                    item_id    INTEGER NOT NULL,
                    sid        INTEGER NOT NULL,
                    name       TEXT NOT NULL,
                    base_price INTEGER NOT NULL,
                    stock      INTEGER NOT NULL,
                    trades     INTEGER NOT NULL,
                    retrieved  TEXT NOT NULL,
                    PRIMARY KEY (item_id, sid)
                );

                CREATE TABLE IF NOT EXISTS market_history (
                    item_id   INTEGER NOT NULL,
                    sid       INTEGER NOT NULL,
                    observed  TEXT NOT NULL,
                    price     INTEGER NOT NULL,
                    stock     INTEGER NOT NULL DEFAULT 0,
                    trades    INTEGER NOT NULL DEFAULT 0,
                    source_id TEXT NOT NULL DEFAULT 'arsha',
                    PRIMARY KEY (item_id, sid, observed)
                );
                CREATE INDEX IF NOT EXISTS ix_market_history_item
                    ON market_history(item_id, sid, observed DESC);

                CREATE TABLE IF NOT EXISTS source_state (
                    source_id         TEXT PRIMARY KEY,
                    display_name      TEXT NOT NULL,
                    status            TEXT NOT NULL,
                    last_attempt_at   TEXT,
                    last_success_at   TEXT,
                    last_record_count INTEGER NOT NULL DEFAULT 0,
                    last_error        TEXT NOT NULL DEFAULT '',
                    cursor            TEXT NOT NULL DEFAULT '',
                    metadata_json     TEXT NOT NULL DEFAULT '{}'
                );

                CREATE TABLE IF NOT EXISTS import_file_state (
                    path          TEXT PRIMARY KEY COLLATE NOCASE,
                    source_id     TEXT NOT NULL,
                    size_bytes    INTEGER NOT NULL,
                    modified_utc  TEXT NOT NULL,
                    imported_at   TEXT NOT NULL,
                    record_count  INTEGER NOT NULL DEFAULT 0,
                    node_count    INTEGER NOT NULL DEFAULT 0,
                    edge_count    INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS knowledge_record (
                    id            INTEGER PRIMARY KEY AUTOINCREMENT,
                    source_id     TEXT NOT NULL,
                    external_id   TEXT NOT NULL,
                    kind          TEXT NOT NULL,
                    title         TEXT NOT NULL,
                    summary       TEXT NOT NULL DEFAULT '',
                    content       TEXT NOT NULL DEFAULT '',
                    url           TEXT NOT NULL DEFAULT '',
                    region        TEXT NOT NULL DEFAULT 'global',
                    tags          TEXT NOT NULL DEFAULT '',
                    metadata_json TEXT NOT NULL DEFAULT '{}',
                    content_hash  TEXT NOT NULL DEFAULT '',
                    confidence    REAL NOT NULL DEFAULT 0.8,
                    effective_at  TEXT,
                    retrieved_at  TEXT NOT NULL,
                    expires_at    TEXT,
                    UNIQUE(source_id, external_id)
                );
                CREATE INDEX IF NOT EXISTS ix_knowledge_kind_region
                    ON knowledge_record(kind, region, effective_at DESC);
                CREATE INDEX IF NOT EXISTS ix_knowledge_retrieved
                    ON knowledge_record(retrieved_at DESC);

                CREATE TABLE IF NOT EXISTS route_node (
                    node_key             TEXT PRIMARY KEY,
                    name                 TEXT NOT NULL,
                    territory            TEXT NOT NULL DEFAULT '',
                    x                    REAL NOT NULL DEFAULT 0,
                    y                    REAL NOT NULL DEFAULT 0,
                    recommended_ap       INTEGER NOT NULL DEFAULT 0,
                    recommended_dp       INTEGER NOT NULL DEFAULT 0,
                    expected_silver_hour INTEGER NOT NULL DEFAULT 0,
                    risk                 REAL NOT NULL DEFAULT 0,
                    tags                 TEXT NOT NULL DEFAULT '',
                    source_id            TEXT NOT NULL DEFAULT '',
                    updated_at           TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS route_edge (
                    id             INTEGER PRIMARY KEY AUTOINCREMENT,
                    from_key       TEXT NOT NULL,
                    to_key         TEXT NOT NULL,
                    travel_minutes REAL NOT NULL,
                    risk           REAL NOT NULL DEFAULT 0,
                    bidirectional  INTEGER NOT NULL DEFAULT 1,
                    transport      TEXT NOT NULL DEFAULT 'ground',
                    source_id      TEXT NOT NULL DEFAULT '',
                    updated_at     TEXT NOT NULL,
                    UNIQUE(from_key, to_key, transport),
                    FOREIGN KEY(from_key) REFERENCES route_node(node_key) ON DELETE CASCADE,
                    FOREIGN KEY(to_key) REFERENCES route_node(node_key) ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS ix_route_edge_from ON route_edge(from_key);
                CREATE INDEX IF NOT EXISTS ix_route_edge_to ON route_edge(to_key);

                CREATE TABLE IF NOT EXISTS companion_task (
                    id            INTEGER PRIMARY KEY AUTOINCREMENT,
                    title         TEXT NOT NULL,
                    detail        TEXT NOT NULL DEFAULT '',
                    category      TEXT NOT NULL DEFAULT 'general',
                    cadence       TEXT NOT NULL DEFAULT 'once',
                    priority      INTEGER NOT NULL DEFAULT 50,
                    due_at        TEXT,
                    status        TEXT NOT NULL DEFAULT 'open',
                    pinned        INTEGER NOT NULL DEFAULT 0,
                    source_url    TEXT NOT NULL DEFAULT '',
                    metadata_json TEXT NOT NULL DEFAULT '{}',
                    created_at    TEXT NOT NULL,
                    completed_at  TEXT
                );
                CREATE INDEX IF NOT EXISTS ix_companion_task_status_priority
                    ON companion_task(status, pinned DESC, priority DESC, due_at);

                CREATE TABLE IF NOT EXISTS item_decision_history (
                    id         INTEGER PRIMARY KEY AUTOINCREMENT,
                    item_name  TEXT NOT NULL,
                    verdict    TEXT NOT NULL,
                    reason     TEXT NOT NULL DEFAULT '',
                    binding    TEXT NOT NULL DEFAULT '',
                    created_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS pearl_evaluation_history (
                    id                    INTEGER PRIMARY KEY AUTOINCREMENT,
                    offer_name            TEXT NOT NULL,
                    price_pearls          INTEGER NOT NULL DEFAULT 0,
                    original_price_pearls INTEGER NOT NULL DEFAULT 0,
                    score                 INTEGER NOT NULL DEFAULT 0,
                    verdict               TEXT NOT NULL DEFAULT '',
                    notes                 TEXT NOT NULL DEFAULT '',
                    evaluated_at          TEXT NOT NULL
                );

                INSERT INTO schema_info(key, value) VALUES('schema_version', '4')
                ON CONFLICT(key) DO UPDATE SET value='4';
                """);

            try
            {
                ExecNoLock("""
                    CREATE VIRTUAL TABLE IF NOT EXISTS knowledge_fts USING fts5(
                        title, summary, content, tags,
                        content='knowledge_record', content_rowid='id',
                        tokenize='unicode61 remove_diacritics 2'
                    );

                    CREATE TRIGGER IF NOT EXISTS knowledge_ai AFTER INSERT ON knowledge_record BEGIN
                        INSERT INTO knowledge_fts(rowid,title,summary,content,tags)
                        VALUES(new.id,new.title,new.summary,new.content,new.tags);
                    END;
                    CREATE TRIGGER IF NOT EXISTS knowledge_ad AFTER DELETE ON knowledge_record BEGIN
                        INSERT INTO knowledge_fts(knowledge_fts,rowid,title,summary,content,tags)
                        VALUES('delete',old.id,old.title,old.summary,old.content,old.tags);
                    END;
                    CREATE TRIGGER IF NOT EXISTS knowledge_au AFTER UPDATE ON knowledge_record BEGIN
                        INSERT INTO knowledge_fts(knowledge_fts,rowid,title,summary,content,tags)
                        VALUES('delete',old.id,old.title,old.summary,old.content,old.tags);
                        INSERT INTO knowledge_fts(rowid,title,summary,content,tags)
                        VALUES(new.id,new.title,new.summary,new.content,new.tags);
                    END;
                    """);

                // Populate the external-content index once after upgrading an existing database.
                using (var check = _conn.CreateCommand())
                {
                    check.CommandText = "SELECT value FROM schema_info WHERE key='fts_rebuilt_v2' LIMIT 1";
                    if (check.ExecuteScalar() is null)
                    {
                        ExecNoLock("INSERT INTO knowledge_fts(knowledge_fts) VALUES('rebuild')");
                        ExecNoLock("INSERT INTO schema_info(key,value) VALUES('fts_rebuilt_v2','1') ON CONFLICT(key) DO UPDATE SET value='1'");
                    }
                }
                _ftsAvailable = true;
            }
            catch (SqliteException)
            {
                _ftsAvailable = false;
            }
        }
    }

    private void ExecNoLock(string sql, SqliteTransaction? tx = null)
    {
        using var cmd = _conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    // ---- AI cache ---------------------------------------------------------

    public string? GetCachedAi(string key, TimeSpan maxAge)
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT response, created_at FROM ai_cache WHERE key=$k";
            cmd.Parameters.AddWithValue("$k", key);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            var created = ParseDate(r.GetString(1));
            if (DateTimeOffset.UtcNow - created > maxAge) return null;
            return r.GetString(0);
        }
    }

    public void PutCachedAi(string key, string model, string response)
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO ai_cache(key, model, response, created_at)
                VALUES($k,$m,$r,$t)
                ON CONFLICT(key) DO UPDATE SET model=$m, response=$r, created_at=$t;
                """;
            cmd.Parameters.AddWithValue("$k", key);
            cmd.Parameters.AddWithValue("$m", model);
            cmd.Parameters.AddWithValue("$r", response);
            cmd.Parameters.AddWithValue("$t", DateTimeOffset.UtcNow.ToString("O"));
            cmd.ExecuteNonQuery();
        }
    }

    public int PruneAiCache(TimeSpan maxAge)
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM ai_cache WHERE created_at < $cut";
            cmd.Parameters.AddWithValue("$cut", DateTimeOffset.UtcNow.Subtract(maxAge).ToString("O"));
            return cmd.ExecuteNonQuery();
        }
    }

    // ---- Gear -------------------------------------------------------------

    public IReadOnlyList<GearItem> GetGear()
    {
        lock (_gate)
        {
            var list = new List<GearItem>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id,name,slot,kind,grade,caphras,ap,dp,market_id,market_value,equipped FROM gear ORDER BY equipped DESC, slot";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new GearItem
                {
                    Id = r.GetInt64(0),
                    Name = r.GetString(1),
                    Slot = (GearSlot)r.GetInt32(2),
                    Kind = (EnhanceKind)r.GetInt32(3),
                    Grade = (EnhanceGrade)r.GetInt32(4),
                    Caphras = r.GetInt32(5),
                    Ap = r.GetInt32(6),
                    Dp = r.GetInt32(7),
                    MarketItemId = r.IsDBNull(8) ? null : r.GetInt64(8),
                    MarketValue = r.GetInt64(9),
                    Equipped = r.GetInt32(10) != 0
                });
            }
            return list;
        }
    }

    public long UpsertGear(GearItem g)
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            if (g.Id == 0)
            {
                cmd.CommandText = """
                    INSERT INTO gear(name,slot,kind,grade,caphras,ap,dp,market_id,market_value,equipped)
                    VALUES($n,$s,$k,$g,$c,$ap,$dp,$mid,$mv,$e);
                    SELECT last_insert_rowid();
                    """;
            }
            else
            {
                cmd.CommandText = """
                    UPDATE gear SET name=$n,slot=$s,kind=$k,grade=$g,caphras=$c,ap=$ap,dp=$dp,
                        market_id=$mid,market_value=$mv,equipped=$e WHERE id=$id;
                    SELECT $id;
                    """;
                cmd.Parameters.AddWithValue("$id", g.Id);
            }
            cmd.Parameters.AddWithValue("$n", g.Name);
            cmd.Parameters.AddWithValue("$s", (int)g.Slot);
            cmd.Parameters.AddWithValue("$k", (int)g.Kind);
            cmd.Parameters.AddWithValue("$g", (int)g.Grade);
            cmd.Parameters.AddWithValue("$c", g.Caphras);
            cmd.Parameters.AddWithValue("$ap", g.Ap);
            cmd.Parameters.AddWithValue("$dp", g.Dp);
            cmd.Parameters.AddWithValue("$mid", (object?)g.MarketItemId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$mv", g.MarketValue);
            cmd.Parameters.AddWithValue("$e", g.Equipped ? 1 : 0);
            return Convert.ToInt64(cmd.ExecuteScalar() ?? 0L);
        }
    }

    public void DeleteGear(long id)
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM gear WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
    }

    // ---- Grind log --------------------------------------------------------

    public void AddGrindLog(GrindLog log)
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO grind_log(zone,started_at,ended_at,trash,silver)
                VALUES($z,$s,$e,$t,$sil);
                """;
            cmd.Parameters.AddWithValue("$z", log.Zone);
            cmd.Parameters.AddWithValue("$s", log.StartedAt.ToString("O"));
            cmd.Parameters.AddWithValue("$e", log.EndedAt.ToString("O"));
            cmd.Parameters.AddWithValue("$t", log.TrashCount);
            cmd.Parameters.AddWithValue("$sil", log.SilverEarned);
            cmd.ExecuteNonQuery();
        }
    }

    public IReadOnlyList<GrindLog> GetGrindLogs(int limit = 50)
    {
        lock (_gate)
        {
            var list = new List<GrindLog>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id,zone,started_at,ended_at,trash,silver FROM grind_log ORDER BY started_at DESC LIMIT $l";
            cmd.Parameters.AddWithValue("$l", Math.Clamp(limit, 1, 500));
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new GrindLog
                {
                    Id = r.GetInt64(0),
                    Zone = r.GetString(1),
                    StartedAt = ParseDate(r.GetString(2)),
                    EndedAt = ParseDate(r.GetString(3)),
                    TrashCount = r.GetInt32(4),
                    SilverEarned = r.GetInt64(5)
                });
            }
            return list;
        }
    }

    // ---- Market -----------------------------------------------------------

    public void CacheMarket(MarketItem m, string sourceId = "arsha")
    {
        lock (_gate)
        {
            using var tx = _conn.BeginTransaction();
            using (var cmd = _conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO market_cache(item_id,sid,name,base_price,stock,trades,retrieved)
                    VALUES($i,$s,$n,$p,$st,$tr,$rt)
                    ON CONFLICT(item_id,sid) DO UPDATE SET
                        name=$n, base_price=$p, stock=$st, trades=$tr, retrieved=$rt;
                    """;
                AddMarketParameters(cmd, m);
                cmd.ExecuteNonQuery();
            }
            using (var history = _conn.CreateCommand())
            {
                history.Transaction = tx;
                history.CommandText = """
                    INSERT OR IGNORE INTO market_history(item_id,sid,observed,price,stock,trades,source_id)
                    VALUES($i,$s,$o,$p,$st,$tr,$src);
                    """;
                history.Parameters.AddWithValue("$i", m.ItemId);
                history.Parameters.AddWithValue("$s", m.Sid);
                history.Parameters.AddWithValue("$o", m.Retrieved.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:00.0000000+00:00"));
                history.Parameters.AddWithValue("$p", m.BasePrice);
                history.Parameters.AddWithValue("$st", m.CurrentStock);
                history.Parameters.AddWithValue("$tr", m.TotalTrades);
                history.Parameters.AddWithValue("$src", sourceId);
                history.ExecuteNonQuery();
            }
            tx.Commit();
        }
    }

    private static void AddMarketParameters(SqliteCommand cmd, MarketItem m)
    {
        cmd.Parameters.AddWithValue("$i", m.ItemId);
        cmd.Parameters.AddWithValue("$s", m.Sid);
        cmd.Parameters.AddWithValue("$n", m.Name);
        cmd.Parameters.AddWithValue("$p", m.BasePrice);
        cmd.Parameters.AddWithValue("$st", m.CurrentStock);
        cmd.Parameters.AddWithValue("$tr", m.TotalTrades);
        cmd.Parameters.AddWithValue("$rt", m.Retrieved.ToString("O"));
    }

    public MarketItem? GetCachedMarket(long itemId, int sid, TimeSpan maxAge, bool allowStale = false)
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT item_id,sid,name,base_price,stock,trades,retrieved FROM market_cache WHERE item_id=$i AND sid=$s";
            cmd.Parameters.AddWithValue("$i", itemId);
            cmd.Parameters.AddWithValue("$s", sid);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            var retrieved = ParseDate(r.GetString(6));
            if (!allowStale && DateTimeOffset.UtcNow - retrieved > maxAge) return null;
            return ReadMarket(r, retrieved);
        }
    }

    public IReadOnlyList<MarketItem> GetRecentMarketItems(int limit = 100)
    {
        lock (_gate)
        {
            var result = new List<MarketItem>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT item_id,sid,name,base_price,stock,trades,retrieved FROM market_cache ORDER BY retrieved DESC LIMIT $l";
            cmd.Parameters.AddWithValue("$l", Math.Clamp(limit, 1, 1000));
            using var r = cmd.ExecuteReader();
            while (r.Read()) result.Add(ReadMarket(r, ParseDate(r.GetString(6))));
            return result;
        }
    }

    private static MarketItem ReadMarket(SqliteDataReader r, DateTimeOffset retrieved) => new()
    {
        ItemId = r.GetInt64(0),
        Sid = r.GetInt32(1),
        Name = r.GetString(2),
        BasePrice = r.GetInt64(3),
        CurrentStock = r.GetInt64(4),
        TotalTrades = r.GetInt64(5),
        Retrieved = retrieved
    };

    // ---- Price alerts -----------------------------------------------------

    public IReadOnlyList<PriceAlert> GetAlerts()
    {
        lock (_gate)
        {
            var list = new List<PriceAlert>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id,item_id,sid,item_name,target,on_restock,enabled,last_fired FROM price_alert ORDER BY enabled DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new PriceAlert
                {
                    Id = r.GetInt64(0),
                    ItemId = r.GetInt64(1),
                    Sid = r.GetInt32(2),
                    ItemName = r.GetString(3),
                    TargetPrice = r.GetInt64(4),
                    NotifyOnRestock = r.GetInt32(5) != 0,
                    Enabled = r.GetInt32(6) != 0,
                    LastTriggered = r.IsDBNull(7) ? null : ParseDate(r.GetString(7))
                });
            }
            return list;
        }
    }

    public long UpsertAlert(PriceAlert a)
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            if (a.Id == 0)
            {
                cmd.CommandText = """
                    INSERT INTO price_alert(item_id,sid,item_name,target,on_restock,enabled,last_fired)
                    VALUES($i,$s,$n,$t,$r,$e,$lf);
                    SELECT last_insert_rowid();
                    """;
            }
            else
            {
                cmd.CommandText = """
                    UPDATE price_alert SET item_id=$i,sid=$s,item_name=$n,target=$t,
                        on_restock=$r,enabled=$e,last_fired=$lf WHERE id=$id;
                    SELECT $id;
                    """;
                cmd.Parameters.AddWithValue("$id", a.Id);
            }
            cmd.Parameters.AddWithValue("$i", a.ItemId);
            cmd.Parameters.AddWithValue("$s", a.Sid);
            cmd.Parameters.AddWithValue("$n", a.ItemName);
            cmd.Parameters.AddWithValue("$t", a.TargetPrice);
            cmd.Parameters.AddWithValue("$r", a.NotifyOnRestock ? 1 : 0);
            cmd.Parameters.AddWithValue("$e", a.Enabled ? 1 : 0);
            cmd.Parameters.AddWithValue("$lf", (object?)a.LastTriggered?.ToString("O") ?? DBNull.Value);
            return Convert.ToInt64(cmd.ExecuteScalar() ?? 0L);
        }
    }

    public void DeleteAlert(long id)
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM price_alert WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
    }

    // ---- Incremental local imports ----------------------------------------

    public bool IsImportFileCurrent(string path, long sizeBytes, DateTime modifiedUtc)
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM import_file_state WHERE path=$path AND size_bytes=$size AND modified_utc=$modified LIMIT 1";
            cmd.Parameters.AddWithValue("$path", Path.GetFullPath(path));
            cmd.Parameters.AddWithValue("$size", sizeBytes);
            cmd.Parameters.AddWithValue("$modified", modifiedUtc.ToUniversalTime().ToString("O"));
            return cmd.ExecuteScalar() is not null;
        }
    }

    public (int Records, int Nodes, int Edges) GetImportTotals()
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT COALESCE(SUM(record_count),0),COALESCE(SUM(node_count),0),COALESCE(SUM(edge_count),0) FROM import_file_state";
            using var reader = cmd.ExecuteReader();
            return reader.Read()
                ? (Convert.ToInt32(reader.GetInt64(0)), Convert.ToInt32(reader.GetInt64(1)), Convert.ToInt32(reader.GetInt64(2)))
                : (0, 0, 0);
        }
    }

    public void MarkImportFile(
        string path,
        string sourceId,
        long sizeBytes,
        DateTime modifiedUtc,
        int recordCount,
        int nodeCount,
        int edgeCount)
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO import_file_state(path,source_id,size_bytes,modified_utc,imported_at,record_count,node_count,edge_count)
                VALUES($path,$source,$size,$modified,$imported,$records,$nodes,$edges)
                ON CONFLICT(path) DO UPDATE SET
                    source_id=$source,size_bytes=$size,modified_utc=$modified,imported_at=$imported,
                    record_count=$records,node_count=$nodes,edge_count=$edges;
                """;
            cmd.Parameters.AddWithValue("$path", Path.GetFullPath(path));
            cmd.Parameters.AddWithValue("$source", sourceId);
            cmd.Parameters.AddWithValue("$size", sizeBytes);
            cmd.Parameters.AddWithValue("$modified", modifiedUtc.ToUniversalTime().ToString("O"));
            cmd.Parameters.AddWithValue("$imported", DateTimeOffset.UtcNow.ToString("O"));
            cmd.Parameters.AddWithValue("$records", recordCount);
            cmd.Parameters.AddWithValue("$nodes", nodeCount);
            cmd.Parameters.AddWithValue("$edges", edgeCount);
            cmd.ExecuteNonQuery();
        }
    }

    // ---- Knowledge --------------------------------------------------------

    public int UpsertKnowledgeBatch(IEnumerable<KnowledgeRecord> records)
    {
        var materialized = records.Where(x => !string.IsNullOrWhiteSpace(x.SourceId) && !string.IsNullOrWhiteSpace(x.ExternalId)).ToList();
        if (materialized.Count == 0) return 0;

        lock (_gate)
        {
            using var tx = _conn.BeginTransaction();
            using var cmd = _conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO knowledge_record(
                    source_id,external_id,kind,title,summary,content,url,region,tags,metadata_json,
                    content_hash,confidence,effective_at,retrieved_at,expires_at)
                VALUES($src,$ext,$kind,$title,$summary,$content,$url,$region,$tags,$meta,$hash,$conf,$effective,$retrieved,$expires)
                ON CONFLICT(source_id,external_id) DO UPDATE SET
                    kind=$kind,title=$title,summary=$summary,content=$content,url=$url,region=$region,
                    tags=$tags,metadata_json=$meta,content_hash=$hash,confidence=$conf,effective_at=$effective,
                    retrieved_at=$retrieved,expires_at=$expires;
                """;

            foreach (var r in materialized)
            {
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("$src", r.SourceId);
                cmd.Parameters.AddWithValue("$ext", r.ExternalId);
                cmd.Parameters.AddWithValue("$kind", r.Kind);
                cmd.Parameters.AddWithValue("$title", r.Title);
                cmd.Parameters.AddWithValue("$summary", r.Summary);
                cmd.Parameters.AddWithValue("$content", r.Content);
                cmd.Parameters.AddWithValue("$url", r.Url);
                cmd.Parameters.AddWithValue("$region", r.Region);
                cmd.Parameters.AddWithValue("$tags", r.Tags);
                cmd.Parameters.AddWithValue("$meta", r.MetadataJson);
                cmd.Parameters.AddWithValue("$hash", r.ContentHash);
                cmd.Parameters.AddWithValue("$conf", Math.Clamp(r.Confidence, 0, 1));
                cmd.Parameters.AddWithValue("$effective", (object?)r.EffectiveAt?.ToString("O") ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$retrieved", r.RetrievedAt.ToString("O"));
                cmd.Parameters.AddWithValue("$expires", (object?)r.ExpiresAt?.ToString("O") ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
            return materialized.Count;
        }
    }

    public IReadOnlyList<KnowledgeSearchHit> SearchKnowledge(string query, string region, int limit = 8)
    {
        limit = Math.Clamp(limit, 1, 50);
        lock (_gate)
        {
            if (_ftsAvailable && !string.IsNullOrWhiteSpace(query))
            {
                try
                {
                    return SearchFtsNoLock(query, region, limit);
                }
                catch (SqliteException)
                {
                    // Malformed FTS syntax or a build without FTS support: use safe LIKE fallback.
                }
            }
            return SearchLikeNoLock(query, region, limit);
        }
    }

    private IReadOnlyList<KnowledgeSearchHit> SearchFtsNoLock(string query, string region, int limit)
    {
        var list = new List<KnowledgeSearchHit>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            SELECT k.id,k.source_id,k.external_id,k.kind,k.title,k.summary,k.content,k.url,k.region,k.tags,
                   k.metadata_json,k.content_hash,k.confidence,k.effective_at,k.retrieved_at,k.expires_at,
                   bm25(knowledge_fts, 7.0, 3.0, 1.0, 2.0) AS rank
            FROM knowledge_fts
            JOIN knowledge_record k ON k.id=knowledge_fts.rowid
            WHERE knowledge_fts MATCH $q
              AND (k.region='global' OR k.region=$region)
              AND (k.expires_at IS NULL OR k.expires_at > $now)
            ORDER BY rank, COALESCE(k.effective_at,k.retrieved_at) DESC
            LIMIT $l;
            """;
        cmd.Parameters.AddWithValue("$q", query);
        cmd.Parameters.AddWithValue("$region", region);
        cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$l", limit);
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(new KnowledgeSearchHit { Record = ReadKnowledge(r), Rank = r.GetDouble(16) });
        return list;
    }

    private IReadOnlyList<KnowledgeSearchHit> SearchLikeNoLock(string query, string region, int limit)
    {
        var list = new List<KnowledgeSearchHit>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            SELECT id,source_id,external_id,kind,title,summary,content,url,region,tags,metadata_json,
                   content_hash,confidence,effective_at,retrieved_at,expires_at
            FROM knowledge_record
            WHERE (region='global' OR region=$region)
              AND (expires_at IS NULL OR expires_at > $now)
              AND ($empty=1 OR title LIKE $q OR summary LIKE $q OR content LIKE $q OR tags LIKE $q)
            ORDER BY COALESCE(effective_at,retrieved_at) DESC
            LIMIT $l;
            """;
        var empty = string.IsNullOrWhiteSpace(query);
        cmd.Parameters.AddWithValue("$region", region);
        cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$empty", empty ? 1 : 0);
        cmd.Parameters.AddWithValue("$q", $"%{query.Trim()}%");
        cmd.Parameters.AddWithValue("$l", limit);
        using var r = cmd.ExecuteReader();
        var rank = 0d;
        while (r.Read()) list.Add(new KnowledgeSearchHit { Record = ReadKnowledge(r), Rank = rank++ });
        return list;
    }

    public IReadOnlyList<KnowledgeRecord> GetLatestKnowledge(int limit = 20, string? kind = null, string? region = null)
    {
        lock (_gate)
        {
            var list = new List<KnowledgeRecord>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                SELECT id,source_id,external_id,kind,title,summary,content,url,region,tags,metadata_json,
                       content_hash,confidence,effective_at,retrieved_at,expires_at
                FROM knowledge_record
                WHERE ($kind='' OR kind=$kind)
                  AND ($region='' OR region='global' OR region=$region)
                  AND (expires_at IS NULL OR expires_at > $now)
                ORDER BY COALESCE(effective_at,retrieved_at) DESC
                LIMIT $l;
                """;
            cmd.Parameters.AddWithValue("$kind", kind ?? "");
            cmd.Parameters.AddWithValue("$region", region ?? "");
            cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
            cmd.Parameters.AddWithValue("$l", Math.Clamp(limit, 1, 500));
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(ReadKnowledge(r));
            return list;
        }
    }

    private static KnowledgeRecord ReadKnowledge(SqliteDataReader r) => new()
    {
        Id = r.GetInt64(0),
        SourceId = r.GetString(1),
        ExternalId = r.GetString(2),
        Kind = r.GetString(3),
        Title = r.GetString(4),
        Summary = r.GetString(5),
        Content = r.GetString(6),
        Url = r.GetString(7),
        Region = r.GetString(8),
        Tags = r.GetString(9),
        MetadataJson = r.GetString(10),
        ContentHash = r.GetString(11),
        Confidence = r.GetDouble(12),
        EffectiveAt = r.IsDBNull(13) ? null : ParseDate(r.GetString(13)),
        RetrievedAt = ParseDate(r.GetString(14)),
        ExpiresAt = r.IsDBNull(15) ? null : ParseDate(r.GetString(15))
    };

    public void UpsertSourceState(DataSourceState state)
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO source_state(source_id,display_name,status,last_attempt_at,last_success_at,last_record_count,last_error,cursor,metadata_json)
                VALUES($id,$name,$status,$attempt,$success,$count,$error,$cursor,$meta)
                ON CONFLICT(source_id) DO UPDATE SET
                    display_name=$name,status=$status,last_attempt_at=$attempt,last_success_at=$success,
                    last_record_count=$count,last_error=$error,cursor=$cursor,metadata_json=$meta;
                """;
            cmd.Parameters.AddWithValue("$id", state.SourceId);
            cmd.Parameters.AddWithValue("$name", state.DisplayName);
            cmd.Parameters.AddWithValue("$status", state.Status);
            cmd.Parameters.AddWithValue("$attempt", (object?)state.LastAttemptAt?.ToString("O") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$success", (object?)state.LastSuccessAt?.ToString("O") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$count", state.LastRecordCount);
            cmd.Parameters.AddWithValue("$error", state.LastError);
            cmd.Parameters.AddWithValue("$cursor", state.Cursor);
            cmd.Parameters.AddWithValue("$meta", state.MetadataJson);
            cmd.ExecuteNonQuery();
        }
    }

    public IReadOnlyList<DataSourceState> GetSourceStates()
    {
        lock (_gate)
        {
            var list = new List<DataSourceState>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT source_id,display_name,status,last_attempt_at,last_success_at,last_record_count,last_error,cursor,metadata_json FROM source_state ORDER BY display_name";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new DataSourceState
                {
                    SourceId = r.GetString(0),
                    DisplayName = r.GetString(1),
                    Status = r.GetString(2),
                    LastAttemptAt = r.IsDBNull(3) ? null : ParseDate(r.GetString(3)),
                    LastSuccessAt = r.IsDBNull(4) ? null : ParseDate(r.GetString(4)),
                    LastRecordCount = r.GetInt32(5),
                    LastError = r.GetString(6),
                    Cursor = r.GetString(7),
                    MetadataJson = r.GetString(8)
                });
            }
            return list;
        }
    }

    // ---- Retention / maintenance -----------------------------------------

    public int PruneExpiredKnowledge(TimeSpan grace)
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM knowledge_record WHERE expires_at IS NOT NULL AND expires_at < $cutoff";
            cmd.Parameters.AddWithValue("$cutoff", DateTimeOffset.UtcNow.Subtract(grace).ToString("O"));
            return cmd.ExecuteNonQuery();
        }
    }

    public int PruneMarketHistory(TimeSpan maxAge)
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM market_history WHERE observed < $cutoff";
            cmd.Parameters.AddWithValue("$cutoff", DateTimeOffset.UtcNow.Subtract(maxAge).ToString("O"));
            return cmd.ExecuteNonQuery();
        }
    }

    public void Optimize()
    {
        lock (_gate) ExecNoLock("PRAGMA optimize");
    }

    // ---- Route graph ------------------------------------------------------

    public int UpsertRouteNodes(IEnumerable<RouteNode> nodes)
    {
        var data = nodes.Where(x => !string.IsNullOrWhiteSpace(x.Key)).ToList();
        if (data.Count == 0) return 0;
        lock (_gate)
        {
            using var tx = _conn.BeginTransaction();
            using var cmd = _conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO route_node(node_key,name,territory,x,y,recommended_ap,recommended_dp,expected_silver_hour,risk,tags,source_id,updated_at)
                VALUES($key,$name,$territory,$x,$y,$ap,$dp,$silver,$risk,$tags,$src,$updated)
                ON CONFLICT(node_key) DO UPDATE SET
                    name=$name,territory=$territory,x=$x,y=$y,recommended_ap=$ap,recommended_dp=$dp,
                    expected_silver_hour=$silver,risk=$risk,tags=$tags,source_id=$src,updated_at=$updated;
                """;
            foreach (var n in data)
            {
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("$key", n.Key);
                cmd.Parameters.AddWithValue("$name", n.Name);
                cmd.Parameters.AddWithValue("$territory", n.Territory);
                cmd.Parameters.AddWithValue("$x", n.X);
                cmd.Parameters.AddWithValue("$y", n.Y);
                cmd.Parameters.AddWithValue("$ap", n.RecommendedAp);
                cmd.Parameters.AddWithValue("$dp", n.RecommendedDp);
                cmd.Parameters.AddWithValue("$silver", n.ExpectedSilverPerHour);
                cmd.Parameters.AddWithValue("$risk", Math.Clamp(n.Risk, 0, 1));
                cmd.Parameters.AddWithValue("$tags", n.Tags);
                cmd.Parameters.AddWithValue("$src", n.SourceId);
                cmd.Parameters.AddWithValue("$updated", n.UpdatedAt.ToString("O"));
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
            return data.Count;
        }
    }

    public int UpsertRouteEdges(IEnumerable<RouteEdge> edges)
    {
        var data = edges.Where(x => !string.IsNullOrWhiteSpace(x.FromKey) && !string.IsNullOrWhiteSpace(x.ToKey) && x.TravelMinutes > 0).ToList();
        if (data.Count == 0) return 0;
        lock (_gate)
        {
            var nodeKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var keyCmd = _conn.CreateCommand())
            {
                keyCmd.CommandText = "SELECT node_key FROM route_node";
                using var reader = keyCmd.ExecuteReader();
                while (reader.Read())
                {
                    var key = reader.GetString(0);
                    nodeKeys[key] = key;
                }
            }
            data = data
                .Where(x => nodeKeys.ContainsKey(x.FromKey) && nodeKeys.ContainsKey(x.ToKey))
                .Select(x =>
                {
                    x.FromKey = nodeKeys[x.FromKey];
                    x.ToKey = nodeKeys[x.ToKey];
                    return x;
                })
                .GroupBy(x => (x.FromKey, x.ToKey, x.Transport))
                .Select(g => g.Last())
                .ToList();
            if (data.Count == 0) return 0;

            using var tx = _conn.BeginTransaction();
            using var cmd = _conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO route_edge(from_key,to_key,travel_minutes,risk,bidirectional,transport,source_id,updated_at)
                VALUES($from,$to,$minutes,$risk,$bi,$transport,$src,$updated)
                ON CONFLICT(from_key,to_key,transport) DO UPDATE SET
                    travel_minutes=$minutes,risk=$risk,bidirectional=$bi,source_id=$src,updated_at=$updated;
                """;
            foreach (var e in data)
            {
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("$from", e.FromKey);
                cmd.Parameters.AddWithValue("$to", e.ToKey);
                cmd.Parameters.AddWithValue("$minutes", e.TravelMinutes);
                cmd.Parameters.AddWithValue("$risk", Math.Clamp(e.Risk, 0, 1));
                cmd.Parameters.AddWithValue("$bi", e.Bidirectional ? 1 : 0);
                cmd.Parameters.AddWithValue("$transport", e.Transport);
                cmd.Parameters.AddWithValue("$src", e.SourceId);
                cmd.Parameters.AddWithValue("$updated", e.UpdatedAt.ToString("O"));
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
            return data.Count;
        }
    }

    public IReadOnlyList<RouteNode> GetRouteNodes()
    {
        lock (_gate)
        {
            var list = new List<RouteNode>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT node_key,name,territory,x,y,recommended_ap,recommended_dp,expected_silver_hour,risk,tags,source_id,updated_at FROM route_node ORDER BY name";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new RouteNode
                {
                    Key = r.GetString(0), Name = r.GetString(1), Territory = r.GetString(2), X = r.GetDouble(3), Y = r.GetDouble(4),
                    RecommendedAp = r.GetInt32(5), RecommendedDp = r.GetInt32(6), ExpectedSilverPerHour = r.GetInt64(7),
                    Risk = r.GetDouble(8), Tags = r.GetString(9), SourceId = r.GetString(10), UpdatedAt = ParseDate(r.GetString(11))
                });
            }
            return list;
        }
    }

    public IReadOnlyList<RouteEdge> GetRouteEdges()
    {
        lock (_gate)
        {
            var list = new List<RouteEdge>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id,from_key,to_key,travel_minutes,risk,bidirectional,transport,source_id,updated_at FROM route_edge";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new RouteEdge
                {
                    Id = r.GetInt64(0), FromKey = r.GetString(1), ToKey = r.GetString(2), TravelMinutes = r.GetDouble(3),
                    Risk = r.GetDouble(4), Bidirectional = r.GetInt32(5) != 0, Transport = r.GetString(6),
                    SourceId = r.GetString(7), UpdatedAt = ParseDate(r.GetString(8))
                });
            }
            return list;
        }
    }

    public DatabaseStats GetStats()
    {
        lock (_gate)
        {
            return new DatabaseStats
            {
                KnowledgeRecords = ScalarIntNoLock("SELECT COUNT(*) FROM knowledge_record"),
                MarketSnapshots = ScalarIntNoLock("SELECT COUNT(*) FROM market_cache"),
                MarketHistoryPoints = ScalarIntNoLock("SELECT COUNT(*) FROM market_history"),
                RouteNodes = ScalarIntNoLock("SELECT COUNT(*) FROM route_node"),
                RouteEdges = ScalarIntNoLock("SELECT COUNT(*) FROM route_edge"),
                ImportedFiles = ScalarIntNoLock("SELECT COUNT(*) FROM import_file_state"),
                Sources = ScalarIntNoLock("SELECT COUNT(*) FROM source_state"),
                FreshestKnowledgeAt = ScalarDateNoLock("SELECT MAX(retrieved_at) FROM knowledge_record")
            };
        }
    }

    private int ScalarIntNoLock(string sql)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    private DateTimeOffset? ScalarDateNoLock(string sql)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        var value = cmd.ExecuteScalar();
        return value is null or DBNull ? null : ParseDate(Convert.ToString(value)!);
    }

    private static DateTimeOffset ParseDate(string value) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.MinValue;

    public void Dispose()
    {
        lock (_gate) _conn.Dispose();
    }
}
