using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace ControlGastosAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MovimientosController : ControllerBase
{
    private readonly string _connectionString;

    public MovimientosController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta DefaultConnection en appsettings.json");
    }

    private static object ConvertirValor(string key, JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String when string.Equals(key, "Fecha", StringComparison.OrdinalIgnoreCase)
                && DateTime.TryParse(value.GetString(), out var fecha) => fecha,
            JsonValueKind.String => value.GetString()!,
            JsonValueKind.Number => value.TryGetInt32(out int n) ? n : value.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => DBNull.Value,
            _ => value.ToString()
        };
    }

    // 1. OBTENER TODOS LOS MOVIMIENTOS: GET api/Movimientos
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var lista = new List<Dictionary<string, object?>>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            const string sql = "SELECT * FROM dbo.MOVIMIENTOS";
            await using var cmd = new SqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var fila = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    fila[reader.GetName(i)] = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
                }
                lista.Add(fila);
            }

            return Ok(lista);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // 2. RESUMEN MENSUAL POR CATEGORÍA: GET api/Movimientos/resumen-mensual?idUsuario=1&anio=2026&mes=11
    [HttpGet("resumen-mensual")]
    public async Task<IActionResult> GetResumenMensual([FromQuery] int? idUsuario = null, [FromQuery] int? anio = null, [FromQuery] int? mes = null)
    {
        try
        {
            var lista = new List<Dictionary<string, object?>>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT
                    YEAR(m.Fecha) AS Anio,
                    MONTH(m.Fecha) AS Mes,
                    c.Id_Categoria,
                    c.Nombre AS Categoria,
                    COUNT(m.Id_Movimiento) AS Cantidad_Movimientos,
                    SUM(m.Monto) AS Total_Monto
                FROM dbo.MOVIMIENTOS m
                INNER JOIN dbo.CATEGORIA c ON m.Id_Categoria = c.Id_Categoria
                WHERE 1 = 1";

            await using var cmd = new SqlCommand();
            cmd.Connection = conn;

            if (idUsuario.HasValue)
            {
                sql += " AND m.Id_Usuario = @idUsuario";
                cmd.Parameters.AddWithValue("@idUsuario", idUsuario.Value);
            }
            if (anio.HasValue)
            {
                sql += " AND YEAR(m.Fecha) = @anio";
                cmd.Parameters.AddWithValue("@anio", anio.Value);
            }
            if (mes.HasValue)
            {
                sql += " AND MONTH(m.Fecha) = @mes";
                cmd.Parameters.AddWithValue("@mes", mes.Value);
            }

            sql += @"
                GROUP BY YEAR(m.Fecha), MONTH(m.Fecha), c.Id_Categoria, c.Nombre
                ORDER BY Anio DESC, Mes DESC, Total_Monto DESC;";

            cmd.CommandText = sql;
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var fila = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    fila[reader.GetName(i)] = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
                }
                lista.Add(fila);
            }

            return Ok(lista);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // 3. CREAR MOVIMIENTO: POST api/Movimientos
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Dictionary<string, JsonElement> body)
    {
        if (body == null || body.Count == 0)
            return BadRequest(new { error = "Debes enviar los campos del movimiento en el body." });

        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var columnas = new List<string>();
            var parametros = new List<string>();
            await using var cmd = new SqlCommand();
            cmd.Connection = conn;

            int idx = 0;
            foreach (var kvp in body)
            {
                string paramName = $"@p{idx++}";
                columnas.Add($"[{kvp.Key}]");
                parametros.Add(paramName);
                cmd.Parameters.AddWithValue(paramName, ConvertirValor(kvp.Key, kvp.Value));
            }

            cmd.CommandText = $"INSERT INTO dbo.MOVIMIENTOS ({string.Join(", ", columnas)}) VALUES ({string.Join(", ", parametros)}); SELECT SCOPE_IDENTITY();";
            var nuevoId = await cmd.ExecuteScalarAsync();

            return StatusCode(201, new { mensaje = "Movimiento creado correctamente", idGenerado = nuevoId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // 4. ACTUALIZAR MOVIMIENTO: PUT api/Movimientos/{id}?colId=Id_Movimiento
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Dictionary<string, JsonElement> body, [FromQuery] string colId = "Id_Movimiento")
    {
        if (body == null || body.Count == 0)
            return BadRequest(new { error = "Debes enviar los campos a actualizar." });

        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var sets = new List<string>();
            await using var cmd = new SqlCommand();
            cmd.Connection = conn;

            int idx = 0;
            foreach (var kvp in body)
            {
                if (string.Equals(kvp.Key, colId, StringComparison.OrdinalIgnoreCase)) continue;

                string paramName = $"@p{idx++}";
                sets.Add($"[{kvp.Key}] = {paramName}");
                cmd.Parameters.AddWithValue(paramName, ConvertirValor(kvp.Key, kvp.Value));
            }

            if (sets.Count == 0)
                return BadRequest(new { error = "No hay campos modificables en el body." });

            cmd.Parameters.AddWithValue("@id", id);
            cmd.CommandText = $"UPDATE dbo.MOVIMIENTOS SET {string.Join(", ", sets)} WHERE [{colId}] = @id";

            int filasAfectadas = await cmd.ExecuteNonQueryAsync();
            if (filasAfectadas == 0)
                return NotFound(new { mensaje = $"No se encontró movimiento con {colId} = {id}" });

            return Ok(new { mensaje = "Movimiento actualizado correctamente", filasAfectadas });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // 5. ELIMINAR MOVIMIENTO: DELETE api/Movimientos/{id}?colId=Id_Movimiento
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, [FromQuery] string colId = "Id_Movimiento")
    {
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            const string sql = "DELETE FROM dbo.MOVIMIENTOS WHERE [{0}] = @id";
            await using var cmd = new SqlCommand(string.Format(sql, colId), conn);
            cmd.Parameters.AddWithValue("@id", id);

            int filasAfectadas = await cmd.ExecuteNonQueryAsync();
            if (filasAfectadas == 0)
                return NotFound(new { mensaje = $"No se encontró movimiento con {colId} = {id}" });

            return Ok(new { mensaje = "Movimiento eliminado correctamente", filasAfectadas });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}