namespace ParquetViewer.Engine.ParquetNET
{
    internal static class Helpers
    {
        #region Dubious Functions
        //This logic is a cluster f... right now. It blends https://www.aloneguid.uk/posts/2023/04/parquet-empty-vs-null
        //with some of my understanding of how the dremel algorithm works. No way will it work for all cases.

        public static bool IsNull(this Parquet.Data.DataColumn dataColumn, int index, ParquetSchemaElement field)
            => dataColumn.IsNull(index, field.CurrentDefinitionLevel);

        public static bool IsEmpty(this Parquet.Data.DataColumn dataColumn, int index, ParquetSchemaElement field)
            => dataColumn.IsEmpty(index, field.CurrentDefinitionLevel, field.DataField?.MaxDefinitionLevel);

        //Overloads taking a precomputed definition level. CurrentDefinitionLevel walks the parent chain on
        //every read, so callers in per-element loops should hoist it out rather than recompute it each time.
        public static bool IsNull(this Parquet.Data.DataColumn dataColumn, int index, int currentDefinitionLevel)
            => dataColumn.DefinitionLevels?.Length > index && dataColumn.DefinitionLevels[index] <= currentDefinitionLevel - 1;

        public static bool IsEmpty(this Parquet.Data.DataColumn dataColumn, int index, int currentDefinitionLevel, int? maxDefinitionLevel)
            => dataColumn.DefinitionLevels?.Length > index && dataColumn.DefinitionLevels[index] == currentDefinitionLevel
                    && maxDefinitionLevel != dataColumn.DefinitionLevels[index] /*Fixes STRUCT_TYPE_TEST*/;
        #endregion

        /// <summary>
        /// Some parquet writers don't write null entries into the data array for empty and null lists.
        /// This throws off our logic so lets find all empty/null lists and add a null entry into 
        /// the data array to align it with the repetition/definition levels.
        /// </summary>
        /// <param name="dataColumn">The parquet data column</param>
        public static IEnumerable<object> GetDataWithPaddedNulls(this Parquet.Data.DataColumn dataColumn, ParquetSchemaElement field)
        {
            var dataEnumerable = dataColumn.Data.Cast<object?>().Select(d => d ?? DBNull.Value);

            int levelCount = dataColumn.DefinitionLevels?.Length ?? 0;
            if (levelCount > dataColumn.Data.Length)
            {
                //Hoisted out of the loop below: both walk the schema's parent chain on every access.
                var currentDefinitionLevel = field.CurrentDefinitionLevel;
                var maxDefinitionLevel = field.DataField?.MaxDefinitionLevel;

                dataEnumerable = GetDataWithPaddedNulls();

                IEnumerable<object> GetDataWithPaddedNulls()
                {
                    var index = -1;
                    foreach (var data in dataColumn.Data)
                    {
                        index++;

                        while (dataColumn.IsEmpty(index, currentDefinitionLevel, maxDefinitionLevel)
                            || dataColumn.IsNull(index, currentDefinitionLevel))
                        {
                            yield return DBNull.Value;
                            index++;
                        }

                        yield return data ?? DBNull.Value;
                    }

                    //Need to handle case where last N rows are null/empty
                    while (levelCount > index + 1)
                    {
                        yield return DBNull.Value;
                        index++;
                    }
                }
            }

            return dataEnumerable;
        }
    }
}