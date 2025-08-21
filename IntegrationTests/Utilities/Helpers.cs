using Microsoft.EntityFrameworkCore;

namespace IntegrationTests.Utilities;

public static class Helpers
{
    public static async Task<bool> TableExistsAsync(this DbContext context, string tableName)
    {
        var sql = $"SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = '{tableName.ToLower()}')";
        var conn = context.Database.GetDbConnection();
        await conn.OpenAsync();

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            var result = await cmd.ExecuteScalarAsync();
            return result != null && (bool)result;
        }
        finally
        {
            await conn.CloseAsync();
        }
    }
}