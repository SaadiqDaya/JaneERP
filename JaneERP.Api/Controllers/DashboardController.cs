using JaneERP.Api.Data;
using JaneERP.Api.Models;
using JaneERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JaneERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly ApiProductRepository       _products;
    private readonly ApiOrderRepository         _orders;
    private readonly ApiPurchaseOrderRepository _pos;
    private readonly ApiCycleCountRepository    _cycleCount;

    public DashboardController(
        ApiProductRepository       products,
        ApiOrderRepository         orders,
        ApiPurchaseOrderRepository pos,
        ApiCycleCountRepository    cycleCount)
    {
        _products   = products;
        _orders     = orders;
        _pos        = pos;
        _cycleCount = cycleCount;
    }

    [HttpGet]
    public IActionResult Get([FromQuery] int days = 30)
    {
        var result = new DashboardResponse
        {
            SalesDays        = days,
            TotalProducts    = _products.GetTotalActiveProducts(),
            LowStockItems    = _products.GetLowStockCount(),
            SalesTotal       = _orders.GetSalesTotal(days),
            OrdersToPack     = _orders.GetOrdersToPackCount(),
            ItemsToReceive   = _pos.GetItemsToReceiveCount(),
            OverduePOs       = _pos.GetOverduePOCount(),
            OverdueCycleCount= _cycleCount.GetOverdueCount(30),
            PosToReceive     = _pos.GetPosToReceive(),
            SosToPack        = _orders.GetSosToPack(),
        };
        return Ok(result);
    }
}
