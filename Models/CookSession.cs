namespace JaneERP.Models
{
    /// <summary>A cook session groups work orders being produced together in one cooking run.</summary>
    public class CookSession
    {
        public int       CookSessionID { get; set; }
        public string    SessionName   { get; set; } = "";
        public string    Status        { get; set; } = "Open";   // Open | Complete
        public string?   CreatedBy     { get; set; }
        public DateTime  CreatedAt     { get; set; }
        public DateTime? CompletedAt   { get; set; }

        /// <summary>Work orders included in this session (populated by GetCookSession).</summary>
        public List<int> WorkOrderIDs  { get; set; } = new();
    }

    /// <summary>
    /// One step in a cook session = one (WorkOrder × Part) combination that must be verified.
    /// When all steps for a session are done the session can be completed.
    /// </summary>
    public class CookSessionStep
    {
        public int       StepID         { get; set; }
        public int       CookSessionID  { get; set; }
        public int       WorkOrderID    { get; set; }
        public int       PartID         { get; set; }
        public bool      IsDone         { get; set; }
        public string?   DoneBy         { get; set; }
        public DateTime? DoneAt         { get; set; }

        // ── Joined display data ───────────────────────────────────────────────
        public string  PartNumber    { get; set; } = "";
        public string  PartName      { get; set; } = "";
        public string? UnitOfMeasure { get; set; }
        public string  ProductName   { get; set; } = "";
        public string? ProductSKU    { get; set; }
        /// <summary>Quantity of this part needed for this work order (BOM qty × WO qty).</summary>
        public decimal RequiredQty   { get; set; }
        /// <summary>The work order quantity (number of units being produced).</summary>
        public int     WorkOrderQty  { get; set; }
    }

    /// <summary>
    /// Summary row for the progress panel: one row per ingredient across all batches in the session.
    /// </summary>
    public class CookIngredientSummary
    {
        public int     PartID         { get; set; }
        public string  PartNumber     { get; set; } = "";
        public string  PartName       { get; set; } = "";
        public string? UnitOfMeasure  { get; set; }
        /// <summary>Total quantity needed across all work orders in the session.</summary>
        public decimal TotalRequired  { get; set; }
        /// <summary>Current stock on hand.</summary>
        public int     OnHand         { get; set; }
        /// <summary>Number of work order steps that are done for this ingredient.</summary>
        public int     StepsDone      { get; set; }
        /// <summary>Total number of work order steps for this ingredient.</summary>
        public int     StepsTotal     { get; set; }
        public bool    IsComplete     => StepsDone >= StepsTotal && StepsTotal > 0;
        public bool    HasEnoughStock => OnHand >= (int)Math.Ceiling((double)TotalRequired);
        public string  ProgressText   => $"{StepsDone}/{StepsTotal}";
    }

    /// <summary>Row for the printable / exportable Batch Traveller document.</summary>
    public class BatchTravellerRow
    {
        public string  WONumber      { get; set; } = "";
        public string  PartNumber    { get; set; } = "";
        public string  PartName      { get; set; } = "";
        public string? Nicotine      { get; set; }
        public string? Size          { get; set; }
        public int     Qty           { get; set; }
        public string? FlaskType     { get; set; }
        public string? Bins          { get; set; }
        public string? Concentrate   { get; set; }
        public string? Notes         { get; set; }
    }

    /// <summary>
    /// Lightweight BOM preview row for a single work order — used by the ingredient preview panel
    /// in FormBatchCooking before a cook session is started.
    /// </summary>
    public class WOBomPreviewRow
    {
        public int     PartID      { get; set; }
        public string  PartNumber  { get; set; } = "";
        public string  PartName    { get; set; } = "";
        public string? UOM         { get; set; }
        /// <summary>BOM qty × work order qty = total required.</summary>
        public decimal RequiredQty { get; set; }
        /// <summary>Current stock on hand for this part.</summary>
        public int     OnHand      { get; set; }
    }

    /// <summary>Row for the label-printing CSV export.</summary>
    public class LabelExportRow
    {
        public string? BottleType    { get; set; }
        public string  WONumber      { get; set; } = "";
        public string  PartName      { get; set; } = "";
        public string  PartNumber    { get; set; } = "";
        public int     QtyOrdered    { get; set; }
        public string? Version       { get; set; }
        public string  BatchMadeDate { get; set; } = "";
        public string? Size          { get; set; }
        public string? Brand         { get; set; }
        public string? Nicotine      { get; set; }
        public string? VG            { get; set; }
        public string? Note          { get; set; }
    }
}
