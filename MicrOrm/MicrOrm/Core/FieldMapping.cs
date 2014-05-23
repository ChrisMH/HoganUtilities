using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace MicrOrm.Core
{
    internal static class FieldMapping
    {
        internal static IDictionary<string, object> MapRowToDictionary(IDataReader rdr)
        {
            var result = new Dictionary<string, object>();
            for (var i = 0; i < rdr.FieldCount; i++)
            {
                result.Add(MapFieldNameToFriendlyName(rdr.GetName(i)),
                    rdr.IsDBNull(i) ? null : Convert.ChangeType(rdr[i], rdr.GetFieldType(i)));
            }
            return result;
        }

        internal static string MapFieldNameToFriendlyName(string fieldName)
        {
            if (fieldName == null) throw new ArgumentNullException("fieldName");

            var parts = fieldName.Split(new[] {'_'}, StringSplitOptions.RemoveEmptyEntries);

            return parts.Select(part => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(part.ToLower()))
                .Aggregate(String.Concat);
        }
    }
}