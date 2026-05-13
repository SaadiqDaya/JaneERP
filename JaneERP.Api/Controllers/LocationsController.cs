using JaneERP.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JaneERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LocationsController : ControllerBase
{
    private readonly ApiLocationRepository _repo;
    public LocationsController(ApiLocationRepository repo) => _repo = repo;

    [HttpGet]
    public IActionResult GetLocations() => Ok(_repo.GetActive());
}
