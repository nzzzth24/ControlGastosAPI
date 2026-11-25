using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace ControlGastosAPI.Controllers;

[ApiController]
[Route("api")]
public class CatalogosController : ControllerBase
{
    private readonly string _connectionString;

    public CatalogosController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta DefaultConnection en appsettings.json");
    }

    private static object ConvertirValor(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()!,
            JsonValueKind.Number => value.TryGetInt32(out int n) ? n : value.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => DBNull.Value,
            _ => value.ToString()
        };
    }

    private async Task<IActionResult> ObtenerTodo(string tabla)
    {
        try
        {
            var lista = new List<Dictionary<string, object?>>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = $"SELECT * FROM dbo.{tabla}";
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

    private async Task<IActionResult> InsertarEnTabla(string tabla, Dictionary<string, JsonElement>? body)
    {
        if (body == null || body.Count == 0)
            return BadRequest(new { error = "Debes enviar los datos en el body." });

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
                cmd.Parameters.AddWithValue(paramName, ConvertirValor(kvp.Value));
            }

            cmd.CommandText = $"INSERT INTO dbo.{tabla} ({string.Join(", ", columnas)}) VALUES ({string.Join(", ", parametros)}); SELECT SCOPE_IDENTITY();";
            var nuevoId = await cmd.ExecuteScalarAsync();

            return StatusCode(201, new { mensaje = $"Registro creado en {tabla}", idGenerado = nuevoId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private async Task<IActionResult> ActualizarEnTabla(string tabla, string colId, int id, Dictionary<string, JsonElement>? body)
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
                cmd.Parameters.AddWithValue(paramName, ConvertirValor(kvp.Value));
            }

            if (sets.Count == 0)
                return BadRequest(new { error = "No hay campos modificables en el body." });

            cmd.Parameters.AddWithValue("@id", id);
            cmd.CommandText = $"UPDATE dbo.{tabla} SET {string.Join(", ", sets)} WHERE [{colId}] = @id";

            int filasAfectadas = await cmd.ExecuteNonQueryAsync();
            if (filasAfectadas == 0)
                return NotFound(new { mensaje = $"No se encontró registro en {tabla} con {colId} = {id}" });

            return Ok(new { mensaje = $"Registro actualizado en {tabla}", filasAfectadas });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private async Task<IActionResult> EliminarEnTabla(string tabla, string colId, int id)
    {
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string sql = $"DELETE FROM dbo.{tabla} WHERE [{colId}] = @id";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);

            int filasAfectadas = await cmd.ExecuteNonQueryAsync();
            if (filasAfectadas == 0)
                return NotFound(new { mensaje = $"No se encontró registro en {tabla} con {colId} = {id}" });

            return Ok(new { mensaje = $"Registro eliminado de {tabla}", filasAfectadas });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // CIUDADES
    [HttpGet("Ciudades")] public Task<IActionResult> GetCiudades() => ObtenerTodo("CIUDAD");
    [HttpPost("Ciudades")] public Task<IActionResult> PostCiudad([FromBody] Dictionary<string, JsonElement> body) => InsertarEnTabla("CIUDAD", body);
    [HttpPut("Ciudades/{id}")] public Task<IActionResult> PutCiudad(int id, [FromBody] Dictionary<string, JsonElement> body) => ActualizarEnTabla("CIUDAD", "Id_Ciudad", id, body);
    [HttpDelete("Ciudades/{id}")] public Task<IActionResult> DeleteCiudad(int id) => EliminarEnTabla("CIUDAD", "Id_Ciudad", id);

    // BARRIOS
    [HttpGet("Barrios")] public Task<IActionResult> GetBarrios() => ObtenerTodo("BARRIO");
    [HttpPost("Barrios")] public Task<IActionResult> PostBarrio([FromBody] Dictionary<string, JsonElement> body) => InsertarEnTabla("BARRIO", body);
    [HttpPut("Barrios/{id}")] public Task<IActionResult> PutBarrio(int id, [FromBody] Dictionary<string, JsonElement> body) => ActualizarEnTabla("BARRIO", "Id_Barrio", id, body);
    [HttpDelete("Barrios/{id}")] public Task<IActionResult> DeleteBarrio(int id) => EliminarEnTabla("BARRIO", "Id_Barrio", id);

    // DIRECCIONES
    [HttpGet("Direcciones")] public Task<IActionResult> GetDirecciones() => ObtenerTodo("DIRECCION");
    [HttpPost("Direcciones")] public Task<IActionResult> PostDireccion([FromBody] Dictionary<string, JsonElement> body) => InsertarEnTabla("DIRECCION", body);
    [HttpPut("Direcciones/{id}")] public Task<IActionResult> PutDireccion(int id, [FromBody] Dictionary<string, JsonElement> body) => ActualizarEnTabla("DIRECCION", "Id_Direccion", id, body);
    [HttpDelete("Direcciones/{id}")] public Task<IActionResult> DeleteDireccion(int id) => EliminarEnTabla("DIRECCION", "Id_Direccion", id);

    // USUARIOS
    [HttpGet("Usuarios")] public Task<IActionResult> GetUsuarios() => ObtenerTodo("USUARIO");
    [HttpPost("Usuarios")] public Task<IActionResult> PostUsuario([FromBody] Dictionary<string, JsonElement> body) => InsertarEnTabla("USUARIO", body);
    [HttpPut("Usuarios/{id}")] public Task<IActionResult> PutUsuario(int id, [FromBody] Dictionary<string, JsonElement> body) => ActualizarEnTabla("USUARIO", "Id_Usuario", id, body);
    [HttpDelete("Usuarios/{id}")] public Task<IActionResult> DeleteUsuario(int id) => EliminarEnTabla("USUARIO", "Id_Usuario", id);
}