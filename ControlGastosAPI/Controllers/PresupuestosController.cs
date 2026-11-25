using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace ControlGastosAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PresupuestosController : ControllerBase
{
    private readonly string _connectionString;

    public PresupuestosController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta DefaultConnection en appsettings.json");
    }

    // 1. OBTENER TODOS: GET api/Presupuestos
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var lista = new List<Dictionary<string, object?>>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            const string sql = "SELECT * FROM dbo.PRESUPUESTO";
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

    // 2. CREAR: POST api/Presupuestos
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Dictionary<string, JsonElement> body)
    {
        if (body == null || body.Count == 0)
            return BadRequest(new { error = "Debes enviar los campos del presupuesto en el body." });

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

                object valor = kvp.Value.ValueKind switch
                {
                    JsonValueKind.String => kvp.Value.GetString()!,
                    JsonValueKind.Number => kvp.Value.TryGetInt32(out int n) ? n : kvp.Value.GetDecimal(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => DBNull.Value,
                    _ => kvp.Value.ToString()
                };

                cmd.Parameters.AddWithValue(paramName, valor);
            }

            cmd.CommandText = $"INSERT INTO dbo.PRESUPUESTO ({string.Join(", ", columnas)}) VALUES ({string.Join(", ", parametros)}); SELECT SCOPE_IDENTITY();";
            var nuevoId = await cmd.ExecuteScalarAsync();

            return StatusCode(201, new { mensaje = "Presupuesto creado correctamente", idGenerado = nuevoId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // 3. ACTUALIZAR: PUT api/Presupuestos/{id}?colId=Id_Presupuesto
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Dictionary<string, JsonElement> body, [FromQuery] string colId = "Id_Presupuesto")
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

                object valor = kvp.Value.ValueKind switch
                {
                    JsonValueKind.String => kvp.Value.GetString()!,
                    JsonValueKind.Number => kvp.Value.TryGetInt32(out int n) ? n : kvp.Value.GetDecimal(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => DBNull.Value,
                    _ => kvp.Value.ToString()
                };

                cmd.Parameters.AddWithValue(paramName, valor);
            }

            if (sets.Count == 0)
                return BadRequest(new { error = "No hay campos modificables en el body." });

            cmd.Parameters.AddWithValue("@id", id);
            cmd.CommandText = $"UPDATE dbo.PRESUPUESTO SET {string.Join(", ", sets)} WHERE [{colId}] = @id";

            int filasAfectadas = await cmd.ExecuteNonQueryAsync();
            if (filasAfectadas == 0)
                return NotFound(new { mensaje = $"No se encontró presupuesto con {colId} = {id}" });

            return Ok(new { mensaje = "Presupuesto actualizado correctamente", filasAfectadas });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // 4. ELIMINAR: DELETE api/Presupuestos/{id}?colId=Id_Presupuesto
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, [FromQuery] string colId = "Id_Presupuesto")
    {
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            const string sql = "DELETE FROM dbo.PRESUPUESTO WHERE [{0}] = @id";
            await using var cmd = new SqlCommand(string.Format(sql, colId), conn);
            cmd.Parameters.AddWithValue("@id", id);

            int filasAfectadas = await cmd.ExecuteNonQueryAsync();
            if (filasAfectadas == 0)
                return NotFound(new { mensaje = $"No se encontró presupuesto con {colId} = {id}" });

            return Ok(new { mensaje = "Presupuesto eliminado correctamente", filasAfectadas });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}