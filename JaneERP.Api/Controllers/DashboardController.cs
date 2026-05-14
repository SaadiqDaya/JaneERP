using JaneERP.Api.Data;
using JaneERP.Api.Models;
using JaneERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JaneERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Finance,Manager,Sales,Warehouse")]
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
    public async Task<IActionResult> Get([FromQuery] int days = 30)
    {
        var t1 = Task.Run(() => _products.GetTotalActiveProducts());
        var t2 = Task.Run(() => _products.GetLowStockCount());
        var t3 = Task.Run(() => _orders.GetSalesTotal(days));
        var t4 = Task.Run(() => _orders.GetOrdersToPackCount());
        var t5 = Task.Run(() => _pos.GetItemsToReceiveCount());
        var t6 = Task.Run(() => _pos.GetOverduePOCount());
        var t7 = Task.Run(() => _cycleCount.GetOverdueCount(30));
        var t8 = Task.Run(() => _pos.GetPosToReceive());
        var t9 = Task.Run(() => _orders.GetSosToPack());

        await Task.WhenAll(t1, t2, t3, t4, t5, t6, t7, t8, t9);

        var result = new DashboardResponse
        {
            SalesDays         = days,
            TotalProducts     = await t1,
            LowStockItems     = await t2,
            SalesTotal        = await t3,
            OrdersToPack      = await t4,
            ItemsToReceive    = await t5,
            OverduePOs        = await t6,
            OverdueCycleCount = await t7,
            PosToReceive      = await t8,
            SosToPack         = await t9,
        };
        return Ok(result);
    }
}
