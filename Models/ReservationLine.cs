namespace JaneERP.Models
{
    /// <summary>
    /// One row in the stock-reservation dialog — either a product at a location (Sales Order)
    /// or a part (Work Order).
    /// </summary>
    public class ReservationLine
    {
        /// <summary>ProductID or PartID depending on context.</summary>
        public int    ItemId          { get; set; }
        /// <summary>Null for part reservations; set for product reservations.</summary>
        public int?   LocationId      { get; set; }
        /// <summary>Human-readable label shown in the grid (e.g. "PROD-001 — Vanilla Pod").</summary>
        public string DisplayLabel    { get; set; } = "";
        /// <summary>Location name for product reservations, or "—" for parts.</summary>
        public string LocationName    { get; set; } = "—";
        /// <summary>How many units this order/WO needs of this item.</summary>
        public int    Required        { get; set; }
        /// <summary>Total on-hand at this location (or total parts stock).</summary>
        public int    OnHand          { get; set; }
        /// <summary>Already reserved by OTHER orders/WOs at this location.</summary>
        public int    AlreadyReserved { get; set; }
        /// <summary>Computed: OnHand minus AlreadyReserved, floored at 0.</summary>
        public int    Available       => Math.Max(0, OnHand - AlreadyReserved);
        /// <summary>User-selected quantity to lock. Pre-populated to min(Required, Available).</summary>
        public int    ToLock          { get; set; }

        // ── Lot tracking (Phase 2) ────────────────────────────────────────────
        /// <summary>0 = unlotted/backward compat. Set to PartLots.LotID when lot selection is active.</summary>
        public int       LotID          { get; set; }
        public string?   LotNumber      { get; set; }
        public DateTime? ExpirationDate { get; set; }
    }
}
