using System.Globalization;
using System.Text.Json.Nodes;

namespace Tandur.Core
{
    /// <summary>
    /// Konversi angka JSON yang toleran: JsonValue hasil parse (JsonElement) dan JsonValue buatan C#
    /// (int/float/double) diperlakukan sama, seperti angka di Python.
    /// </summary>
    public static class J
    {
        public static double? Dn(JsonNode? n)
        {
            if (n is not JsonValue v) return null;
            if (v.TryGetValue(out double d)) return d;
            if (v.TryGetValue(out int i)) return i;
            if (v.TryGetValue(out long l)) return l;
            if (v.TryGetValue(out float f)) return f;
            if (v.TryGetValue(out decimal m)) return (double)m;
            if (v.TryGetValue(out bool b)) return b ? 1 : 0;
            if (v.TryGetValue(out string? s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var p)) return p;
            return null;
        }

        public static double D(JsonNode? n) => Dn(n) ?? throw new InvalidOperationException($"Bukan angka: {n?.ToJsonString() ?? "null"}");
        public static float F(JsonNode? n) => (float)D(n);
        public static float? Fn(JsonNode? n) => Dn(n) is double d ? (float)d : null;
        public static int I(JsonNode? n) => (int)D(n);
        public static int? In(JsonNode? n) => Dn(n) is double d ? (int)d : null;
    }
}
