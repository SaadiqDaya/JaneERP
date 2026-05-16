namespace JaneERP.Core.Models;

/// <summary>
/// A single inventory lot for a raw material part.
/// Created when a PO is received; quantity is decremented when a work order completes.
/// </summary>
public class PartLot
{
    public int       LotID          { get; set; }
    public int       PartID         { get; set; }
    public int?      LocationID     { get; set; }
    public string?   LotNumber      { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public int       Quantity       { get; set; }
    public DateTime  ReceivedAt     { get; set; }
    public string?   Notes          { get; set; }
}

/// <summary>
/// One row in the lot-selection UI for a single available lot of a BOM part.
/// </summary>
public class LotAvailabilityRow
{
    /// <summary>0 = unlotted (backward compat for parts with no PartLots rows).</summary>
    public int       LotID           { get; set; }
    public string?   LotNumber       { get; set; }
    public DateTime? ExpirationDate  { get; set; }
    public int?      LocationID      { get; set; }
    public string    LocationName    { get; set; } = "";
    public int       TotalQty        { get; set; }
    public int       AlreadyReserved { get; set; }
    public int       Available       { get; set; }
}

/// <summary>
/// All available lots for one BOM part, used to build the Go-Live lot picker.
/// </summary>
public class PartLotAvailability
{
    public int                      PartID     { get; set; }
    public string                   PartNumber { get; set; } = "";
    public string                   PartName   { get; set; } = "";
    public int                      Required   { get; set; }
    public List<LotAvailabilityRow> Lots       { get; set; } = [];
}

/// <summary>
/// One line in the user's lot-reservation selection for GoLive.
/// </summary>
public class LotReservation
{
    /// <summary>0 = unlotted (backward compat).</summary>
    public int LotID    { get; set; }
    public int PartID   { get; set; }
    public int Quantity { get; set; }
}

/// <summary>
/// Parameters for completing a work order.
/// </summary>
public class CompleteWorkOrderRequest
{
    public int     CompletedQty { get; set; }
    public int     ScrapQty     { get; set; }
    public string? ScrapReason  { get; set; }
    /// <summary>Finished goods go here. If null, product's DefaultLocationID is used.</summary>
    public int?    LocationID   { get; set; }
    public string? Notes        { get; set; }
}

/// <summary>
/// BOM ingredient line for the pre-cooking preview.
/// </summary>
public class BomPreviewRow
{
    public int     PartID           { get; set; }
    public string  PartNumber       { get; set; } = "";
    public string  PartName         { get; set; } = "";
    public string  UOM              { get; set; } = "";
    public int     RequiredQty      { get; set; }
    public int     OnHand           { get; set; }
    public bool    CreatesBatchLoss { get; set; }
    public decimal BatchLossRate    { get; set; }
}
