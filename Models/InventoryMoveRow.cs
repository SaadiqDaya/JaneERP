namespace JaneERP.Models
{
    /// <summary>
    /// One row from an inventory-moves CSV import.
    /// Populated with resolved IDs and validation state by
    /// ImportRepository.ValidateInventoryMoves before being shown in
    /// the preview grid and eventually passed to ExecuteInventoryMoves.
    /// </summary>
    public class InventoryMoveRow
    {
        // ── CSV-sourced fields ────────────────────────────────────────────────
        public string SKU          { get; set; } = string.Empty;
        public string FromLocation { get; set; } = string.Empty;
        public string ToLocation   { get; set; } = string.Empty;
        /// <summary>Null = move all available stock at the source location.</summary>
        public int?   RequestedQty { get; set; }

        // ── Resolved by ValidateInventoryMoves ───────────────────────────────
        public string ProductName  { get; set; } = string.Empty;
        public int    ProductID    { get; set; }
        public int    FromLocID    { get; set; }
        public int    ToLocID      { get; set; }
        /// <summary>Net stock currently sitting at FromLocation for this product.</summary>
        public int    AvailableQty { get; set; }
        /// <summary>
        /// Quantity that will actually be moved = Min(RequestedQty ?? Available, Available).
        /// 0 if the row is invalid.
        /// </summary>
        public int    MoveQty      { get; set; }

        // ── Lot / expiry (optional, set by ParseCsv after validation) ───────────
        /// <summary>Lot number to move. Null = move across all lots (oldest first, FEFO).</summary>
        public string?   LotNumber      { get; set; }
        /// <summary>Expiration date filter. Null = not filtered by expiry.</summary>
        public DateTime? ExpirationDate { get; set; }
        /// <summary>Human-readable summary of lots that will be consumed, populated by ValidateInventoryMoves.</summary>
        public string    LotSummary     { get; set; } = string.Empty;

        // ── Validation state ─────────────────────────────────────────────────
        public bool   IsValid { get; set; }
        public string? Error  { get; set; }

        public string StatusLabel => IsValid ? $"Move {MoveQty}" : $"Skip — {Error}";
    }
}
