using System;
using System.Collections.Generic;
using System.Diagnostics;
using SqlPad.Oracle.DatabaseConnection;
#if ORACLE_MANAGED_DATA_ACCESS_CLIENT
using Oracle.ManagedDataAccess.Types;
#else
using Oracle.DataAccess.Types;
#endif

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleValueAggregator (Count={Count}; Minimum={Minimum}; Maximum={Maximum}; Sum={Sum}; Average={Average})")]
	public class OracleValueAggregator : IValueAggregator
	{
		private readonly HashSet<object> _distinctValues = new HashSet<object>();
		private ValueType _valueType;
		private OracleDecimal _oracleNumberSum;
		private OracleIntervalYM _oracleYearToMonthSum;
		private OracleIntervalDS _oracleDayToSecondSum;
		private OracleDecimal _oracleNumberMinimum;
		private OracleDate _oracleDateMinimum;
		private OracleTimeStamp _oracleTimeStampMinimum;
		private OracleTimeStampLTZ _oracleTimeStampLocalTimezoneMinimum;
		private OracleTimeStampTZ _oracleTimeStampTimezoneMinimum;
		private OracleIntervalYM _oracleYearToMonthMinimum;
		private OracleIntervalDS _oracleDayToSecondMinimum;
		private OracleDecimal _oracleNumberMaximum;
		private OracleDate _oracleDateMaximum;
		private OracleIntervalYM _oracleYearToMonthMaximum;
		private OracleIntervalDS _oracleDayToSecondMaximum;
		private OracleTimeStamp _oracleTimeStampMaximum;
		private OracleTimeStampLTZ _oracleTimeStampLocalTimezoneMaximum;
		private OracleTimeStampTZ _oracleTimeStampTimezoneMaximum;

		public object Minimum => GetValue(_oracleNumberMinimum, _oracleDateMinimum, _oracleTimeStampMinimum, _oracleTimeStampTimezoneMinimum, _oracleTimeStampLocalTimezoneMinimum, _oracleYearToMonthMinimum, _oracleDayToSecondMinimum);

		public object Maximum => GetValue(_oracleNumberMaximum, _oracleDateMaximum, _oracleTimeStampMaximum, _oracleTimeStampTimezoneMaximum, _oracleTimeStampLocalTimezoneMaximum, _oracleYearToMonthMaximum, _oracleDayToSecondMaximum);

		public object Average => GetValue(_oracleNumberSum / Count, OracleDate.Null, OracleTimeStamp.Null, OracleTimeStampTZ.Null, OracleTimeStampLTZ.Null, _oracleYearToMonthSum / (int)Count, _oracleDayToSecondSum / (int)Count);

		public object Sum => GetValue(_oracleNumberSum, OracleDate.Null, OracleTimeStamp.Null, OracleTimeStampTZ.Null, OracleTimeStampLTZ.Null, _oracleYearToMonthSum, _oracleDayToSecondSum);

		public long Count { get; private set; }

		public long DistinctCount => _distinctValues.Count;

		public bool AggregatedValuesAvailable { get; private set; } = true;

		public bool LimitValuesAvailable { get; private set; } = true;

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

			var oracleValue = value as IValue;
			if (oracleValue != null && oracleValue.IsNull)
			{
				return;
			}

			Count++;

			var valueType = ValueType.None;

			var oracleNumber = value as OracleNumber;
			if (value is int || value is long || value is decimal || value is short || value is uint || value is ulong || value is ushort)
			{
				valueType = ValueType.Number;
				value = oracleNumber = new OracleNumber(new OracleDecimal(Convert.ToDecimal(value)));
			}

			var oracleDate = value as OracleDateTime;
			if (value is DateTime)
			{
				value = oracleDate = new OracleDateTime(new OracleDate(Convert.ToDateTime(value)));
			}

			_distinctValues.Add(value);

			var oracleTimestamp = value as OracleTimestamp;
			var oracleTimestampTimezone = value as OracleTimestampWithTimeZone;
			var oracleTimestampLocalTimezone = value as OracleTimestampWithLocalTimeZone;
			var oracleIntervalYearToMonth = value as OracleIntervalYearToMonth;
			var oracleIntervalDayToSecond = value as OracleIntervalDayToSecond;
			if (oracleNumber != null)
			{
				valueType = ValueType.Number;
				var typedValue = (OracleDecimal)oracleNumber.RawValue;
				_oracleNumberMinimum = OracleDecimal.Min(_oracleNumberMinimum, typedValue);
				_oracleNumberMaximum = OracleDecimal.Max(_oracleNumberMaximum, typedValue);
				_oracleNumberSum += typedValue;
			}
			else if (oracleDate != null)
			{
				valueType = ValueType.Date;
				var typedValue = (OracleDate)oracleDate.RawValue;
				if (OracleDate.LessThan(typedValue, _oracleDateMinimum))
				{
					_oracleDateMinimum = typedValue;
				}

				if (OracleDate.GreaterThan(typedValue, _oracleDateMaximum))
				{
					_oracleDateMaximum = typedValue;
				}

				AggregatedValuesAvailable = false;
			}
			else if (oracleTimestamp != null)
			{
				valueType = ValueType.Timestamp;
				var typedValue = (OracleTimeStamp)oracleTimestamp.RawValue;
				if (OracleTimeStamp.LessThan(typedValue, _oracleTimeStampMinimum))
				{
					_oracleTimeStampMinimum = typedValue;
				}

				if (OracleTimeStamp.GreaterThan(typedValue, _oracleTimeStampMaximum))
				{
					_oracleTimeStampMaximum = typedValue;
				}

				AggregatedValuesAvailable = false;
			}
			else if (oracleTimestampTimezone != null)
			{
				valueType = ValueType.TimestampWithTimezone;
				var typedValue = (OracleTimeStampTZ)oracleTimestampTimezone.RawValue;
				if (OracleTimeStampTZ.LessThan(typedValue, _oracleTimeStampTimezoneMinimum))
				{
					_oracleTimeStampTimezoneMinimum = typedValue;
				}

				if (OracleTimeStampTZ.GreaterThan(typedValue, _oracleTimeStampTimezoneMaximum))
				{
					_oracleTimeStampTimezoneMaximum = typedValue;
				}

				AggregatedValuesAvailable = false;
			}
			else if (oracleTimestampLocalTimezone != null)
			{
				valueType = ValueType.TimestampWithLocalTimezone;
				var typedValue = (OracleTimeStampLTZ)oracleTimestampLocalTimezone.RawValue;
				if (OracleTimeStampLTZ.LessThan(typedValue, _oracleTimeStampLocalTimezoneMinimum))
				{
					_oracleTimeStampLocalTimezoneMinimum = typedValue;
				}

				if (OracleTimeStampLTZ.GreaterThan(typedValue, _oracleTimeStampLocalTimezoneMaximum))
				{
					_oracleTimeStampLocalTimezoneMaximum = typedValue;
				}

				AggregatedValuesAvailable = false;
			}
			else if (oracleIntervalYearToMonth != null)
			{
				valueType = ValueType.IntervalYearToMonth;
				var typedValue = (OracleIntervalYM)oracleIntervalYearToMonth.RawValue;
				if (OracleIntervalYM.LessThan(typedValue, _oracleYearToMonthMinimum))
				{
					_oracleYearToMonthMinimum = typedValue;
				}

				if (OracleIntervalYM.GreaterThan(typedValue, _oracleYearToMonthMaximum))
				{
					_oracleYearToMonthMaximum = typedValue;
				}

				_oracleYearToMonthSum += typedValue;
			}
			else if (oracleIntervalDayToSecond != null)
			{
				valueType = ValueType.IntervalDayToSecond;
				var typedValue = (OracleIntervalDS)oracleIntervalDayToSecond.RawValue;

				if (OracleIntervalDS.LessThan(typedValue, _oracleDayToSecondMinimum))
				{
					_oracleDayToSecondMinimum = typedValue;
				}

				if (OracleIntervalDS.GreaterThan(typedValue, _oracleDayToSecondMaximum))
				{
					_oracleDayToSecondMaximum = typedValue;
				}

				_oracleDayToSecondSum += typedValue;
			}
			else
			{
				AggregatedValuesAvailable = false;
				LimitValuesAvailable = false;
			}

			if (_valueType == ValueType.None)
			{
				_valueType = valueType;
			}
			else if (_valueType != valueType)
			{
				AggregatedValuesAvailable = false;
				LimitValuesAvailable = false;
				Reset();
			}
		}

		private object GetValue(OracleDecimal number, OracleDate date, OracleTimeStamp timestamp, OracleTimeStampTZ timestampWithTimezone, OracleTimeStampLTZ timestampWithLocalTimeZone, OracleIntervalYM yearToMonth, OracleIntervalDS dayToSecond)
		{
			switch (_valueType)
			{
				case ValueType.Number:
					return number.IsNull ? null : new OracleNumber(number);
				case ValueType.Date:
					return date.IsNull ? null : new OracleDateTime(date);
				case ValueType.Timestamp:
					return timestamp.IsNull ? null : new OracleTimestamp(timestamp);
				case ValueType.TimestampWithTimezone:
					return timestampWithTimezone.IsNull ? null : new OracleTimestampWithTimeZone(timestampWithTimezone);
				case ValueType.TimestampWithLocalTimezone:
					return timestampWithLocalTimeZone.IsNull ? null : new OracleTimestampWithLocalTimeZone(timestampWithLocalTimeZone);
				case ValueType.IntervalYearToMonth:
					return yearToMonth.IsNull ? null : new OracleIntervalYearToMonth(yearToMonth);
				case ValueType.IntervalDayToSecond:
					return dayToSecond.IsNull ? null : new OracleIntervalDayToSecond(dayToSecond);
				default:
					return null;
			}
		}

		private void Reset()
		{
			_valueType = ValueType.None;
			_oracleNumberSum = OracleDecimal.Zero;
			_oracleYearToMonthSum = OracleIntervalYM.Zero;
			_oracleDayToSecondSum = OracleIntervalDS.Zero;
			_oracleNumberMinimum = OracleDecimal.MaxValue;
			_oracleDateMinimum = OracleDate.MaxValue;
			_oracleTimeStampMinimum = OracleTimeStamp.MaxValue;
			_oracleTimeStampTimezoneMinimum = OracleTimeStampTZ.MaxValue;
			_oracleTimeStampLocalTimezoneMinimum = OracleTimeStampLTZ.MaxValue;
			_oracleYearToMonthMinimum = OracleIntervalYM.MaxValue;
			_oracleDayToSecondMinimum = OracleIntervalDS.MaxValue;
			_oracleNumberMaximum = OracleDecimal.MinValue;
			_oracleDateMaximum = OracleDate.MinValue;
			_oracleTimeStampMaximum = OracleTimeStamp.MinValue;
			_oracleTimeStampTimezoneMaximum = OracleTimeStampTZ.MinValue;
			_oracleTimeStampLocalTimezoneMaximum = OracleTimeStampLTZ.MinValue;
			_oracleYearToMonthMaximum = OracleIntervalYM.MinValue;
			_oracleDayToSecondMaximum = OracleIntervalDS.MinValue;
			_distinctValues.Clear();
		}

		private enum ValueType
		{
			None,
			Number,
			Date,
			Timestamp,
			TimestampWithTimezone,
			TimestampWithLocalTimezone,
			IntervalYearToMonth,
			IntervalDayToSecond
		}
	}
}
