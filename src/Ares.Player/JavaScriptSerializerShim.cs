// Stand-in for System.Web.Script.Serialization.JavaScriptSerializer (not part of .NET 8), built on System.Text.Json.
// DeserializeObject returns what the old class returned: Dictionary<string, object>, ArrayList, string, int/long/double, bool, null.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;

namespace System.Web.Script.Serialization
{
    public class JavaScriptSerializer
    {
        public int MaxJsonLength { get; set; }
        public int RecursionLimit { get; set; }

        public JavaScriptSerializer()
        {
            MaxJsonLength = 2097152;
            RecursionLimit = 100;
        }

        public string Serialize(object value)
        {
            return JsonSerializer.Serialize(value, value == null ? typeof(object) : value.GetType(),
                new JsonSerializerOptions { IncludeFields = false });
        }

        public object DeserializeObject(string json)
        {
            using (JsonDocument doc = JsonDocument.Parse(json))
                return Convert(doc.RootElement);
        }

        public T Deserialize<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true, IncludeFields = true });
        }

        private static object Convert(JsonElement e)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Object:
                    Dictionary<string, object> d = new Dictionary<string, object>();
                    foreach (JsonProperty p in e.EnumerateObject())
                        d[p.Name] = Convert(p.Value);
                    return d;
                case JsonValueKind.Array:
                    ArrayList l = new ArrayList();
                    foreach (JsonElement item in e.EnumerateArray())
                        l.Add(Convert(item));
                    return l;
                case JsonValueKind.String:
                    return e.GetString();
                case JsonValueKind.Number:
                    int i;
                    if (e.TryGetInt32(out i)) return i;
                    long n;
                    if (e.TryGetInt64(out n)) return n;
                    return e.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                default:
                    return null;
            }
        }
    }
}
