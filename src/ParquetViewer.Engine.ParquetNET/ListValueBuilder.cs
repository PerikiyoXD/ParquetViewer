using ParquetViewer.Engine.ParquetNET.Types;
using ParquetViewer.Engine.Types;
using System.Collections;

namespace ParquetViewer.Engine.ParquetNET
{
    public class ListValueBuilder
    {
        private readonly int[] _repetitionLevels;
        private readonly int[] _definitionLevels;
        private readonly IEnumerable<object> _data;
        private readonly Type _type;

        public ListValueBuilder(int[] repetitionLevels, int[] definitionLevels, IEnumerable<object> data, Type type)
        {
            ArgumentNullException.ThrowIfNull(definitionLevels);
            ArgumentNullException.ThrowIfNull(repetitionLevels);
            ArgumentNullException.ThrowIfNull(data);
            ArgumentNullException.ThrowIfNull(type);

            if (type == typeof(byte[]))
                _type = typeof(ByteArrayValue);
            else
                _type = type;

            //We assume they all have the same length
            _definitionLevels = definitionLevels;
            _repetitionLevels = repetitionLevels;
            _data = data;
        }

        private IEnumerable<Range> GetRowRanges()
        {
            int startIndex = 0;
            int endIndex = 0;
            for (int i = 1; i < _repetitionLevels.Length; i++)
            {
                if (_repetitionLevels[i] == 0)
                {
                    endIndex = i;
                    yield return new(startIndex, endIndex);

                    startIndex = i;
                }
            }

            yield return new(startIndex, _repetitionLevels.Length);
        }

        /// <summary>
        /// Reads nested list values
        /// </summary>
        /// <returns>Enumerable of ListValue's. We need to return object to support DBNull.Value</returns>
        public IEnumerable<object> ReadRows(int skipRecords, int readRecords, int numberOfListParents, int currentDefinitionLevel, int maxDefinitionLevel, CancellationToken cancellationToken)
        {
            //Materialize the data once up front. It arrives as a lazy LINQ chain, so slicing it per row
            //used to re-run the whole projection from index 0 for every row, making this quadratic.
            var data = _data
                .Select(data => data is byte[] bytes ? new ByteArrayValue(bytes) : data) //Need to handle byte array type separately
                .ToArray();

            var rowRangesToRead = GetRowRanges().Skip(skipRecords).Take(readRecords);
            foreach (var rowRange in rowRangesToRead)
            {
                cancellationToken.ThrowIfCancellationRequested();

                //Ranges are derived from the repetition levels, which can be longer than the data array.
                //ReadListValue still gets the unclamped range since it slices the repetition levels with it,
                //but the data slice is clamped to match what the previous Skip/Take would have returned.
                var dataStart = Math.Min(rowRange.Start.Value, data.Length);
                var dataEnd = Math.Min(rowRange.End.Value, data.Length);
                var listValue = ReadListValue(rowRange, numberOfListParents, () => data[dataStart..dataEnd],
                (int index) =>
                {
                    return _definitionLevels.Length > index && _definitionLevels[index] == currentDefinitionLevel;
                });
                yield return listValue;
            }
        }

        private object ReadListValue(Range range, int numberOfListParents, Func<object[]> dataProvider, Func<int, bool> isEmptyProvider)
        {
            var rangeRepetition = _repetitionLevels.AsSpan(range);
            var rangeData = dataProvider.Invoke();

            if (rangeData.All(data => data == DBNull.Value))
            {
                if (isEmptyProvider(range.Start.Value))
                {
                    return new ListValue([], _type);
                }
                else
                {
                    return DBNull.Value;
                }
            }

            LinkedArrayList root = new();
            var node = root.GoDownToLevel(numberOfListParents);

            //First data point is always added to the most nested list
            node.Add(rangeData[0]);

            //Add everything else now
            for (var index = 1; index < rangeRepetition.Length; index++)
            {
                var repetitionLevel = rangeRepetition[index];
                var data = rangeData[index];

                if (repetitionLevel == numberOfListParents)
                {
                    //We're still in the same level, append to the current list
                    node.Add(data);
                    continue;
                }

                if (repetitionLevel == numberOfListParents - 1)
                {
                    node = node.NextList();
                    node.Add(data);
                    node.IsNull = data == DBNull.Value && !isEmptyProvider(range.Start.Value + index);
                    node.IsEmpty = data == DBNull.Value && isEmptyProvider(range.Start.Value + index);
                    continue;
                }

                var count = numberOfListParents;
                while (repetitionLevel < count - 1)
                {
                    node = node.Parent!;
                    count--;
                }

                node = node.NextList();
                node = node.GoDownToLevel(numberOfListParents);
                node.Add(data);
                node.IsNull = data == DBNull.Value && !isEmptyProvider(range.Start.Value + index);
                node.IsEmpty = data == DBNull.Value && isEmptyProvider(range.Start.Value + index);
            }

            return ConstructListValues(root);
        }

        private object ConstructListValues(LinkedArrayList array)
        {
            if (array.IsNull)
                return DBNull.Value;

            var hasChildArrays = false;
            var convertedArray = new ArrayList();

            if (!array.IsEmpty)
            {
                foreach (var data in array)
                {
                    if (data is LinkedArrayList childArray)
                    {
                        convertedArray.Add(ConstructListValues(childArray));
                        hasChildArrays = true;
                    }
                    else
                    {
                        convertedArray.Add(data);
                    }
                }
            }

            var type = hasChildArrays ? typeof(ListValue) : _type;
            return new ListValue(convertedArray, type);
        }

        private class LinkedArrayList : ArrayList
        {
            public LinkedArrayList? Parent { get; set; }
            public int Level { get; private set; }
            public bool IsNull { get; set; }
            public bool IsEmpty { get; set; }

            public LinkedArrayList()
            {
                Level = 1;
                Parent = null;
            }

            private LinkedArrayList(int level, LinkedArrayList? parent = null)
            {
                Level = level;
                Parent = parent;
            }

            public LinkedArrayList GoDownToLevel(int level)
            {
                var node = this;
                while (node.Level < level)
                {
                    var childNode = new LinkedArrayList(node.Level + 1, node);
                    node.Add(childNode);
                    node = childNode;
                }
                return node;
            }

            public LinkedArrayList NextList()
            {
                if (this.Parent is null)
                    throw new InvalidOperationException();

                var nextList = new LinkedArrayList(this.Level, this.Parent);
                this.Parent.Add(nextList);
                return nextList;
            }
        }
    }
}