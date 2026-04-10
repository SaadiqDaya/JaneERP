namespace JaneERP.Models
{
    public class Location
    {
        public int     LocationID   { get; set; }
        public string  LocationName { get; set; } = "";
        public bool    IsActive     { get; set; } = true;
        public string? Notes        { get; set; }

        /// <summary>Used by ComboBox display.</summary>
        public override string ToString() => LocationName;
    }
}
