namespace JaneERP.Interfaces
{
    public interface ITaskRepository
    {
        List<ErpTask>       GetAll(string? assignedTo = null, string? status = null);
        ErpTask?            GetById(int taskId);
        List<ErpTask>       GetOutstanding(string? assignedTo = null);
        int                 Add(ErpTask task);
        void                UpdateStatus(int taskId, string status);
        void                UpdateDueDate(int taskId, DateTime dueDate);
        void                UpdateDescription(int taskId, string description);
        void                UpdateAssignedTo(int taskId, string assignedTo);
        void                UpdatePriority(int taskId, string priority);
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
        List<(string Username, string Email)> GetUserEmails(IEnumerable<string> usernames);
    }
}
