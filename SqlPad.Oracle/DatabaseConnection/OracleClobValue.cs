using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
#if ORACLE_MANAGED_DATA_ACCESS_CLIENT
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
#else
using Oracle.DataAccess.Client;
using Oracle.DataAccess.Types;
#endif

using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;

namespace SqlPad.Oracle.DatabaseConnection
{
	public abstract class OracleLargeTextValue : ILargeTextValue
	{
		public const int DefaultPreviewLength = 1023;

		private string _preview;
		private string _value;

		public bool IsEditable => false;

		public abstract string DataTypeName { get; }

		public abstract object RawValue { get; }

		public abstract bool IsNull { get; }

		public abstract string ToSqlLiteral();

		public string ToXml() => $"<![CDATA[{Value.ToXmlCompliant()}]]>";

		public string ToJson() => IsNull ? "null" : $"\"{Value.Replace("\"", "\\\"")}\"";

		public abstract long Length { get; }

		public virtual int PreviewLength => DefaultPreviewLength;

		public string Preview => _preview ?? BuildPreview();

		private string BuildPreview()
		{
			if (IsNull)
			{
				return String.Empty;
			}

			var builder = new StringBuilder(PreviewLength + 1);
			GetChunk(builder, 0, PreviewLength + 1);
			
			var preview = builder.ToString();
			
			var indexFirstLineBreak = preview.IndexOf('\n', 0, preview.Length < PreviewLength ? preview.Length : PreviewLength);
			if (preview.Length > PreviewLength || indexFirstLineBreak != -1)
			{
				preview = $"{preview.Substring(0, indexFirstLineBreak != -1 ? indexFirstLineBreak : PreviewLength)}{CellValueConverter.Ellipsis}";
			}

			return _preview = preview;
		}

		public string Value
		{
			get
			{
				Prefetch();

				return _value;
			}
		}

		protected abstract string GetValue();

		public abstract void GetChunk(StringBuilder stringBuilder, int offset, int length);

		public override string ToString() => Preview;

		public void Prefetch()
		{
			if (_value != null)
			{
				return;
			}
			
			if (_preview == null)
			{
				BuildPreview();
			}

			_value = IsNull ? String.Empty : GetValue();
		}

		protected bool Equals(OracleLargeTextValue other) => string.Equals(Value, other.Value);

		public override bool Equals(object obj)
		{
			if (obj is null) return false;
			if (ReferenceEquals(this, obj)) return true;
			return obj.GetType() == GetType() && Equals((OracleLargeTextValue)obj);
		}

		public override int GetHashCode() => Value.GetHashCode();
	}

#if !ORACLE_MANAGED_DATA_ACCESS_CLIENT
	public class OracleXmlValue : OracleLargeTextValue, IDisposable
	{
		private readonly OracleXmlType _xmlType;

		public override string DataTypeName => TerminalValues.XmlType;

		public override long Length => Value.Length;

		public override bool IsNull => _xmlType.IsNull;

		public override string ToSqlLiteral() =>
			IsNull
				? TerminalValues.Null
				: $"{TerminalValues.XmlType}('{Value.Replace("'", "''")}')";

		public override int PreviewLength { get; }

		public override object RawValue => _xmlType;

		protected override string GetValue() => _xmlType.Value;

		public OracleXmlValue(OracleXmlType xmlType, int previewLength = DefaultPreviewLength)
		{
			_xmlType = xmlType;
			PreviewLength = previewLength;
		}

		public override void GetChunk(StringBuilder stringBuilder, int offset, int length)
		{
			var characters = new char[length];
			var characterCount = _xmlType.GetStream().Read(characters, 0, length);
			stringBuilder.Append(characters, 0, characterCount);
		}

		public void Dispose() => _xmlType.Dispose();
	}
