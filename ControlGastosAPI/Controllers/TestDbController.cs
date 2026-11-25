using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ControlGastosAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestDbController : ControllerBase
{
    private readonly string _connectionString;

    public TestDbController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta DefaultConnection en appsettings.json");
    }

    [HttpGet]
    public async Task<IActionResult> ProbarConexion()
    {
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            return Ok(new { mensaje = "Conexión exitosa a GASTOS_PERSONALESUV" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}