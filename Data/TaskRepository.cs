using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Interfaces;
using Microsoft.Data.SqlClient;

namespace JaneERP
{
    public class TaskWorkflow
    {
        public int    WorkflowID { get; set; }
        public string Name       { get; set; } = "";
    }

    public class WorkflowStatus
    {
        public int    StatusID    { get; set; }
        public int    WorkflowID  { get; set; }
        public string StatusName  { get; set; } = "";
        public int    SortOrder   { get; set; }
    }

    public class ErpTask
    {
        public int      TaskID                { get; set; }
        public string   Title                 { get; set; } = "";
        public string?  Description           { get; set; }
        public string   AssignedTo            { get; set; } = "";
        public string   CreatedBy             { get; set; } = "";
        public DateTime DueDate               { get; set; }
        public string   Status                { get; set; } = "Open"; // Open, In Progress, Done
        public string   Priority              { get; set; } = "Normal"; // Low, Normal, High, Urgent
        public int?     WorkflowID            { get; set; }
        public string?  WorkflowCurrentStatus { get; set; }
        public string?  WorkflowName          { get; set; }
        public DateTime CreatedAt             { get; set; }

        /// <summary>
        /// Resolved stage for display: shows WorkflowCurrentStatus when a workflow is active,
        /// otherwise falls back to the legacy Status value.
        /// </summary>
        public string StageDisplay => !string.IsNullOrWhiteSpace(WorkflowCurrentStatus)
            ? WorkflowCurrentStatus
            : Status;
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

