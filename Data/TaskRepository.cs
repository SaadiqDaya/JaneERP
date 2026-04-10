using System.Configuration;
using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace JaneERP
{
    public class ErpTask
    {
        public int      TaskID      { get; set; }
        public string   Title       { get; set; } = "";
        public string?  Description { get; set; }
        public string   AssignedTo  { get; set; } = "";
        public string   CreatedBy   { get; set; } = "";
        public DateTime DueDate     { get; set; }
        public string   Status      { get; set; } = "Open"; // Open, InProgress, Done
        public DateTime CreatedAt   { get; set; }
    }

    public class TaskComment
    {
        public int      CommentID  { get; set; }
        public int      TaskID     { get; set; }
        public string   Username   { get; set; } = "";
        public string   Body       { get; set; } = "";
        public DateTime CreatedAt  { get; set; }
    }

    public class TaskMention
    {
        public int      MentionID     { get; set; }
        public int      TaskID        { get; set; }
        public string   MentionedUser { get; set; } = "";
        public string   MentionedBy   { get; set; } = "";
        public string   CommentText   { get; set; } = "";
        public DateTime MentionedAt   { get; set; }
        public bool     IsRead        { get; set; }
        // Task info joined for display
        public string   TaskTitle     { get; set; } = "";
    }

    public class TaskRepository
    {
        private readonly string _cs =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found.");

        public void EnsureSchema()
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='Tasks' AND xtype='U')
                CREATE TABLE Tasks (
                    TaskID      INT           IDENTITY(1,1) PRIMARY KEY,
                    Title       NVARCHAR(200) NOT NULL,
                    Description NVARCHAR(2000) NULL,
                    AssignedTo  NVARCHAR(100) NOT NULL,
                    CreatedBy   NVARCHAR(100) NOT NULL,
                    DueDate     DATETIME      NOT NULL,
                    Status      NVARCHAR(50)  NOT NULL DEFAULT 'Open',
                    CreatedAt   DATETIME      NOT NULL DEFAULT GETDATE()
                );

                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='TaskComments' AND xtype='U')
                CREATE TABLE TaskComments (
                    CommentID  INT            IDENTITY(1,1) PRIMARY KEY,
                    TaskID     INT            NOT NULL REFERENCES Tasks(TaskID) ON DELETE CASCADE,
                    Username   NVARCHAR(100)  NOT NULL,
                    Body       NVARCHAR(2000) NOT NULL,
                    CreatedAt  DATETIME       NOT NULL DEFAULT GETDATE()
                );

                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='TaskMentions' AND xtype='U')
                CREATE TABLE TaskMentions (
                    MentionID     INT            IDENTITY(1,1) PRIMARY KEY,
                    TaskID        INT            NOT NULL REFERENCES Tasks(TaskID) ON DELETE CASCADE,
                    MentionedUser NVARCHAR(100)  NOT NULL,
                    MentionedBy   NVARCHAR(100)  NOT NULL,
                    CommentText   NVARCHAR(MAX)  NOT NULL,
                    MentionedAt   DATETIME       NOT NULL DEFAULT GETDATE(),
                    IsRead        BIT            NOT NULL DEFAULT 0
                );");
        }

        public List<ErpTask> GetAll(string? assignedTo = null)
        {
            using IDbConnection db = new SqlConnection(_cs);
            var sql = assignedTo == null
                ? "SELECT * FROM Tasks ORDER BY DueDate"
                : "SELECT * FROM Tasks WHERE AssignedTo = @assignedTo ORDER BY DueDate";
            return db.Query<ErpTask>(sql, new { assignedTo }).ToList();
        }

        public ErpTask? GetById(int taskId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.QueryFirstOrDefault<ErpTask>("SELECT * FROM Tasks WHERE TaskID = @taskId", new { taskId });
        }

        public List<ErpTask> GetOutstanding(string? assignedTo = null)
        {
            using IDbConnection db = new SqlConnection(_cs);
            var where = assignedTo == null
                ? "WHERE Status <> 'Done'"
                : "WHERE Status <> 'Done' AND AssignedTo = @assignedTo";
            return db.Query<ErpTask>($"SELECT * FROM Tasks {where} ORDER BY DueDate", new { assignedTo }).ToList();
        }

        public int Add(ErpTask task)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.QuerySingle<int>(@"
                INSERT INTO Tasks (Title, Description, AssignedTo, CreatedBy, DueDate, Status)
                VALUES (@Title, @Description, @AssignedTo, @CreatedBy, @DueDate, @Status);
                SELECT CAST(SCOPE_IDENTITY() AS INT);", task);
        }

        public void UpdateStatus(int taskId, string status)
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Execute("UPDATE Tasks SET Status = @status WHERE TaskID = @taskId", new { taskId, status });
        }

        public void Delete(int taskId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Execute("DELETE FROM Tasks WHERE TaskID = @taskId", new { taskId });
        }

        public List<string> GetAllUsernames()
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query<string>("SELECT Username FROM Users ORDER BY Username").ToList();
        }

        // ── Comments ─────────────────────────────────────────────────────────────

        public List<TaskComment> GetComments(int taskId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query<TaskComment>(
                "SELECT * FROM TaskComments WHERE TaskID = @taskId ORDER BY CreatedAt",
                new { taskId }).ToList();
        }

        public void AddComment(int taskId, string username, string body)
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Execute(
                "INSERT INTO TaskComments (TaskID, Username, Body) VALUES (@taskId, @username, @body)",
                new { taskId, username, body });
        }

        // ── Mentions ──────────────────────────────────────────────────────────────

        public void AddMention(int taskId, string mentionedUser, string mentionedBy, string commentText)
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Execute(@"
                INSERT INTO TaskMentions (TaskID, MentionedUser, MentionedBy, CommentText)
                VALUES (@taskId, @mentionedUser, @mentionedBy, @commentText)",
                new { taskId, mentionedUser, mentionedBy, commentText });
        }

        public List<TaskMention> GetMentions(string forUser, bool unreadOnly = true)
        {
            using IDbConnection db = new SqlConnection(_cs);
            var where = unreadOnly
                ? "WHERE m.MentionedUser = @forUser AND m.IsRead = 0"
                : "WHERE m.MentionedUser = @forUser";
            return db.Query<TaskMention>($@"
                SELECT m.MentionID, m.TaskID, m.MentionedUser, m.MentionedBy,
                       m.CommentText, m.MentionedAt, m.IsRead,
                       t.Title AS TaskTitle
                FROM TaskMentions m
                JOIN Tasks t ON t.TaskID = m.TaskID
                {where}
                ORDER BY m.MentionedAt DESC",
                new { forUser }).ToList();
        }

        public void MarkMentionRead(int mentionId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Execute("UPDATE TaskMentions SET IsRead = 1 WHERE MentionID = @mentionId", new { mentionId });
        }

        public void MarkAllMentionsRead(string forUser)
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Execute("UPDATE TaskMentions SET IsRead = 1 WHERE MentionedUser = @forUser AND IsRead = 0",
                new { forUser });
        }

        /// <summary>Returns (username, email) for the given list of usernames.</summary>
        public List<(string Username, string Email)> GetUserEmails(IEnumerable<string> usernames)
        {
            using IDbConnection db = new SqlConnection(_cs);
            var list = usernames.ToList();
            // Use a concrete type so Dapper can map columns correctly
            var rows = db.Query<UserEmailRow>(
                "SELECT Username, Email FROM Users WHERE Username IN @usernames",
                new { usernames = list }).ToList();
            return rows.Select(r => (r.Username, r.Email ?? "")).ToList();
        }

        private class UserEmailRow
        {
            public string  Username { get; set; } = "";
            public string? Email    { get; set; }
        }
    }
}
