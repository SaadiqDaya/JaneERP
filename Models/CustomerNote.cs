namespace JaneERP.Models
{
    public class CustomerNote
    {
        public int      NoteID     { get; set; }
        public int      CustomerID { get; set; }
        public string   NoteText   { get; set; } = string.Empty;
        public string   NoteType   { get; set; } = "Note";  // Note | Call | Email | Visit
        public string?  CreatedBy  { get; set; }
        public DateTime CreatedAt  { get; set; }
    }
}
