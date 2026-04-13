using System.Data;
using System.Text.Json;

public class JsonTypeHandler<T> : Dapper.SqlMapper.TypeHandler<T>
{
    public override void SetValue(IDbDataParameter parameter, T value)
    {
        parameter.Value = JsonSerializer.Serialize(value);
    }

    public override T Parse(object value)
    {
        return JsonSerializer.Deserialize<T>(value.ToString()!);
    }
}