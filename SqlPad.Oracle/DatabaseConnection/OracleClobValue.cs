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
		public const string Ellipsis = "\u2026";

		private string _preview;
		private string _value;

		public bool IsEditable { get { return false; } }

		public abstract string DataTypeName { get; }
		
		public abstract bool IsNull { get; }

		public abstract string ToSqlLiteral();

		public string ToXml()
		{
			return String.Format("<![CDATA[{0}]]>", Value);
		}

		public string ToJson()
		{
			return IsNull
				? "null"
				: String.Format("\"{0}\"", Value.Replace("\"", "\\\""));
		}

		public abstract long Length { get; }

		public virtual int PreviewLength
		{
			get { return DefaultPreviewLength; }
		}

		public string Preview
		{
			get { return _preview ?? BuildPreview(); }
		}

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
				preview = String.Format("{0}{1}", preview.Substring(0, indexFirstLineBreak != -1 ? indexFirstLineBreak : PreviewLength), Ellipsis);
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

		public override string ToString()
		{
			return Preview;
		}

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
	}

	#if !ORACLE_MANAGED_DATA_ACCESS_CLIENT
	public class OracleXmlValue : OracleLargeTextValue, IDisposable
	{
		private readonly OracleXmlType _xmlType;
		private readonly int _previewLength;

		public override string DataTypeName { get { return TerminalValues.XmlType; } }

		public override long Length { get { return Value.Length; } }

		public override bool IsNull { get { return _xmlType.IsNull; } }

		public override string ToSqlLiteral()
		{
			return IsNull
				? TerminalValues.Null
				: String.Format("{0}('{1}')", TerminalValues.XmlType, Value.Replace("'", "''"));
		}

		public override int PreviewLength
		{
			get { return _previewLength; }
		}

		protected override string GetValue()
		{
			return _xmlType.Value;
		}

		public OracleXmlValue(OracleXmlType xmlType, int previewLength = DefaultPreviewLength)
		{
			_xmlType = xmlType;
			_previewLength = previewLength;
		}

		public override void GetChunk(StringBuilder stringBuilder, int offset, int length)
		{
			var characters = new char[length];
			var characterCount = _xmlType.GetStream().Read(characters, 0, length);
			stringBuilder.Append(characters, 0, characterCount);
		}

		public void Dispose()
		{
			_xmlType.Dispose();
		}
	}
	#endif

	public class OracleSimpleValue : ILargeTextValue
	{
		private readonly string _value;

		public OracleSimpleValue(object value)
		{
			_value = Convert.ToString(value);
			IsNull = value == DBNull.Value;
		}

		public bool IsNull { get; private set; }

		public string ToSqlLiteral()
		{
			return IsNull
				? TerminalValues.Null
				: String.Format("'{0}'", _value.Replace("'", "''"));
		}

		public string ToXml()
		{
			return _value;
		}

		public string ToJson()
		{
			return IsNull
				? "null"
				: String.Format("\"{0}\"", Value.Replace("\"", "\\\""));
		}

		public string DataTypeName
		{
			get { throw new NotImplementedException(); }
		}

		public long Length
		{
			get { return _value.Length; }
		}

		public void GetChunk(StringBuilder stringBuilder, int offset, int length)
		{
			stringBuilder.Append(_value.Substring(offset, length));
		}

		public bool IsEditable { get { return false; } }
		
		public void Prefetch() { }

		public string Preview { get { return _value; } }
		
		public string Value { get { return _value; } }

		public override string ToString()
		{
			return _value;
		}
	}

	public class OracleClobValue : OracleLargeTextValue, IDisposable
	{
		private const int MaximumChunkSize = 16777216;

		private readonly OracleClob _clob;
		private readonly int _previewLength;
		private readonly string _dataTypeName;

		public override string DataTypeName { get { return _dataTypeName; } }

		public override long Length { get { return _clob.Length; } }

		public override bool IsNull { get { return _clob.IsNull || _clob.IsEmpty; } }

		public override string ToSqlLiteral()
		{
			return IsNull
				? TerminalValues.Null
				: String.Format("TO_{0}CLOB('{1}')", _clob.IsNClob ? "N" : null, Value.Replace("'", "''"));
		}

		public override int PreviewLength
		{
			get { return _previewLength; }
		}

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
			_previewLength = previewLength;
			_dataTypeName = dataTypeName;
		}

		public override void GetChunk(StringBuilder stringBuilder, int offset, int length)
		{
			var characters = new char[length];
			var characterCount = _clob.Read(characters, offset, length);
			stringBuilder.Append(characters, 0, characterCount);
		}

		public void Dispose()
		{
			_clob.Dispose();
		}
	}

	public class OracleBlobValue : ILargeBinaryValue, IDisposable
	{
		private readonly OracleBlob _blob;
		private byte[] _value;

		public string DataTypeName { get { return TerminalValues.Blob; } }
		
		public bool IsEditable { get { return false; } }
		
		public bool IsNull { get { return _blob.IsNull || _blob.IsEmpty; } }

		public string ToSqlLiteral()
		{
			return IsNull
				? TerminalValues.Null
				: String.Format("TO_BLOB('{0}')", Value.ToHexString());
		}

		public string ToXml()
		{
			return OracleRawValue.ToXml(Value);
		}

		public string ToJson()
		{
			return OracleRawValue.ToJson(Value);
		}

		public long Length { get { return _blob.Length; } }

		public byte[] Value
		{
			get
			{
				Prefetch();

				return _value;
			}
		}

		private byte[] GetValue()
		{
			return _blob.IsNull || _blob.IsEmpty
					? new byte[0]
					: _blob.Value;
		}

		public OracleBlobValue(OracleBlob blob)
		{
			_blob = blob;
		}

		public byte[] GetChunk(int bytes)
		{
			var buffer = new byte[bytes];
			var bytesRead = _blob.Read(buffer, 0, bytes);
			var result = new byte[bytesRead];
			Array.Copy(buffer, 0, result, 0, bytesRead);
			return result;
		}

		public void Dispose()
		{
			_blob.Dispose();
		}

		public override string ToString()
		{
			return Length == 0 ? String.Empty : String.Format("(BLOB[{0} B])", Length);
		}

		public void Prefetch()
		{
			if (_value == null)
			{
				_value = GetValue();
			}
		}
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

		public bool IsNull { get { return _value.IsNull; } }

		public string ToSqlLiteral()
		{
			return IsNull
				? TerminalValues.Null
				: ToLiteral(_dateTime, _value.Nanosecond, null);
		}

		public string ToXml()
		{
			return IsNull
				? null
				: ToXml(_dateTime, _value.Nanosecond);
		}

		public string ToJson()
		{
			return IsNull
				? "null"
				: ToJson(_dateTime, _value.Nanosecond, null);
		}

		public override string ToString()
		{
			return _value.IsNull
				? String.Empty
				: FormatValue(_dateTime, _value.Nanosecond, GetFractionPrecision(_value));
		}

		internal static string FormatValue(OracleDateTime dateTime, int nanoseconds, int fractionPrecision)
		{
			var fractionPart = nanoseconds.ToString(CultureInfo.InvariantCulture).PadRight(9, '0').Substring(0, fractionPrecision);
			return String.Format("{0}{1}", dateTime, String.IsNullOrEmpty(fractionPart) ? null : String.Format(".{0}", fractionPart));
		}

		internal static int GetFractionPrecision<T>(T value)
		{
			return (int)FractionPrecisionFields[typeof(T)].GetValue(value);
		}

		internal static string ToLiteral(OracleDateTime dateTime, int nanoseconds, string timeZone)
		{
			if (!String.IsNullOrEmpty(timeZone))
			{
				timeZone = String.Format(" {0}", timeZone.Trim());
			}

			var nanoSecondsExtension = nanoseconds == 0 ? null : String.Format(".{0}", nanoseconds.ToString("000000000"));

			return String.Format("TIMESTAMP'{0}{1}-{2}-{3} {4}:{5}:{6}{7}{8}'", dateTime.IsBeforeCrist ? "-" : null, dateTime.Value.Year, dateTime.Value.Month, dateTime.Value.Day, dateTime.Value.Hour, dateTime.Value.Minute, dateTime.Value.Second, nanoSecondsExtension, timeZone);
		}

		internal static string ToXml(OracleDateTime dateTime, int nanoseconds)
		{
			var miliSecondsExtension = nanoseconds == 0 ? null : String.Format(".{0}", Math.Round(nanoseconds / 1E+6m).ToString("000"));
			return String.Format("{0}{1}", dateTime.Value.ToString("yyyy-MM-ddTHH:mm:ss"), miliSecondsExtension);
		}

		internal static string ToJson(OracleDateTime dateTime, int nanoseconds, string timeZone)
		{
			var miliSecondsExtension = nanoseconds == 0 ? null : String.Format(".{0}", Math.Round(nanoseconds / 1E+6m).ToString("000"));
			return String.Format("\"{0}{1}{2}\"", dateTime.Value.ToString("yyyy-MM-ddTHH:mm:ss"), miliSecondsExtension, String.IsNullOrEmpty(timeZone) ? null : timeZone.Trim());
		}
	}

	public class OracleDateTime : IValue
	{
		public const string IsoDateDotNetFormatMask = "yyyy-MM-dd HH:mm:ss";
		
		private readonly DateTime _value;

		public DateTime Value { get { return _value; } }

		public bool IsNull { get; private set; }

		public string ToSqlLiteral()
		{
			return IsNull
				? TerminalValues.Null
				: String.Format("TO_DATE('{0}{1}', 'SYYYY-MM-DD HH24:MI:SS')", IsBeforeCrist ? "-" : null, _value.ToString(IsoDateDotNetFormatMask));
		}

		public string ToXml()
		{
			return IsNull
				? null
				: OracleTimestamp.ToXml(this, 0);
		}

		public string ToJson()
		{
			return IsNull
				? "null"
				: OracleTimestamp.ToJson(this, 0, null);
		}

		public bool IsBeforeCrist { get; private set; }

		public OracleDateTime()
		{
			IsNull = true;
		}

		public OracleDateTime(int year, int month, int day, int hour, int minute, int second)
		{
			_value = new DateTime(Math.Abs(year), month, day, hour, minute, second);
			IsBeforeCrist = year < 0;
		}

		public override string ToString()
		{
			return IsNull
				? String.Empty
				: String.Format("{0}{1}", IsBeforeCrist ? "BC " : null, CellValueConverter.FormatDateTime(_value));
		}
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

		public bool IsNull { get { return _value.IsNull; } }

		public string ToSqlLiteral()
		{
			return IsNull
				? TerminalValues.Null
				: OracleTimestamp.ToLiteral(_dateTime, _value.Nanosecond, _value.TimeZone);
		}

		public string ToXml()
		{
			return IsNull
				? null
				: OracleTimestamp.ToXml(_dateTime, _value.Nanosecond);
		}

		public string ToJson()
		{
			return IsNull
				? "null"
				: OracleTimestamp.ToJson(_dateTime, _value.Nanosecond, _value.TimeZone);
		}

		public override string ToString()
		{
			return _value.IsNull
				? String.Empty
				: String.Format("{0} {1}", OracleTimestamp.FormatValue(_dateTime, _value.Nanosecond, OracleTimestamp.GetFractionPrecision(_value)), _value.TimeZone);
		}
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

		public bool IsNull { get { return _value.IsNull; } }

		public string ToSqlLiteral()
		{
			return IsNull
				? TerminalValues.Null
				: OracleTimestamp.ToLiteral(_dateTime, _value.Nanosecond, null);
		}

		public string ToXml()
		{
			return IsNull
				? null
				: OracleTimestamp.ToXml(_dateTime, _value.Nanosecond);
		}

		public string ToJson()
		{
			return IsNull
				? "null"
				: OracleTimestamp.ToJson(_dateTime, _value.Nanosecond, null);
		}

		public override string ToString()
		{
			return _value.IsNull
				? String.Empty
				: OracleTimestamp.FormatValue(_dateTime, _value.Nanosecond, OracleTimestamp.GetFractionPrecision(_value));
		}
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

		public string DataTypeName { get { return TerminalValues.Raw; } }
		
		public bool IsEditable { get { return false; } }
		
		public bool IsNull { get { return _oracleBinary.IsNull; } }

		public string ToSqlLiteral()
		{
			return IsNull
				? TerminalValues.Null
				: ToSqlLiteral(Value);
		}

		public string ToXml()
		{
			return ToXml(Value);
		}

		public string ToJson()
		{
			return ToJson(Value);
		}

		public long Length { get { return _oracleBinary.Length; } }
		
		public void Prefetch()
		{
		}

		public byte[] Value { get; private set; }
		
		public byte[] GetChunk(int bytes)
		{
			throw new NotSupportedException();
		}

		public override string ToString()
		{
			return _preview;
		}

		internal static string ToSqlLiteral(byte[] value)
		{
			return String.Format("'{0}'", value.ToHexString());
		}

		internal static string ToXml(byte[] value)
		{
			return value.Length == 0
				? null
				: String.Format("<![CDATA[{0}]]>", Convert.ToBase64String(value));
		}

		internal static string ToJson(byte[] value)
		{
			return value.Length == 0
					? "null"
					: String.Format("\"{0}\"", Convert.ToBase64String(value));
		}
	}

	public class OracleNumber : IValue
	{
		private readonly OracleDecimal _oracleDecimal;

		public OracleNumber(OracleDecimal value)
		{
			_oracleDecimal = SetOutputFormat(value);
		}

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

		public bool IsNull { get { return _oracleDecimal.IsNull; } }

		public string ToSqlLiteral()
		{
			if (IsNull)
			{
				return TerminalValues.Null;
			}

			var literalValue = ToString();
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

			return literalValue;
		}

		public string ToXml()
		{
			return IsNull
				? null
				: ToSqlLiteral();
		}

		public string ToJson()
		{
			return IsNull
				? "null"
				: ToSqlLiteral();
		}

		public override string ToString()
		{
			return _oracleDecimal.IsNull
				? String.Empty
				: _oracleDecimal.ToString();
		}
	}

	public class OracleLongRawValue : ILargeBinaryValue
	{
		private const int BufferSize = 8192;
		private readonly OracleDataReader _reader;
		private byte[] _value;
		private readonly int _columnIndex;

		public string DataTypeName { get { return "LONG RAW"; } }

		public bool IsEditable { get { return false; } }

		public bool IsNull { get; private set; }

		public string ToSqlLiteral()
		{
			return IsNull
				? TerminalValues.Null
				: OracleRawValue.ToSqlLiteral(_value);
		}

		public string ToXml()
		{
			return OracleRawValue.ToXml(Value);
		}

		public string ToJson()
		{
			return OracleRawValue.ToJson(Value);
		}

		public long Length { get { return _value.Length; } }

		public byte[] Value { get { return _value; } }

		public OracleLongRawValue(OracleBinary binary)
		{
			_value = binary.IsNull ? new byte[0] : binary.Value;
			IsNull = binary.IsNull;
		}

		public byte[] GetChunk(int bytes)
		{
			throw new NotImplementedException();
		}

		public override string ToString()
		{
			return _value.Length == 0 ? String.Empty : "(LONG RAW)";
		}

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
				Array.Copy(buffers[i], 0L, result, i * BufferSize, BufferSize);
				buffers[i] = null;
			}

			return result;
		}

		public void Prefetch() { }
	}
}
