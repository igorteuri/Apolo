// (internal on purpose: every project that needs it compiles its own copy)
// Stand-in for the old ServiceStack.Text library (version 3.9, whose free licence is not available for .NET 8).
// It only covers what Ares.Tags uses. The file format is JSON here instead of ServiceStack's "JSV" text format:
// export/import files of the old Editor are therefore NOT compatible between the two builds (the Editor is discontinued).
using System.IO;
using System.Text.Json;

namespace ServiceStack.Text
{
    internal static class TypeSerializer
    {
        private static readonly JsonSerializerOptions s_Options = new JsonSerializerOptions { IncludeFields = true, WriteIndented = false };

        public static void SerializeToWriter<T>(T value, TextWriter writer)
        {
            writer.Write(JsonSerializer.Serialize(value, s_Options));
        }

        public static string SerializeToString<T>(T value)
        {
            return JsonSerializer.Serialize(value, s_Options);
        }

        public static T DeserializeFromStream<T>(Stream stream)
        {
            return JsonSerializer.Deserialize<T>(stream, s_Options);
        }

        public static T DeserializeFromString<T>(string text)
        {
            return JsonSerializer.Deserialize<T>(text, s_Options);
        }
    }
}
