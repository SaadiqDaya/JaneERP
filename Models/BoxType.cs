namespace JaneERP.Models
{
    public class BoxType
    {
        public int    BoxTypeID { get; set; }
        public string BoxName   { get; set; } = "";
        public string Notes     { get; set; } = "";
        public bool   IsActive  { get; set; } = true;

        public override string ToString() => BoxName;
    }
}
