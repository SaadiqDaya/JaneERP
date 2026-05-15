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
        // Recurrence columns
        public string?  RecurrencePattern     { get; set; }  // NULL, 'Daily', 'Weekly', 'Monthly'
        public int      RecurrenceInterval    { get; set; } = 1;
        public DateTime? NextOccurrence       { get; set; }
        public int?     ParentTaskId          { get; set; }
        // Tags
        public string?  Tags                  { get; set; }

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

    public class TaskLinkedRecord
    {
        public int      LinkId        { get; set; }
        public int      TaskId        { get; set; }
        public string   LinkedModule  { get; set; } = "";
        public string   LinkedId      { get; set; } = "";
        public string   LinkedDisplay { get; set; } = "";
        public DateTime CreatedAt     { get; set; }
    }

    public class TaskHistoryEntry
    {
        public int      HistoryId  { get; set; }
        public int      TaskId     { get; set; }
        public string   FieldName  { get; set; } = "";
        public string?  OldValue   { get; set; }
        public string?  NewValue   { get; set; }
        public string   ChangedBy  { get; set; } = "";
        public DateTime ChangedAt  { get; set; }
    }

    public class TaskSubtask
    {
        public int      SubtaskId   { get; set; }
        public int      TaskId      { get; set; }
        public string   Title       { get; set; } = "";
        public bool     IsComplete  { get; set; }
        public string?  CompletedBy { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int      SortOrder   { get; set; }
    }

    public class TaskTemplate
    {
        public int      TemplateId  { get; set; }
        public string   Name        { get; set; } = "";
        public string   Description { get; set; } = "";
        public string   CreatedBy   { get; set; } = "";
        public List<TaskTemplateItem> Items { get; set; } = new();
    }

    public class TaskTemplateItem
    {
        public int    ItemId        { get; set; }
        public int    TemplateId    { get; set; }
        public string Title         { get; set; } = "";
        public string Description   { get; set; } = "";
        public string Priority      { get; set; } = "Normal";
        public int    DueDaysOffset { get; set; }
        public int    SortOrder     { get; set; }
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

                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='TaskLinkedRecords' AND xtype='U')
                CREATE TABLE TaskLinkedRecords (
                    LinkId         INT            IDENTITY(1,1) PRIMARY KEY,
                    TaskId         INT            NOT NULL REFERENCES Tasks(TaskID) ON DELETE CASCADE,
                    LinkedModule   NVARCHAR(50)   NOT NULL,
                    LinkedId       NVARCHAR(100)  NOT NULL,
                    LinkedDisplay  NVARCHAR(200)  NULL,
                    CreatedAt      DATETIME       NOT NULL DEFAULT GETDATE()
                );

                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='TaskHistory' AND xtype='U')
                CREATE TABLE TaskHistory (
                    HistoryId  INT            IDENTITY(1,1) PRIMARY KEY,
                    TaskId     INT            NOT NULL REFERENCES Tasks(TaskID) ON DELETE CASCADE,
                    FieldName  NVARCHAR(100)  NOT NULL,
                    OldValue   NVARCHAR(500)  NULL,
                    NewValue   NVARCHAR(500)  NULL,
                    ChangedBy  NVARCHAR(100)  NOT NULL,
                    ChangedAt  DATETIME       NOT NULL DEFAULT GETDATE()
                );

                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='TaskSubtasks' AND xtype='U')
                CREATE TABLE TaskSubtasks (
                    SubtaskId   INT            IDENTITY(1,1) PRIMARY KEY,
                    TaskId      INT            NOT NULL REFERENCES Tasks(TaskID) ON DELETE CASCADE,
                    Title       NVARCHAR(300)  NOT NULL,
                    IsComplete  BIT            NOT NULL DEFAULT 0,
                    CompletedBy NVARCHAR(100)  NULL,
                    CompletedAt DATETIME       NULL,
                    SortOrder   INT            NOT NULL DEFAULT 0
                );

                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='TaskTemplates' AND xtype='U')
                CREATE TABLE TaskTemplates (
                    TemplateId  INT            IDENTITY(1,1) PRIMARY KEY,
                    Name        NVARCHAR(200)  NOT NULL,
                    Description NVARCHAR(500)  NULL,
                    CreatedBy   NVARCHAR(100)  NULL,
                    CreatedAt   DATETIME       NOT NULL DEFAULT GETDATE()
                );

                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='TaskTemplateItems' AND xtype='U')
                CREATE TABLE TaskTemplateItems (
                    ItemId         INT            IDENTITY(1,1) PRIMARY KEY,
                    TemplateId     INT            NOT NULL REFERENCES TaskTemplates(TemplateId) ON DELETE CASCADE,
                    Title          NVARCHAR(200)  NOT NULL,
                    Description    NVARCHAR(1000) NULL,
                    Priority       NVARCHAR(50)   NOT NULL DEFAULT 'Normal',
                    DueDaysOffset  INT            NOT NULL DEFAULT 0,
                    SortOrder      INT            NOT NULL DEFAULT 0
                );

                -- Tasks column migrations
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tasks') AND name = 'WorkflowID')
                    ALTER TABLE Tasks ADD WorkflowID INT NULL REFERENCES TaskWorkflows(WorkflowID);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tasks') AND name = 'WorkflowCurrentStatus')
                    ALTER TABLE Tasks ADD WorkflowCurrentStatus NVARCHAR(100) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tasks') AND name = 'Priority')
                    ALTER TABLE Tasks ADD Priority NVARCHAR(50) NOT NULL DEFAULT 'Normal';
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tasks') AND name = 'RecurrencePattern')
                    ALTER TABLE Tasks ADD RecurrencePattern NVARCHAR(50) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tasks') AND name = 'RecurrenceInterval')
                    ALTER TABLE Tasks ADD RecurrenceInterval INT NOT NULL DEFAULT 1;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tasks') AND name = 'NextOccurrence')
                    ALTER TABLE Tasks ADD NextOccurrence DATETIME NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tasks') AND name = 'ParentTaskId')
                    ALTER TABLE Tasks ADD ParentTaskId INT NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tasks') AND name = 'Tags')
                    ALTER TABLE Tasks ADD Tags NVARCHAR(500) NULL;");
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

        public void UpdateStatus(int taskId, string status, string changedBy = "")
        {
            using IDbConnection db = new SqlConnection(_cs);
            if (!string.IsNullOrEmpty(changedBy))
            {
                var old = db.ExecuteScalar<string>("SELECT Status FROM Tasks WHERE TaskID = @taskId", new { taskId });
                db.Execute("UPDATE Tasks SET Status = @status WHERE TaskID = @taskId", new { taskId, status });
                LogHistoryInternal(db, taskId, "Status", old, status, changedBy);
            }
            else
            {
                db.Execute("UPDATE Tasks SET Status = @status WHERE TaskID = @taskId", new { taskId, status });
            }
        }

        public void UpdateDueDate(int taskId, DateTime dueDate, string changedBy = "")
        {
            using IDbConnection db = new SqlConnection(_cs);
            if (!string.IsNullOrEmpty(changedBy))
            {
                var old = db.ExecuteScalar<DateTime?>("SELECT DueDate FROM Tasks WHERE TaskID = @taskId", new { taskId });
                db.Execute("UPDATE Tasks SET DueDate = @dueDate WHERE TaskID = @taskId", new { taskId, dueDate });
                LogHistoryInternal(db, taskId, "DueDate", old?.ToString("yyyy-MM-dd HH:mm"), dueDate.ToString("yyyy-MM-dd HH:mm"), changedBy);
            }
            else
            {
                db.Execute("UPDATE Tasks SET DueDate = @dueDate WHERE TaskID = @taskId", new { taskId, dueDate });
            }
        }

        public void UpdateDescription(int taskId, string description, string changedBy = "")
        {
            using IDbConnection db = new SqlConnection(_cs);
            if (!string.IsNullOrEmpty(changedBy))
            {
                var old = db.ExecuteScalar<string>("SELECT Description FROM Tasks WHERE TaskID = @taskId", new { taskId });
                db.Execute("UPDATE Tasks SET Description = @description WHERE TaskID = @taskId", new { taskId, description });
                LogHistoryInternal(db, taskId, "Description", old, description, changedBy);
            }
            else
            {
                db.Execute("UPDATE Tasks SET Description = @description WHERE TaskID = @taskId", new { taskId, description });
            }
        }

        public void UpdateAssignedTo(int taskId, string assignedTo, string changedBy = "")
        {
            using IDbConnection db = new SqlConnection(_cs);
            if (!string.IsNullOrEmpty(changedBy))
            {
                var old = db.ExecuteScalar<string>("SELECT AssignedTo FROM Tasks WHERE TaskID = @taskId", new { taskId });
                db.Execute("UPDATE Tasks SET AssignedTo = @assignedTo WHERE TaskID = @taskId", new { taskId, assignedTo });
                LogHistoryInternal(db, taskId, "AssignedTo", old, assignedTo, changedBy);
            }
            else
            {
                db.Execute("UPDATE Tasks SET AssignedTo = @assignedTo WHERE TaskID = @taskId", new { taskId, assignedTo });
            }
        }

        public void UpdatePriority(int taskId, string priority, string changedBy = "")
        {
            using IDbConnection db = new SqlConnection(_cs);
            if (!string.IsNullOrEmpty(changedBy))
            {
                var old = db.ExecuteScalar<string>("SELECT Priority FROM Tasks WHERE TaskID = @taskId", new { taskId });
                db.Execute("UPDATE Tasks SET Priority = @priority WHERE TaskID = @taskId", new { taskId, priority });
                LogHistoryInternal(db, taskId, "Priority", old, priority, changedBy);
            }
            else
            {
                db.Execute("UPDATE Tasks SET Priority = @priority WHERE TaskID = @taskId", new { taskId, priority });
            }
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

        // ── Linked Records ────────────────────────────────────────────────────────

        public TaskLinkedRecord? GetLinkedRecord(int taskId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.QueryFirstOrDefault<TaskLinkedRecord>(
                "SELECT * FROM TaskLinkedRecords WHERE TaskId = @taskId",
                new { taskId });
        }

        public bool SetLinkedRecord(int taskId, string module, string linkedId, string displayLabel)
        {
            using IDbConnection db = new SqlConnection(_cs);
            // Upsert: delete existing then insert
            db.Execute("DELETE FROM TaskLinkedRecords WHERE TaskId = @taskId", new { taskId });
            int rows = db.Execute(@"
                INSERT INTO TaskLinkedRecords (TaskId, LinkedModule, LinkedId, LinkedDisplay)
                VALUES (@taskId, @module, @linkedId, @displayLabel)",
                new { taskId, module, linkedId, displayLabel });
            return rows > 0;
        }

        public bool ClearLinkedRecord(int taskId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            int rows = db.Execute("DELETE FROM TaskLinkedRecords WHERE TaskId = @taskId", new { taskId });
            return rows > 0;
        }

        public List<ErpTask> GetTasksByLinkedRecord(string module, string linkedId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query<ErpTask>(@"
                SELECT t.*, wf.Name AS WorkflowName
                FROM   Tasks t
                JOIN   TaskLinkedRecords lr ON lr.TaskId = t.TaskID
                LEFT JOIN TaskWorkflows wf  ON wf.WorkflowID = t.WorkflowID
                WHERE  lr.LinkedModule = @module AND lr.LinkedId = @linkedId
                ORDER BY t.DueDate",
                new { module, linkedId }).ToList();
        }

        // ── History / Audit Trail ─────────────────────────────────────────────────

        public List<TaskHistoryEntry> GetHistory(int taskId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query<TaskHistoryEntry>(
                "SELECT * FROM TaskHistory WHERE TaskId = @taskId ORDER BY ChangedAt",
                new { taskId }).ToList();
        }

        public bool LogHistory(int taskId, string fieldName, string? oldValue, string? newValue, string changedBy)
        {
            using IDbConnection db = new SqlConnection(_cs);
            int rows = db.Execute(@"
                INSERT INTO TaskHistory (TaskId, FieldName, OldValue, NewValue, ChangedBy)
                VALUES (@taskId, @fieldName, @oldValue, @newValue, @changedBy)",
                new { taskId, fieldName, oldValue, newValue, changedBy });
            return rows > 0;
        }

        // Internal helper — called from Update* methods that already hold an open connection
        private static void LogHistoryInternal(IDbConnection db, int taskId, string fieldName,
            string? oldValue, string? newValue, string changedBy)
        {
            db.Execute(@"
                INSERT INTO TaskHistory (TaskId, FieldName, OldValue, NewValue, ChangedBy)
                VALUES (@taskId, @fieldName, @oldValue, @newValue, @changedBy)",
                new { taskId, fieldName, oldValue, newValue, changedBy });
        }

        // ── Subtasks ──────────────────────────────────────────────────────────────

        public List<TaskSubtask> GetSubtasks(int taskId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query<TaskSubtask>(
                "SELECT * FROM TaskSubtasks WHERE TaskId = @taskId ORDER BY SortOrder",
                new { taskId }).ToList();
        }

        public bool AddSubtask(int taskId, string title, int sortOrder = 0)
        {
            using IDbConnection db = new SqlConnection(_cs);
            int rows = db.Execute(@"
                INSERT INTO TaskSubtasks (TaskId, Title, SortOrder)
                VALUES (@taskId, @title, @sortOrder)",
                new { taskId, title, sortOrder });
            return rows > 0;
        }

        public bool CompleteSubtask(int subtaskId, string completedBy)
        {
            using IDbConnection db = new SqlConnection(_cs);
            int rows = db.Execute(@"
                UPDATE TaskSubtasks
                SET IsComplete = 1, CompletedBy = @completedBy, CompletedAt = GETDATE()
                WHERE SubtaskId = @subtaskId",
                new { subtaskId, completedBy });
            return rows > 0;
        }

        public bool DeleteSubtask(int subtaskId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            int rows = db.Execute("DELETE FROM TaskSubtasks WHERE SubtaskId = @subtaskId", new { subtaskId });
            return rows > 0;
        }

        // ── Workload ──────────────────────────────────────────────────────────────

        public Dictionary<string, int> GetWorkloadSummary()
        {
            using IDbConnection db = new SqlConnection(_cs);
            var rows = db.Query<WorkloadRow>(
                "SELECT AssignedTo, COUNT(*) AS TaskCount FROM Tasks WHERE Status <> 'Done' GROUP BY AssignedTo");
            return rows.ToDictionary(r => r.AssignedTo, r => r.TaskCount);
        }

        private class WorkloadRow
        {
            public string AssignedTo { get; set; } = "";
            public int    TaskCount  { get; set; }
        }

        // ── Overdue ───────────────────────────────────────────────────────────────

        public List<ErpTask> GetOverdueTasks()
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query<ErpTask>(@"
                SELECT t.*, wf.Name AS WorkflowName
                FROM   Tasks t
                LEFT JOIN TaskWorkflows wf ON wf.WorkflowID = t.WorkflowID
                WHERE  t.DueDate < GETDATE()
                  AND  t.Status <> 'Done'
                  AND  t.Status <> 'Cancelled'
                ORDER BY t.DueDate").ToList();
        }

        // ── Tags ──────────────────────────────────────────────────────────────────

        public bool UpdateTags(int taskId, string tags)
        {
            using IDbConnection db = new SqlConnection(_cs);
            int rows = db.Execute("UPDATE Tasks SET Tags = @tags WHERE TaskID = @taskId", new { taskId, tags });
            return rows > 0;
        }

        // ── Templates ────────────────────────────────────────────────────────────

        public List<TaskTemplate> GetTemplates()
        {
            using IDbConnection db = new SqlConnection(_cs);
            var templates = db.Query<TaskTemplate>("SELECT * FROM TaskTemplates ORDER BY Name").ToList();
            if (templates.Count == 0) return templates;

            var templateIds = templates.Select(t => t.TemplateId).ToList();
            var items = db.Query<TaskTemplateItem>(
                "SELECT * FROM TaskTemplateItems WHERE TemplateId IN @templateIds ORDER BY SortOrder",
                new { templateIds }).ToList();

            foreach (var tmpl in templates)
                tmpl.Items = items.Where(i => i.TemplateId == tmpl.TemplateId).ToList();

            return templates;
        }

        public TaskTemplate? GetTemplate(int templateId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            var tmpl = db.QueryFirstOrDefault<TaskTemplate>(
                "SELECT * FROM TaskTemplates WHERE TemplateId = @templateId", new { templateId });
            if (tmpl == null) return null;

            tmpl.Items = db.Query<TaskTemplateItem>(
                "SELECT * FROM TaskTemplateItems WHERE TemplateId = @templateId ORDER BY SortOrder",
                new { templateId }).ToList();
            return tmpl;
        }

        public int SaveTemplate(TaskTemplate template)
        {
            using var db = new SqlConnection(_cs);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                int templateId;
                if (template.TemplateId == 0)
                {
                    templateId = db.QuerySingle<int>(@"
                        INSERT INTO TaskTemplates (Name, Description, CreatedBy)
                        VALUES (@Name, @Description, @CreatedBy);
                        SELECT CAST(SCOPE_IDENTITY() AS INT);", template, tx);
                }
                else
                {
                    templateId = template.TemplateId;
                    db.Execute(@"
                        UPDATE TaskTemplates
                        SET Name = @Name, Description = @Description
                        WHERE TemplateId = @TemplateId", template, tx);
                    db.Execute("DELETE FROM TaskTemplateItems WHERE TemplateId = @templateId",
                        new { templateId }, tx);
                }

                foreach (var item in template.Items)
                {
                    db.Execute(@"
                        INSERT INTO TaskTemplateItems (TemplateId, Title, Description, Priority, DueDaysOffset, SortOrder)
                        VALUES (@templateId, @Title, @Description, @Priority, @DueDaysOffset, @SortOrder)",
                        new
                        {
                            templateId,
                            item.Title,
                            item.Description,
                            item.Priority,
                            item.DueDaysOffset,
                            item.SortOrder
                        }, tx);
                }

                tx.Commit();
                return templateId;
            }
            catch { tx.Rollback(); throw; }
        }

        public bool DeleteTemplate(int templateId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            int rows = db.Execute("DELETE FROM TaskTemplates WHERE TemplateId = @templateId", new { templateId });
            return rows > 0;
        }

        public List<ErpTask> CreateTasksFromTemplate(int templateId, string assignTo, DateTime startDate, string createdBy)
        {
            var tmpl = GetTemplate(templateId);
            if (tmpl == null) return new List<ErpTask>();

            using IDbConnection db = new SqlConnection(_cs);
            var created = new List<ErpTask>();

            foreach (var item in tmpl.Items.OrderBy(i => i.SortOrder))
            {
                var task = new ErpTask
                {
                    Title      = item.Title,
                    Description = item.Description,
                    AssignedTo = assignTo,
                    CreatedBy  = createdBy,
                    DueDate    = startDate.AddDays(item.DueDaysOffset),
                    Status     = "Open",
                    Priority   = item.Priority
                };
                task.TaskID = db.QuerySingle<int>(@"
                    INSERT INTO Tasks (Title, Description, AssignedTo, CreatedBy, DueDate, Status, Priority)
                    VALUES (@Title, @Description, @AssignedTo, @CreatedBy, @DueDate, @Status, @Priority);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);", task);
                created.Add(task);
            }

            return created;
        }

        // ── Recurrence ───────────────────────────────────────────────────────────

        public bool SetRecurrence(int taskId, string pattern, int interval)
        {
            using IDbConnection db = new SqlConnection(_cs);
            int rows = db.Execute(@"
                UPDATE Tasks
                SET RecurrencePattern = @pattern, RecurrenceInterval = @interval
                WHERE TaskID = @taskId",
                new { taskId, pattern, interval });
            return rows > 0;
        }

        public bool ClearRecurrence(int taskId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            int rows = db.Execute(@"
                UPDATE Tasks
                SET RecurrencePattern = NULL, RecurrenceInterval = 1, NextOccurrence = NULL
                WHERE TaskID = @taskId",
                new { taskId });
            return rows > 0;
        }

        public ErpTask? GenerateNextRecurrence(int taskId, string createdBy)
        {
            var source = GetById(taskId);
            if (source == null || string.IsNullOrEmpty(source.RecurrencePattern))
                return null;

            // Calculate next due date
            var baseDate = source.NextOccurrence ?? source.DueDate;
            DateTime nextDue = source.RecurrencePattern switch
            {
                "Daily"   => baseDate.AddDays(source.RecurrenceInterval),
                "Weekly"  => baseDate.AddDays(source.RecurrenceInterval * 7),
                "Monthly" => baseDate.AddMonths(source.RecurrenceInterval),
                _         => baseDate.AddDays(source.RecurrenceInterval)
            };

            using IDbConnection db = new SqlConnection(_cs);

            // Stamp next occurrence on source task
            db.Execute("UPDATE Tasks SET NextOccurrence = @nextDue WHERE TaskID = @taskId",
                new { nextDue, taskId });

            // Insert the new recurring task
            var newTask = new ErpTask
            {
                Title             = source.Title,
                Description       = source.Description,
                AssignedTo        = source.AssignedTo,
                CreatedBy         = createdBy,
                DueDate           = nextDue,
                Status            = "Open",
                Priority          = source.Priority,
                WorkflowID        = source.WorkflowID,
                RecurrencePattern = source.RecurrencePattern,
                RecurrenceInterval = source.RecurrenceInterval,
                ParentTaskId      = taskId,
                Tags              = source.Tags
            };

            newTask.TaskID = db.QuerySingle<int>(@"
                INSERT INTO Tasks
                    (Title, Description, AssignedTo, CreatedBy, DueDate, Status, Priority,
                     WorkflowID, RecurrencePattern, RecurrenceInterval, ParentTaskId, Tags)
                VALUES
                    (@Title, @Description, @AssignedTo, @CreatedBy, @DueDate, @Status, @Priority,
                     @WorkflowID, @RecurrencePattern, @RecurrenceInterval, @ParentTaskId, @Tags);
                SELECT CAST(SCOPE_IDENTITY() AS INT);", newTask);

            return newTask;
        }
    }
}
