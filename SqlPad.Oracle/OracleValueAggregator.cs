using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SqlPad.Oracle.DatabaseConnection;
#if ORACLE_MANAGED_DATA_ACCESS_CLIENT
using Oracle.ManagedDataAccess.Types;
#else
using Oracle.DataAccess.Types;
#endif

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleValueAggregator (Count={Count}; DistinctCount={DistinctCount}; Minimum={Minimum}; Maximum={Maximum}; Sum={Sum}; Average={Average}; Median={Median})")]
	public class OracleValueAggregator : IValueAggregator
	{
		private readonly HashSet<object> _distinctValues = new HashSet<object>();
		private readonly List<Tuple<Type, object>> _typeValues = new List<Tuple<Type, object>>();

		private bool _isIndeterminate;
		private Type _valueType;
		private OracleDecimal _oracleNumberSum;
		private OracleIntervalYM _oracleYearToMonthSum;
		private OracleIntervalDS _oracleDayToSecondSum;

		private IEnumerable<object> SourceValues =>
			_typeValues
				.Where(t => t.Item1 == _valueType)
				.Select(t => t.Item2);

		private IReadOnlyList<object> OrderedSourceValues => SourceValues.OrderBy(SelectComparable).ToArray();

		public object Minimum => OrderedSourceValues.FirstOrDefault();

		public object Maximum => OrderedSourceValues.LastOrDefault();

		public object Average => GetValue(_oracleNumberSum / Count, OracleDate.Null, OracleTimeStamp.Null, OracleTimeStampTZ.Null, OracleTimeStampLTZ.Null, _oracleYearToMonthSum / (int)Count, _oracleDayToSecondSum / (int)Count);

		public object Sum => GetValue(_oracleNumberSum, OracleDate.Null, OracleTimeStamp.Null, OracleTimeStampTZ.Null, OracleTimeStampLTZ.Null, _oracleYearToMonthSum, _oracleDayToSecondSum);

		public Mode Mode
		{
			get
			{
				var modeValues =
					SourceValues
						.GroupBy(t => t)
						.Select(g => new Mode { Value = g.Key, Count = g.Count() })
						.OrderByDescending(g => g.Count)
						.Take(2)
						.ToArray();

				if (modeValues.Length == 0)
				{
					return Mode.Empty;
				}

				var mostNumerousItem = modeValues[0];

				if (modeValues.Length == 1)
				{
					return mostNumerousItem;
				}

				return mostNumerousItem.Count == modeValues[1].Count
					? Mode.Empty
					: mostNumerousItem;
			}
		}

		public object Median
		{
			get
			{
				var values = OrderedSourceValues;
				if (values.Count == 0)
				{
					return null;
				}

				var middleIndex = (values.Count - 1) / 2;
				var middleValue = values[middleIndex];

				if (values.Count % 2 != 0)
				{
					return middleValue;
				}

				var aggregator = new OracleValueAggregator();
				aggregator.AddValue(middleValue);
				aggregator.AddValue(values[middleIndex + 1]);

				return aggregator.Average;
			}
		}

		public long Count { get; private set; }

		public long? DistinctCount =>
			_isIndeterminate
				? (long?)null
				: _distinctValues.Count;

		public bool AggregatedValuesAvailable { get; private set; }

		public bool LimitValuesAvailable { get; private set; }

		public OracleValueAggregator()
		{
			Reset();
		}

		public void AddValue(object value)
		{
			if (value == null || value == DBNull.Value)
			{
				return;
			}

			if (value is IValue oracleValue && oracleValue.IsNull)
			{
				return;
			}

			Type valueType;

			var oracleNumber = value as OracleNumber;
			if (value is int || value is long || value is decimal || value is short || value is uint || value is ulong || value is ushort)
			{
				value = oracleNumber = new OracleNumber(new OracleDecimal(Convert.ToDecimal(value)));
			}

			var oracleDate = value as OracleDateTime;
			if (value is DateTime)
			{
				value = oracleDate = new OracleDateTime(new OracleDate(Convert.ToDateTime(value)));
			}

			Count++;

			_distinctValues.Add(value);

			var oracleTimestamp = value as OracleTimestamp;
			var oracleTimestampTimezone = value as OracleTimestampWithTimeZone;
			var oracleTimestampLocalTimezone = value as OracleTimestampWithLocalTimeZone;
			var oracleIntervalYearToMonth = value as OracleIntervalYearToMonth;
			var oracleIntervalDayToSecond = value as OracleIntervalDayToSecond;
			if (oracleNumber != null)
			{
				valueType = typeof(OracleDecimal);
				var typedValue = (OracleDecimal)oracleNumber.RawValue;
				_oracleNumberSum += typedValue;
			}
			else if (oracleDate != null)
			{
				valueType = typeof(OracleDate);
				AggregatedValuesAvailable = false;
			}
			else if (oracleTimestamp != null)
			{
				valueType = typeof(OracleTimeStamp);
				AggregatedValuesAvailable = false;
			}
			else if (oracleTimestampTimezone != null)
			{
				valueType = typeof(OracleTimeStampTZ);
				AggregatedValuesAvailable = false;
			}
			else if (oracleTimestampLocalTimezone != null)
			{
				valueType = typeof(OracleTimeStampLTZ);
				AggregatedValuesAvailable = false;
			}
			else if (oracleIntervalYearToMonth != null)
			{
				valueType = typeof(OracleIntervalYM);
				var typedValue = (OracleIntervalYM)oracleIntervalYearToMonth.RawValue;
				_oracleYearToMonthSum += typedValue;
			}
			else if (oracleIntervalDayToSecond != null)
			{
				valueType = typeof(OracleIntervalDS);
				var typedValue = (OracleIntervalDS)oracleIntervalDayToSecond.RawValue;
				_oracleDayToSecondSum += typedValue;
			}
			else
			{
				valueType = typeof(object);
				AggregatedValuesAvailable = false;
				LimitValuesAvailable = false;
			}

			if (LimitValuesAvailable)
			{
				_typeValues.Add(Tuple.Create(valueType, value));
			}

			if (_valueType == null)
			{
				_valueType = valueType;
			}
			else if (_valueType != valueType)
			{
				SetIndeterminate();
			}
		}

		private object GetValue(OracleDecimal number, OracleDate date, OracleTimeStamp timestamp, OracleTimeStampTZ timestampWithTimezone, OracleTimeStampLTZ timestampWithLocalTimeZone, OracleIntervalYM yearToMonth, OracleIntervalDS dayToSecond)
		{
			if (!LimitValuesAvailable)
			{
				return null;
			}

			if (_valueType == typeof(OracleDecimal))
			{
				return number.IsNull ? null : new OracleNumber(number);
			}

			if (_valueType == typeof(OracleDate))
			{
				return date.IsNull ? null : new OracleDateTime(date);
			}

			if (_valueType == typeof(OracleTimeStamp))
			{
				return timestamp.IsNull ? null : new OracleTimestamp(timestamp);
			}

			if (_valueType == typeof(OracleTimeStampTZ))
			{
				return timestampWithTimezone.IsNull ? null : new OracleTimestampWithTimeZone(timestampWithTimezone);
			}

			if (_valueType == typeof(OracleTimeStampLTZ))
			{
				return timestampWithLocalTimeZone.IsNull ? null : new OracleTimestampWithLocalTimeZone(timestampWithLocalTimeZone);
			}

			if (_valueType == typeof(OracleIntervalYM))
			{
				return yearToMonth.IsNull ? null : new OracleIntervalYearToMonth(yearToMonth);
			}

			if (_valueType == typeof(OracleIntervalDS))
			{
				return dayToSecond.IsNull ? null : new OracleIntervalDayToSecond(dayToSecond);
			}

			return null;
		}

		private void Reset()
		{
			_valueType = null;
			_distinctValues.Clear();
			SetIndeterminate();

			AggregatedValuesAvailable = true;
			LimitValuesAvailable = true;
			_isIndeterminate = false;
		}

		private void SetIndeterminate()
		{
			AggregatedValuesAvailable = false;
			LimitValuesAvailable = false;

			_oracleNumberSum = OracleDecimal.Zero;
			_oracleYearToMonthSum = OracleIntervalYM.Zero;
			_oracleDayToSecondSum = OracleIntervalDS.Zero;
			_typeValues.Clear();
			_isIndeterminate = true;
		}

		private static object SelectComparable(object value)
		{
			var sqlPadValue = value as IValue;
			value = sqlPadValue == null
				? value
				: sqlPadValue.RawValue;

			return value as IComparable;
		}
	}
}