#endif

	public class OracleSimpleValue : ILargeTextValue, IComparable
	{
		public OracleSimpleValue(object value)
		{
			RawValue = value;
			Preview = Convert.ToString(value);
			IsNull = value == DBNull.Value;
		}

		public object RawValue { get; }

		public bool IsNull { get; }

		public string ToSqlLiteral() => IsNull ? TerminalValues.Null : $"'{Preview.Replace("'", "''")}'";

		public string ToXml() => Preview.ToXmlCompliant();

		public string ToJson() => IsNull ? "null" : $"\"{Value.Replace("\"", "\\\"")}\"";

		public string DataTypeName => throw new NotImplementedException();

		public long Length => Preview.Length;

	    public void GetChunk(StringBuilder stringBuilder, int offset, int length) => stringBuilder.Append(Preview.Substring(offset, length));

		public bool IsEditable => false;

	    public void Prefetch() { }

		public string Preview { get; }

		public string Value => Preview;

		public override string ToString() => Preview;

		public int CompareTo(object obj) => String.CompareOrdinal(Preview, Convert.ToString(obj));

		protected bool Equals(OracleSimpleValue other) => Equals(RawValue, other.RawValue);

		public override bool Equals(object obj)
		{
			if (obj is null) return false;
			if (ReferenceEquals(this, obj)) return true;
			return obj.GetType() == GetType() && Equals((OracleSimpleValue)obj);
		}

		public override int GetHashCode() => RawValue.GetHashCode();
	}

	public class OracleClobValue : OracleLargeTextValue, IDisposable
	{
		private const int MaximumChunkSize = 16777216;

		private readonly OracleClob _clob;

		public override string DataTypeName { get; }

		public override long Length => _clob.Length;

		public override object RawValue => _clob;

		public override bool IsNull => _clob.IsNull || _clob.IsEmpty;

		public override string ToSqlLiteral() =>
			IsNull
				? TerminalValues.Null
				: $"TO_{(_clob.IsNClob ? "N" : null)}CLOB('{Value.Replace("'", "''")}')";

		public override int PreviewLength { get; }

	    protected override string GetValue()
		{
			var totalLength = (int)_clob.Length / 2;
			var builder = new StringBuilder(totalLength);
			var offset = 0;

			_clob.Seek(0, SeekOrigin.Begin);

			int remainingLength;
			while ((remainingLength = totalLength - offset) > 0)
			{
				var chunkLength = Math.Min(MaximumChunkSize, remainingLength);
				GetChunk(builder, 0, chunkLength);
				offset = builder.Length;
			}

			return builder.ToString();
		}

		public OracleClobValue(string dataTypeName, OracleClob clob, int previewLength = DefaultPreviewLength)
		{
			_clob = clob;
			PreviewLength = previewLength;
			DataTypeName = dataTypeName;
		}

		public override void GetChunk(StringBuilder stringBuilder, int offset, int length)
		{
			var characters = new char[length];
			var characterCount = _clob.Read(characters, offset, length);
			stringBuilder.Append(characters, 0, characterCount);
		}

		public void Dispose() => _clob.Dispose();
	}

	public class OracleExternalBinaryFile : ILargeBinaryValue, IDisposable
	{
		private readonly OracleBFile _bfile;
		private byte[] _value;

		public bool IsNull { get; }

		public OracleExternalBinaryFile(OracleBFile bfile)
		{
			_bfile = bfile;
			bfile.OpenFile();
			IsNull = bfile.IsNull || bfile.IsEmpty;
		}

		public string ToSqlLiteral() => "NULL /* unsupported */";

		public string ToXml() => OracleRawValue.ToXml(Value);

		public string ToJson() => OracleRawValue.ToJson(Value);

		public object RawValue => _bfile;

		public string DataTypeName { get; } = OracleDatabaseModelBase.BuiltInDataTypeBFile;

		public bool IsEditable { get; } = false;

		public long Length => _bfile.Length;

		public void Prefetch()
		{
			if (_value != null)
				return;

			_value =
				_bfile.IsNull || _bfile.IsEmpty
					? new byte[0]
					: _bfile.Value;
		}

		public byte[] Value
		{
			get
			{
				Prefetch();

				return _value;
			}
		}

		public byte[] GetChunk(int bytes)
		{
			var buffer = new byte[bytes];
			var bytesRead = _bfile.Read(buffer, 0, bytes);
			var result = new byte[bytesRead];
			Array.Copy(buffer, 0, result, 0, bytesRead);
			return result;
		}

		public void Dispose() => _bfile.Dispose();

		public override string ToString() => _bfile.IsNull ? String.Empty : $"(BFILE[{Length:N0} B])";

		protected bool Equals(OracleExternalBinaryFile other) => Equals(_bfile, other._bfile);

		public override bool Equals(object obj)
		{
			if (obj is null) return false;
			if (ReferenceEquals(this, obj)) return true;
			return obj.GetType() == GetType() && Equals((OracleExternalBinaryFile)obj);
		}

		public override int GetHashCode() => _bfile.GetHashCode();
	}

	public class OracleBlobValue : ILargeBinaryValue, IDisposable
	{
		private readonly OracleBlob _blob;
		private byte[] _value;

		public string DataTypeName { get; } = TerminalValues.Blob;

		public bool IsEditable { get; } = false;

		public bool IsNull { get; }

		public object RawValue => _blob;

		public string ToSqlLiteral() =>
			IsNull
				? TerminalValues.Null
				: $"TO_BLOB('{Value.ToHexString()}')";

		public string ToXml() => OracleRawValue.ToXml(Value);

		public string ToJson() => OracleRawValue.ToJson(Value);

		public long Length => _blob.Length;

		public byte[] Value
		{
			get
			{
				Prefetch();

				return _value;
			}
		}

		public OracleBlobValue(OracleBlob blob)
		{
			_blob = blob;
			IsNull = blob.IsNull || blob.IsEmpty;
		}

		public byte[] GetChunk(int bytes)
		{
			var buffer = new byte[bytes];
			var bytesRead = _blob.Read(buffer, 0, bytes);
			var result = new byte[bytesRead];
			Array.Copy(buffer, 0, result, 0, bytesRead);
			return result;
		}

		public void Dispose() => _blob.Dispose();

		public override string ToString() => _blob.IsNull ? String.Empty : $"(BLOB[{Length:N0} B])";

		public void Prefetch()
		{
			if (_value == null)
			{
				_value =
					_blob.IsNull || _blob.IsEmpty
						? new byte[0]
						: _blob.Value;
			}
		}

		protected bool Equals(OracleBlobValue other) => Equals(_blob, other._blob);

		public override bool Equals(object obj)
		{
			if (obj is null) return false;
			if (ReferenceEquals(this, obj)) return true;
			return obj.GetType() == GetType() && Equals((OracleBlobValue)obj);
		}

		public override int GetHashCode() => _blob.GetHashCode();
	}

	public class OracleTimestamp : IValue
	{
		private readonly OracleDateTime _dateTime;
		private readonly OracleTimeStamp _value;

		private const BindingFlags BindingFlagsPrivateInstanceField = BindingFlags.Instance | BindingFlags.NonPublic;
		private static readonly Dictionary<Type, FieldInfo> FractionPrecisionFields =
			new Dictionary<Type, FieldInfo>
			{
				{ typeof(OracleTimeStamp), typeof(OracleTimeStamp).GetField("m_fSecondPrec", BindingFlagsPrivateInstanceField)},
				{ typeof(OracleTimeStampTZ), typeof(OracleTimeStampTZ).GetField("m_fSecondPrec", BindingFlagsPrivateInstanceField)},
				{ typeof(OracleTimeStampLTZ), typeof(OracleTimeStampLTZ).GetField("m_fSecondPrec", BindingFlagsPrivateInstanceField)}
			};

		public OracleTimestamp(OracleTimeStamp timeStamp)
		{
			_value = timeStamp;

			if (!timeStamp.IsNull)
			{
				_dateTime = new OracleDateTime(timeStamp.Year, timeStamp.Month, timeStamp.Day, timeStamp.Hour, timeStamp.Minute, timeStamp.Second);
			}
		}

		public object RawValue => _value;

		public bool IsNull => _value.IsNull;

		public string ToSqlLiteral() =>
			IsNull
				? TerminalValues.Null
				: ToLiteral(_dateTime, _value.Nanosecond, null);

		public string ToXml() => IsNull ? null : ToXml(_dateTime, _value.Nanosecond);

		public string ToJson() =>
			IsNull
				? "null"
				: ToJson(_dateTime, _value.Nanosecond, null);

		public override string ToString() =>
			_value.IsNull
				? String.Empty
				: FormatValue(_dateTime, _value.Nanosecond, GetFractionPrecision(_value));

		public override bool Equals(object obj)
		{
			if (obj is null) return false;
			if (ReferenceEquals(this, obj)) return true;
			return obj.GetType() == GetType() && Equals((OracleTimestamp)obj);
		}

		private bool Equals(OracleTimestamp other) => _value.Equals(other._value);

		public override int GetHashCode() => IsNull ? 0 : ((OracleTimeStamp)RawValue).BinData.GetContentHashCode();

		internal static string FormatValue(OracleDateTime dateTime, int nanoseconds, int fractionPrecision)
		{
			var fractionPart = nanoseconds.ToString(CultureInfo.InvariantCulture).PadLeft(9, '0').Substring(0, fractionPrecision);
			return $"{dateTime}{(String.IsNullOrEmpty(fractionPart) ? null : $".{fractionPart}")}";
		}

		internal static int GetFractionPrecision<T>(T value) => (int)FractionPrecisionFields[typeof(T)].GetValue(value);

		internal static string ToLiteral(OracleDateTime dateTime, int nanoseconds, string timeZone)
		{
			if (!String.IsNullOrEmpty(timeZone))
			{
				timeZone = $" {timeZone.Trim()}";
			}

			var nanoSecondsExtension = nanoseconds == 0 ? null : $".{nanoseconds:000000000}";

			return $"TIMESTAMP'{(dateTime.IsBeforeCrist ? "-" : null)}{dateTime.Value.Year}-{dateTime.Value.Month}-{dateTime.Value.Day} {dateTime.Value.Hour}:{dateTime.Value.Minute}:{dateTime.Value.Second}{nanoSecondsExtension}{timeZone}'";
		}

		internal static string ToXml(OracleDateTime dateTime, int nanoseconds)
		{
			var miliSecondsExtension = nanoseconds == 0 ? null : $".{Math.Round(nanoseconds / 1E+6m):000}";
			return $"{dateTime.Value:yyyy-MM-ddTHH:mm:ss}{miliSecondsExtension}";
		}

		internal static string ToJson(OracleDateTime dateTime, int nanoseconds, string timeZone)
		{
			var miliSecondsExtension = nanoseconds == 0 ? null : $".{Math.Round(nanoseconds / 1E+6m):000}";
			return $"\"{dateTime.Value:yyyy-MM-ddTHH:mm:ss}{miliSecondsExtension}{(String.IsNullOrEmpty(timeZone) ? null : timeZone.Trim())}\"";
		}
	}

	public class OracleRowId : IValue
	{
		public const int InternalCode = 104;
		public const string TypeName = TerminalValues.RowIdDataType;

		public bool IsNull { get; }

		public OracleRowId(OracleString oracleString)
		{
			RawValue = oracleString;
			IsNull = oracleString.IsNull;
		}

		public string ToSqlLiteral() => IsNull ? TerminalValues.Null : $"'{RawValue}'";

		public string ToXml() => RawValue.ToString();

		public string ToJson() => IsNull ? "null" : $"\"{RawValue}\"";

		public object RawValue { get; }

		public override string ToString()
		{
			var rowIdString = RawValue.ToString();
			var objectId = DecodeBase64Hex(rowIdString.Substring(0, 6).PadLeft(8, 'A'));
			var fileNumber = DecodeBase64Hex(rowIdString.Substring(6, 3).PadLeft(4, 'A'));

			string filePart = null;
			string blockSource;
			if (fileNumber > 0)
			{
				filePart = $"fil={fileNumber}; ";
				blockSource = rowIdString.Substring(9, 6).PadLeft(8, 'A');
			}
			else
			{
				blockSource = rowIdString.Substring(6, 9).PadLeft(12, 'A');
			}

			var blockNumber = DecodeBase64Hex(blockSource);
			var offset = DecodeBase64Hex(rowIdString.Substring(15, 3).PadLeft(4, 'A'));
			return $"{rowIdString} (obj={objectId}; {filePart}blk={blockNumber}; off={offset})";
		}

		private static int DecodeBase64Hex(string base64Bytes)
		{
			var binaryData = Convert.FromBase64String(base64Bytes);
			var result = 0;

			foreach (var b in binaryData)
			{
				result = result * 256 + b;
			}

			return result;
		}

		protected bool Equals(OracleRowId other) => Equals(RawValue, other.RawValue);

		public override bool Equals(object obj)
		{
			if (obj is null) return false;
			if (ReferenceEquals(this, obj)) return true;
			return obj.GetType() == GetType() && Equals((OracleRowId)obj);
		}

		public override int GetHashCode() => RawValue.GetHashCode();
	}

	public class OracleIntervalDayToSecond : IValue
	{
		private static readonly FieldInfo FieldDayPrecision = typeof(OracleIntervalDS).GetField("m_dayPrec", BindingFlags.Instance | BindingFlags.NonPublic);
		private static readonly FieldInfo FieldSecondPrecision = typeof(OracleIntervalDS).GetField("m_fSecondPrec", BindingFlags.Instance | BindingFlags.NonPublic);

		private readonly OracleIntervalDS _value;

		public bool IsNull { get; }

		public OracleIntervalDayToSecond(OracleIntervalDS intervalDayToSecond)
		{
			_value = intervalDayToSecond;
			IsNull = intervalDayToSecond.IsNull;
		}

		public string ToSqlLiteral() =>
			IsNull
				? TerminalValues.Null
				: $"INTERVAL '{_value}' DAY({FieldDayPrecision.GetValue(_value)}) TO SECOND({FieldSecondPrecision.GetValue(_value)})";

		public string ToXml() => _value.ToString();

		public string ToJson() => IsNull ? "null" : $"\"{_value}\"";

		public object RawValue => _value;

		public override string ToString() => IsNull ? String.Empty : _value.ToString();

		protected bool Equals(OracleIntervalDayToSecond other) => _value.Equals(other._value);

		public override bool Equals(object obj)
		{
			if (obj is null) return false;
			if (ReferenceEquals(this, obj)) return true;
			return obj.GetType() == GetType() && Equals((OracleIntervalDayToSecond)obj);
		}

		public override int GetHashCode() => _value.GetHashCode();
	}

	public class OracleIntervalYearToMonth : IValue
	{
		private static readonly FieldInfo FieldYearPrecision = typeof(OracleIntervalYM).GetField("m_yearPrec", BindingFlags.Instance | BindingFlags.NonPublic);

		private readonly OracleIntervalYM _value;

		public bool IsNull { get; }

		public OracleIntervalYearToMonth(OracleIntervalYM intervalYearToMonth)
		{
			_value = intervalYearToMonth;
			IsNull = intervalYearToMonth.IsNull;
		}

		public string ToSqlLiteral() =>
			IsNull
				? TerminalValues.Null
				: $"INTERVAL '{_value}' YEAR({FieldYearPrecision.GetValue(_value)}) TO MONTH";

		public string ToXml() => _value.ToString();

		public string ToJson() =>
			IsNull
				? "null"
				: $"\"{_value}\"";

		public object RawValue => _value;

		public override string ToString() => IsNull ? String.Empty : _value.ToString();

		protected bool Equals(OracleIntervalYearToMonth other) => _value.Equals(other._value);

		public override bool Equals(object obj)
		{
			if (obj is null) return false;
			if (ReferenceEquals(this, obj)) return true;
			return obj.GetType() == GetType() && Equals((OracleIntervalYearToMonth)obj);
		}

		public override int GetHashCode() => _value.GetHashCode();
	}

	public class OracleDateTime : IValue
	{
		public const string IsoDateDotNetFormatMask = "yyyy-MM-dd";
		public const string IsoDateTimeDotNetFormatMask = "yyyy-MM-dd HH:mm:ss";

		public DateTime Value { get; }

		public bool IsNull { get; }

		public string ToSqlLiteral()
		{
			if (IsNull)
			{
				return TerminalValues.Null;
			}

			var beforeChristPrefix = IsBeforeCrist ? "-" : null;
			return Value.Hour == 0 && Value.Minute == 0 && Value.Second == 0
				? $"DATE'{beforeChristPrefix}{Value.ToString(IsoDateDotNetFormatMask)}'"
				: $"TO_DATE('{beforeChristPrefix}{Value.ToString(IsoDateTimeDotNetFormatMask)}', 'SYYYY-MM-DD HH24:MI:SS')";
		}

		public string ToXml() =>
			IsNull
				? null
				: OracleTimestamp.ToXml(this, 0);

		public string ToJson() =>
			IsNull
				? "null"
				: OracleTimestamp.ToJson(this, 0, null);

		public bool IsBeforeCrist { get; }

		public object RawValue { get; }

		public OracleDateTime() => IsNull = true;

		public OracleDateTime(OracleDate oracleDate) : this(oracleDate.Year, oracleDate.Month, oracleDate.Day, oracleDate.Hour, oracleDate.Minute, oracleDate.Second)
		{
		}

		public OracleDateTime(int year, int month, int day, int hour, int minute, int second)
		{
			Value = new DateTime(Math.Abs(year), month, day, hour, minute, second);
			IsBeforeCrist = year < 0;

			RawValue = new OracleDate(year, month, day, hour, minute, second);
		}

		public override string ToString() =>
			IsNull
				? String.Empty
				: $"{(IsBeforeCrist ? "BC " : null)}{CellValueConverter.FormatDateTime(Value)}";

		protected bool Equals(OracleDateTime other) => Equals(RawValue, other.RawValue);

		public override bool Equals(object obj)
		{
			if (obj is null) return false;
			if (ReferenceEquals(this, obj)) return true;
			return obj.GetType() == GetType() && Equals((OracleDateTime)obj);
		}

		public override int GetHashCode() => IsNull ? 0 : ((OracleDate)RawValue).BinData.GetContentHashCode();
	}

	public class OracleTimestampWithTimeZone : IValue
	{
		private readonly OracleDateTime _dateTime;
		private readonly OracleTimeStampTZ _value;

		public OracleTimestampWithTimeZone(OracleTimeStampTZ timeStamp)
		{
			_value = timeStamp;

			if (!timeStamp.IsNull)
			{
				_dateTime = new OracleDateTime(timeStamp.Year, timeStamp.Month, timeStamp.Day, timeStamp.Hour, timeStamp.Minute, timeStamp.Second);
			}
		}

		public object RawValue => _value;

		public bool IsNull => _value.IsNull;

		public string ToSqlLiteral() =>
			IsNull
				? TerminalValues.Null
				: OracleTimestamp.ToLiteral(_dateTime, _value.Nanosecond, _value.TimeZone);

		public string ToXml() =>
			IsNull
				? null
				: OracleTimestamp.ToXml(_dateTime, _value.Nanosecond);

		public string ToJson() =>
			IsNull
				? "null"
				: OracleTimestamp.ToJson(_dateTime, _value.Nanosecond, _value.TimeZone);

		public override string ToString() =>
			_value.IsNull
				? String.Empty
				: $"{OracleTimestamp.FormatValue(_dateTime, _value.Nanosecond, OracleTimestamp.GetFractionPrecision(_value))} {_value.TimeZone}";

		protected bool Equals(OracleTimestampWithTimeZone other) => _value.Equals(other._value);

		public override bool Equals(object obj)
		{
			if (obj is null) return false;
			if (ReferenceEquals(this, obj)) return true;
			return obj.GetType() == GetType() && Equals((OracleTimestampWithTimeZone)obj);
		}

		public override int GetHashCode() => IsNull ? 0 : ((OracleTimeStampTZ)RawValue).BinData.GetContentHashCode();
	}

	public class OracleTimestampWithLocalTimeZone : IValue
	{
		private readonly OracleDateTime _dateTime;
		private readonly OracleTimeStampLTZ _value;

		public OracleTimestampWithLocalTimeZone(OracleTimeStampLTZ timeStamp)
		{
			_value = timeStamp;

			if (!timeStamp.IsNull)
			{
				_dateTime = new OracleDateTime(timeStamp.Year, timeStamp.Month, timeStamp.Day, timeStamp.Hour, timeStamp.Minute, timeStamp.Second);
			}
		}

		public object RawValue => _value;

		public bool IsNull => _value.IsNull;

		public string ToSqlLiteral() =>
			IsNull
				? TerminalValues.Null
				: OracleTimestamp.ToLiteral(_dateTime, _value.Nanosecond, null);

		public string ToXml() =>
			IsNull
				? null
				: OracleTimestamp.ToXml(_dateTime, _value.Nanosecond);

		public string ToJson() =>
			IsNull
				? "null"
				: OracleTimestamp.ToJson(_dateTime, _value.Nanosecond, null);

		public override string ToString()
		{
			return _value.IsNull
				? String.Empty
				: OracleTimestamp.FormatValue(_dateTime, _value.Nanosecond, OracleTimestamp.GetFractionPrecision(_value));
		}

		protected bool Equals(OracleTimestampWithLocalTimeZone other) => _value.Equals(other._value);

		public override bool Equals(object obj)
		{
			if (obj is null) return false;
			if (ReferenceEquals(this, obj)) return true;
			return obj.GetType() == GetType() && Equals((OracleTimestampWithLocalTimeZone)obj);
		}

		public override int GetHashCode() => IsNull ? 0 : ((OracleTimeStampLTZ)RawValue).BinData.GetContentHashCode();
	}

	public class OracleRawValue : ILargeBinaryValue
	{
		private readonly OracleBinary _oracleBinary;
		private readonly string _preview;

		public OracleRawValue(OracleBinary value)
		{
			_oracleBinary = value;
			
			if (value.IsNull)
			{
				_preview = String.Empty;
				Value = new byte[0];
			}
			else
			{
				_preview = _oracleBinary.Value.ToHexString();
				Value = _oracleBinary.Value;
			}
		}

		public object RawValue => _oracleBinary;

		public string DataTypeName => TerminalValues.Raw;

		public bool IsEditable => false;

		public bool IsNull => _oracleBinary.IsNull;

		public string ToSqlLiteral() =>
			IsNull
				? TerminalValues.Null
				: ToSqlLiteral(Value);

		public string ToXml() => ToXml(Value);

		public string ToJson() => ToJson(Value);

		public long Length => _oracleBinary.Length;

	    public void Prefetch()
		{
		}

		public byte[] Value { get; }
		
		public byte[] GetChunk(int bytes) => throw new NotSupportedException();

		public override string ToString() => _preview;

		internal static string ToSqlLiteral(byte[] value) => $"'{value.ToHexString()}'";

		internal static string ToXml(byte[] value) =>
			value.Length == 0
				? null
				: $"<![CDATA[{Convert.ToBase64String(value)}]]>";

		internal static string ToJson(byte[] value) =>
			value.Length == 0
				? "null"
				: $"\"{Convert.ToBase64String(value)}\"";

		protected bool Equals(OracleRawValue other) => _oracleBinary.Equals(other._oracleBinary);

		public override bool Equals(object obj)
		{
			if (obj is null) return false;
			if (ReferenceEquals(this, obj)) return true;
			return obj.GetType() == GetType() && Equals((OracleRawValue)obj);
		}

		public override int GetHashCode() => _oracleBinary.GetHashCode();
	}

	public class OracleNumber : IValue
	{
		private readonly OracleDecimal _oracleDecimal;

		public OracleNumber(OracleDecimal value) => _oracleDecimal = SetOutputFormat(value);

		internal static OracleDecimal SetOutputFormat(OracleDecimal value)
		{
			if (!value.IsNull)
			{
				var absValue = OracleDecimal.Abs(value);
				if (absValue < 1e-28m && absValue != 0)
				{
					value.Format = "FM0D0999999999999999999999999999999999999999EEEE";
				}
				else if (absValue > 0 && absValue < 1)
				{
					value.Format = "FM0D9999999999999999999999999999999999999999";
				}
			}

			return value;
		}

		public object RawValue => _oracleDecimal;

		public bool IsNull => _oracleDecimal.IsNull;

		public string ToSqlLiteral()
		{
			if (IsNull)
			{
				return TerminalValues.Null;
			}

			var literalValue = ToString();

#if !ORACLE_MANAGED_DATA_ACCESS_CLIENT
			if (!_oracleDecimal.IsInt)
			{
				var decimalSeparator = OracleGlobalization.GetThreadInfo().NumericCharacters[0];
				if (decimalSeparator != '.')
				{
					var index = literalValue.LastIndexOf(decimalSeparator);
					if (index != -1)
					{
						return literalValue.Remove(index, 1).Insert(index, ".");
					}
				}
			}
#endif

			return literalValue;
		}

		public string ToXml() =>
			IsNull
				? null
				: ToSqlLiteral();

		public string ToJson() =>
			IsNull
				? "null"
				: ToSqlLiteral();

		public override string ToString() =>
			_oracleDecimal.IsNull
				? String.Empty
				: _oracleDecimal.ToString();

		protected bool Equals(OracleNumber other) => _oracleDecimal.Equals(other._oracleDecimal);

		public override bool Equals(object obj)
		{
			if (obj is null) return false;
			if (ReferenceEquals(this, obj)) return true;
			return obj.GetType() == GetType() && Equals((OracleNumber)obj);
		}

		public override int GetHashCode() => ToString().GetHashCode();
	}

	public class OracleLongRawValue : ILargeBinaryValue
	{
		private const int BufferSize = 8192;
		private readonly OracleDataReader _reader;
		private readonly int _columnIndex;

		public string DataTypeName => "LONG RAW";

		public bool IsEditable => false;

		public bool IsNull { get; }

		public string ToSqlLiteral() =>
			IsNull
				? TerminalValues.Null
				: OracleRawValue.ToSqlLiteral(Value);

		public string ToXml() => OracleRawValue.ToXml(Value);

		public string ToJson() => OracleRawValue.ToJson(Value);

		public long Length => Value.Length;

		public byte[] Value { get; }

		public OracleLongRawValue(OracleBinary binary)
		{
			RawValue = binary;
			Value = binary.IsNull ? new byte[0] : binary.Value;
			IsNull = binary.IsNull;
		}

		public object RawValue { get; }

		public byte[] GetChunk(int bytes) => throw new NotImplementedException();

		public override string ToString() => Value.Length == 0 ? String.Empty : $"(LONG RAW[{Length:N0} B])";

		private byte[] FetchValue()
		{
			var buffers = new List<byte[]>();
			var offset = 0;
			var totalSize = 0L;
			while (true)
			{
				var buffer = new byte[BufferSize];
				var bytesLength = _reader.GetBytes(_columnIndex, offset, buffer, 0, BufferSize);
				totalSize += bytesLength;
				buffers.Add(buffer);

				if (bytesLength < BufferSize)
					break;

				offset += BufferSize;
			}

			var result = new byte[totalSize];

			for (var i = 0; i < buffers.Count; i++)
			{
				Array.Copy(buffers[i], 0L, result, i*BufferSize, BufferSize);
				buffers[i] = null;
			}

			return result;
		}

		public void Prefetch()
		{
		}

		protected bool Equals(OracleLongRawValue other) => Equals(RawValue, other.RawValue);

		public override bool Equals(object obj)
		{
			if (obj is null) return false;
			if (ReferenceEquals(this, obj)) return true;
			return obj.GetType() == GetType() && Equals((OracleLongRawValue)obj);
		}

		public override int GetHashCode() => RawValue.GetHashCode();
	}
}
