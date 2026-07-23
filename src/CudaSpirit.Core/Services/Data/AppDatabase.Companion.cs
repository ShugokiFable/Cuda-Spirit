using CudaSpirit.Core.Models;

namespace CudaSpirit.Core.Services.Data;

public sealed partial class AppDatabase
{
    public long UpsertTask(CompanionTask task)
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = task.Id == 0
                ? """
                  INSERT INTO companion_task(title,detail,category,cadence,priority,due_at,status,pinned,source_url,metadata_json,created_at,completed_at)
                  VALUES($title,$detail,$category,$cadence,$priority,$due,$status,$pinned,$url,$meta,$created,$completed);
                  SELECT last_insert_rowid();
                  """
                : """
                  UPDATE companion_task SET title=$title,detail=$detail,category=$category,cadence=$cadence,
                      priority=$priority,due_at=$due,status=$status,pinned=$pinned,source_url=$url,
                      metadata_json=$meta,completed_at=$completed WHERE id=$id;
                  SELECT $id;
                  """;
            if (task.Id != 0) cmd.Parameters.AddWithValue("$id", task.Id);
            cmd.Parameters.AddWithValue("$title", task.Title);
            cmd.Parameters.AddWithValue("$detail", task.Detail);
            cmd.Parameters.AddWithValue("$category", task.Category);
            cmd.Parameters.AddWithValue("$cadence", task.Cadence);
            cmd.Parameters.AddWithValue("$priority", Math.Clamp(task.Priority, 0, 100));
            cmd.Parameters.AddWithValue("$due", (object?)task.DueAt?.ToString("O") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$status", task.Status);
            cmd.Parameters.AddWithValue("$pinned", task.Pinned ? 1 : 0);
            cmd.Parameters.AddWithValue("$url", task.SourceUrl);
            cmd.Parameters.AddWithValue("$meta", task.MetadataJson);
            cmd.Parameters.AddWithValue("$created", task.CreatedAt.ToString("O"));
            cmd.Parameters.AddWithValue("$completed", (object?)task.CompletedAt?.ToString("O") ?? DBNull.Value);
            return Convert.ToInt64(cmd.ExecuteScalar() ?? 0L);
        }
    }

    public IReadOnlyList<CompanionTask> GetTasks(bool includeCompleted = false, int limit = 100)
    {
        lock (_gate)
        {
            var list = new List<CompanionTask>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                SELECT id,title,detail,category,cadence,priority,due_at,status,pinned,source_url,metadata_json,created_at,completed_at
                FROM companion_task
                WHERE ($all=1 OR status<>'done')
                ORDER BY pinned DESC, CASE WHEN due_at IS NULL THEN 1 ELSE 0 END, due_at, priority DESC, id DESC
                LIMIT $limit;
                """;
            cmd.Parameters.AddWithValue("$all", includeCompleted ? 1 : 0);
            cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 500));
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new CompanionTask
                {
                    Id = r.GetInt64(0), Title = r.GetString(1), Detail = r.GetString(2), Category = r.GetString(3),
                    Cadence = r.GetString(4), Priority = r.GetInt32(5), DueAt = r.IsDBNull(6) ? null : ParseDate(r.GetString(6)),
                    Status = r.GetString(7), Pinned = r.GetInt32(8) != 0, SourceUrl = r.GetString(9), MetadataJson = r.GetString(10),
                    CreatedAt = ParseDate(r.GetString(11)), CompletedAt = r.IsDBNull(12) ? null : ParseDate(r.GetString(12))
                });
            }
            return list;
        }
    }

    public void SetTaskStatus(long id, string status)
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE companion_task SET status=$status, completed_at=$completed WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$status", status);
            cmd.Parameters.AddWithValue("$completed", status == "done" ? DateTimeOffset.UtcNow.ToString("O") : DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    public void DeleteTask(long id)
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM companion_task WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
    }

    public void AddItemDecision(ItemDecisionHistory item)
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT INTO item_decision_history(item_name,verdict,reason,binding,created_at) VALUES($name,$verdict,$reason,$binding,$created)";
            cmd.Parameters.AddWithValue("$name", item.ItemName);
            cmd.Parameters.AddWithValue("$verdict", item.Verdict);
            cmd.Parameters.AddWithValue("$reason", item.Reason);
            cmd.Parameters.AddWithValue("$binding", item.Binding);
            cmd.Parameters.AddWithValue("$created", item.CreatedAt.ToString("O"));
            cmd.ExecuteNonQuery();
        }
    }

    public IReadOnlyList<ItemDecisionHistory> GetItemDecisionHistory(int limit = 30)
    {
        lock (_gate)
        {
            var list = new List<ItemDecisionHistory>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id,item_name,verdict,reason,binding,created_at FROM item_decision_history ORDER BY id DESC LIMIT $limit";
            cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 200));
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(new ItemDecisionHistory
            {
                Id = r.GetInt64(0), ItemName = r.GetString(1), Verdict = r.GetString(2), Reason = r.GetString(3),
                Binding = r.GetString(4), CreatedAt = ParseDate(r.GetString(5))
            });
            return list;
        }
    }

    public void AddPearlEvaluation(PearlEvaluationHistory item)
    {
        lock (_gate)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO pearl_evaluation_history(offer_name,price_pearls,original_price_pearls,score,verdict,notes,evaluated_at)
                VALUES($name,$price,$original,$score,$verdict,$notes,$at)
                """;
            cmd.Parameters.AddWithValue("$name", item.OfferName);
            cmd.Parameters.AddWithValue("$price", item.PricePearls);
            cmd.Parameters.AddWithValue("$original", item.OriginalPricePearls);
            cmd.Parameters.AddWithValue("$score", item.Score);
            cmd.Parameters.AddWithValue("$verdict", item.Verdict);
            cmd.Parameters.AddWithValue("$notes", item.Notes);
            cmd.Parameters.AddWithValue("$at", item.EvaluatedAt.ToString("O"));
            cmd.ExecuteNonQuery();
        }
    }

    public IReadOnlyList<PearlEvaluationHistory> GetPearlEvaluationHistory(int limit = 30)
    {
        lock (_gate)
        {
            var list = new List<PearlEvaluationHistory>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id,offer_name,price_pearls,original_price_pearls,score,verdict,notes,evaluated_at FROM pearl_evaluation_history ORDER BY id DESC LIMIT $limit";
            cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 200));
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(new PearlEvaluationHistory
            {
                Id = r.GetInt64(0), OfferName = r.GetString(1), PricePearls = r.GetInt32(2), OriginalPricePearls = r.GetInt32(3),
                Score = r.GetInt32(4), Verdict = r.GetString(5), Notes = r.GetString(6), EvaluatedAt = ParseDate(r.GetString(7))
            });
            return list;
        }
    }
}