    public class TaskRepository : ITaskRepository
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
                );

                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='TaskWorkflows' AND xtype='U')
                CREATE TABLE TaskWorkflows (
                    WorkflowID INT           IDENTITY(1,1) PRIMARY KEY,
                    Name       NVARCHAR(100) NOT NULL
                );

                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='TaskWorkflowStatuses' AND xtype='U')
                CREATE TABLE TaskWorkflowStatuses (
                    StatusID   INT           IDENTITY(1,1) PRIMARY KEY,
                    WorkflowID INT           NOT NULL REFERENCES TaskWorkflows(WorkflowID) ON DELETE CASCADE,
                    StatusName NVARCHAR(100) NOT NULL,
                    SortOrder  INT           NOT NULL DEFAULT 0
                );

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tasks') AND name = 'WorkflowID')
                    ALTER TABLE Tasks ADD WorkflowID INT NULL REFERENCES TaskWorkflows(WorkflowID);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tasks') AND name = 'WorkflowCurrentStatus')
                    ALTER TABLE Tasks ADD WorkflowCurrentStatus NVARCHAR(100) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tasks') AND name = 'Priority')
                    ALTER TABLE Tasks ADD Priority NVARCHAR(50) NOT NULL DEFAULT 'Normal';");
        }

        public List<ErpTask> GetAll(string? assignedTo = null, string? status = null)
        {
            using IDbConnection db = new SqlConnection(_cs);
            var conditions = new List<string>();
            if (assignedTo != null) conditions.Add("t.AssignedTo = @assignedTo");
            if (status != null)     conditions.Add("t.Status = @status");
            var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
            return db.Query<ErpTask>($@"
                SELECT t.*, wf.Name AS WorkflowName
                FROM   Tasks t
                LEFT JOIN TaskWorkflows wf ON wf.WorkflowID = t.WorkflowID
                {where}
                ORDER BY t.DueDate",
                new { assignedTo, status }).ToList();
        }

        public ErpTask? GetById(int taskId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.QueryFirstOrDefault<ErpTask>(@"
                SELECT t.*, wf.Name AS WorkflowName
                FROM   Tasks t
                LEFT JOIN TaskWorkflows wf ON wf.WorkflowID = t.WorkflowID
                WHERE  t.TaskID = @taskId",
                new { taskId });
        }

        public List<ErpTask> GetOutstanding(string? assignedTo = null)
        {
            using IDbConnection db = new SqlConnection(_cs);
            var where = assignedTo == null
                ? "WHERE t.Status <> 'Done'"
                : "WHERE t.Status <> 'Done' AND t.AssignedTo = @assignedTo";
            return db.Query<ErpTask>($@"
                SELECT t.*, wf.Name AS WorkflowName
                FROM   Tasks t
                LEFT JOIN TaskWorkflows wf ON wf.WorkflowID = t.WorkflowID
                {where}
                ORDER BY t.DueDate",
                new { assignedTo }).ToList();
        }

        public int Add(ErpTask task)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.QuerySingle<int>(@"
                INSERT INTO Tasks (Title, Description, AssignedTo, CreatedBy, DueDate, Status, Priority)
                VALUES (@Title, @Description, @AssignedTo, @CreatedBy, @DueDate, @Status, @Priority);
                SELECT CAST(SCOPE_IDENTITY() AS INT);", task);
        }

        public void UpdateStatus(int taskId, string status)
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Execute("UPDATE Tasks SET Status = @status WHERE TaskID = @taskId", new { taskId, status });
        }

        public void UpdateDueDate(int taskId, DateTime dueDate)
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Execute("UPDATE Tasks SET DueDate = @dueDate WHERE TaskID = @taskId", new { taskId, dueDate });
        }

        public void UpdateDescription(int taskId, string description)
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Execute("UPDATE Tasks SET Description = @description WHERE TaskID = @taskId", new { taskId, description });
        }

        public void UpdateAssignedTo(int taskId, string assignedTo)
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Execute("UPDATE Tasks SET AssignedTo = @assignedTo WHERE TaskID = @taskId", new { taskId, assignedTo });
        }

        public void UpdatePriority(int taskId, string priority)
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Execute("UPDATE Tasks SET Priority = @priority WHERE TaskID = @taskId", new { taskId, priority });
        }

        public void UpdateWorkflowStatus(int taskId, int? workflowId, string? workflowStatus)
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Execute(@"UPDATE Tasks SET WorkflowID = @workflowId, WorkflowCurrentStatus = @workflowStatus
                         WHERE TaskID = @taskId", new { taskId, workflowId, workflowStatus });
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

        // ── Workflows ─────────────────────────────────────────────────────────────

        public List<TaskWorkflow> GetWorkflows()
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query<TaskWorkflow>("SELECT * FROM TaskWorkflows ORDER BY Name").ToList();
        }

        public int AddWorkflow(string name)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.QuerySingle<int>(@"
                INSERT INTO TaskWorkflows (Name) VALUES (@name);
                SELECT CAST(SCOPE_IDENTITY() AS INT);", new { name });
        }

        public void DeleteWorkflow(int workflowId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Execute("DELETE FROM TaskWorkflows WHERE WorkflowID = @workflowId", new { workflowId });
        }

        public List<WorkflowStatus> GetWorkflowStatuses(int workflowId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query<WorkflowStatus>(
                "SELECT * FROM TaskWorkflowStatuses WHERE WorkflowID = @workflowId ORDER BY SortOrder",
                new { workflowId }).ToList();
        }

        public int AddWorkflowStatus(int workflowId, string statusName)
        {
            using IDbConnection db = new SqlConnection(_cs);
            int maxSort = db.ExecuteScalar<int>(
                "SELECT ISNULL(MAX(SortOrder), -1) FROM TaskWorkflowStatuses WHERE WorkflowID = @workflowId",
                new { workflowId });
            return db.QuerySingle<int>(@"
                INSERT INTO TaskWorkflowStatuses (WorkflowID, StatusName, SortOrder)
                VALUES (@workflowId, @statusName, @sortOrder);
                SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new { workflowId, statusName, sortOrder = maxSort + 1 });
        }

        public void DeleteWorkflowStatus(int statusId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Execute("DELETE FROM TaskWorkflowStatuses WHERE StatusID = @statusId", new { statusId });
        }

        public void MoveWorkflowStatus(int statusId, bool moveUp)
        {
            using var db = new SqlConnection(_cs);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                var current = db.QueryFirstOrDefault<WorkflowStatus>(
                    "SELECT * FROM TaskWorkflowStatuses WHERE StatusID = @statusId",
                    new { statusId }, tx);
                if (current == null) return;

                var neighbour = moveUp
                    ? db.QueryFirstOrDefault<WorkflowStatus>(
                        "SELECT TOP 1 * FROM TaskWorkflowStatuses WHERE WorkflowID = @wid AND SortOrder < @sort ORDER BY SortOrder DESC",
                        new { wid = current.WorkflowID, sort = current.SortOrder }, tx)
                    : db.QueryFirstOrDefault<WorkflowStatus>(
                        "SELECT TOP 1 * FROM TaskWorkflowStatuses WHERE WorkflowID = @wid AND SortOrder > @sort ORDER BY SortOrder ASC",
                        new { wid = current.WorkflowID, sort = current.SortOrder }, tx);

                if (neighbour == null) return;

                db.Execute("UPDATE TaskWorkflowStatuses SET SortOrder = @s WHERE StatusID = @id",
                    new { s = neighbour.SortOrder, id = current.StatusID }, tx);
                db.Execute("UPDATE TaskWorkflowStatuses SET SortOrder = @s WHERE StatusID = @id",
                    new { s = current.SortOrder, id = neighbour.StatusID }, tx);
                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        public void SetTaskWorkflow(int taskId, int? workflowId, string? initialStatus)
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Execute(@"
                UPDATE Tasks
                SET WorkflowID = @workflowId, WorkflowCurrentStatus = @initialStatus
                WHERE TaskID = @taskId",
                new { taskId, workflowId, initialStatus });
        }

        public List<string> GetWorkflowStatusNames(int workflowId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query<string>(
                "SELECT StatusName FROM TaskWorkflowStatuses WHERE WorkflowID = @workflowId ORDER BY SortOrder",
                new { workflowId }).ToList();
        }
    }
}
