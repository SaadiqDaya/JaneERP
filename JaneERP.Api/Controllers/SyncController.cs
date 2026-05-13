using System.Runtime.Versioning;
using JaneERP.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JaneERP.Api.Controllers;

[ApiController]
[Route("api/sync")]
[Authorize]
[SupportedOSPlatform("windows")]
public class SyncController : ControllerBase
{
    private readonly ApiSyncRepository _sync;
    public SyncController(ApiSyncRepository sync) => _sync = sync;

    /// <summary>GET /api/sync/stores — list all active stores with last sync time</summary>
    [HttpGet("stores")]
    public IActionResult GetStores()
    {
        try { return Ok(_sync.GetStores()); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    /// <summary>POST /api/sync/{storeId} — pull new orders from Shopify for one store</summary>
    [HttpPost("{storeId:int}")]
    public async Task<IActionResult> SyncStore(int storeId)
    {
        try
        {
            var result = await _sync.SyncStoreAsync(storeId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
