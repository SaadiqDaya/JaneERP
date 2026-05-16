namespace JaneERP.Interfaces
{
    public interface ITaskRepository
    {
        List<ErpTask>       GetAll(string? assignedTo = null, string? status = null);
        ErpTask?            GetById(int taskId);
        List<ErpTask>       GetOutstanding(string? assignedTo = null);
        int                 Add(ErpTask task);
        void                UpdateStatus(int taskId, string status, string changedBy = "");
        void                UpdateDueDate(int taskId, DateTime dueDate, string changedBy = "");
        void                UpdateDescription(int taskId, string description, string changedBy = "");
        void                UpdateAssignedTo(int taskId, string assignedTo, string changedBy = "");
        void                UpdatePriority(int taskId, string priority, string changedBy = "");
        void                UpdateWorkflowStatus(int taskId, int? workflowId, string? workflowStatus);
        void                Delete(int taskId);
        List<string>        GetAllUsernames();
        List<TaskComment>   GetComments(int taskId);
        void                AddComment(int taskId, string username, string body);
        void                AddMention(int taskId, string mentionedUser, string mentionedBy, string commentText);
        List<TaskMention>   GetMentions(string forUser, bool unreadOnly = true);
        void                MarkMentionRead(int mentionId);
        void                MarkAllMentionsRead(string forUser);
        List<TaskWorkflow>  GetWorkflows();
        int                 AddWorkflow(string name);
        void                DeleteWorkflow(int workflowId);
        List<WorkflowStatus> GetWorkflowStatuses(int workflowId);
        int                 AddWorkflowStatus(int workflowId, string statusName);
        void                DeleteWorkflowStatus(int statusId);
        void                MoveWorkflowStatus(int statusId, bool moveUp);
        void                SetTaskWorkflow(int taskId, int? workflowId, string? initialStatus);
        List<string>        GetWorkflowStatusNames(int workflowId);
        Dictionary<string, List<string>> GetAllWorkflowStageNames(); // single-query: workflowName -> stage names
        List<(string Username, string Email)> GetUserEmails(IEnumerable<string> usernames);

        // ── Linked records ────────────────────────────────────────────────────────
        TaskLinkedRecord?   GetLinkedRecord(int taskId);
        bool                SetLinkedRecord(int taskId, string module, string linkedId, string displayLabel);
        bool                ClearLinkedRecord(int taskId);
        List<ErpTask>       GetTasksByLinkedRecord(string module, string linkedId);
        // Multi-linked record methods
        List<TaskLinkedRecord> GetLinkedRecords(int taskId);
        bool                AddLinkedRecord(int taskId, string module, string linkedId, string displayLabel);
        bool                RemoveLinkedRecord(int linkId);

        // ── History / audit trail ─────────────────────────────────────────────────
        List<TaskHistoryEntry> GetHistory(int taskId);
        bool                LogHistory(int taskId, string fieldName, string? oldValue, string? newValue, string changedBy);

        // ── Subtasks ──────────────────────────────────────────────────────────────
        List<TaskSubtask>   GetSubtasks(int taskId);
        bool                AddSubtask(int taskId, string title, int sortOrder = 0);
        bool                CompleteSubtask(int subtaskId, string completedBy);
        bool                UncompleteSubtask(int subtaskId);
        bool                DeleteSubtask(int subtaskId);

        // ── Workload ──────────────────────────────────────────────────────────────
        Dictionary<string, int> GetWorkloadSummary();  // username -> open task count

        // ── Overdue (for notification service) ───────────────────────────────────
        List<ErpTask>       GetOverdueTasks();  // tasks past DueDate where Status != 'Done'

        // ── Tags ──────────────────────────────────────────────────────────────────
        bool                UpdateTags(int taskId, string tags);  // comma-separated

        // ── Templates ────────────────────────────────────────────────────────────
        List<TaskTemplate>  GetTemplates();
        TaskTemplate?       GetTemplate(int templateId);
        int                 SaveTemplate(TaskTemplate template);
        bool                DeleteTemplate(int templateId);
        List<ErpTask>       CreateTasksFromTemplate(int templateId, string assignTo, DateTime startDate, string createdBy);

        // ── Recurrence ───────────────────────────────────────────────────────────
        bool                SetRecurrence(int taskId, string pattern, int interval);
        bool                ClearRecurrence(int taskId);
        ErpTask?            GenerateNextRecurrence(int taskId, string createdBy);  // creates next occurrence task

        // ── Paged queries ─────────────────────────────────────────────────────────
        (List<ErpTask> tasks, int total) GetPagedTasks(
            int page, int pageSize,
            string? assignedTo = null, string? stage = null,
            string? search = null, string? tag = null, bool showAll = false);
    }
}
