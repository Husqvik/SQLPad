using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using Oracle.DataAccess.Client;
using Oracle.DataAccess.Types;

namespace SqlPad.Oracle
{
	public abstract class OracleLargeTextValue : ILargeTextValue
	{
		protected const int PreviewLength = 1023;
		public const string Ellipsis = "\u2026";

		private string _preview;
		private string _value;

		public bool IsEditable { get { return false; } }

		public abstract string DataTypeName { get; }
		
		public abstract bool IsNull { get; }

		public abstract long Length { get; }

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

			var preview = GetChunk(0, PreviewLength + 1);
			if (preview.Length > PreviewLength)
			{
				preview = String.Format("{0}{1}", preview.Substring(0, PreviewLength), Ellipsis);
			}

			return _preview = preview;
		}

		public string Value
		{
			get { return _value ?? (_value = IsNull ? String.Empty : GetValue()); }
		}

		protected abstract string GetValue();

		public abstract string GetChunk(int offset, int length);

		public override string ToString()
		{
			return Preview;
		}
	}

	public class OracleXmlValue : OracleLargeTextValue, IDisposable
	{
		private readonly OracleXmlType _xmlType;

		public override string DataTypeName { get { return "XMLTYPE"; } }

		public override long Length { get { return Value.Length; } }

		public override bool IsNull { get { return _xmlType.IsNull; } }

		protected override string GetValue()
		{
			return _xmlType.Value;
		}

		public OracleXmlValue(OracleXmlType xmlType)
		{
			_xmlType = xmlType;
		}

		public override string GetChunk(int offset, int length)
		{
			var characters = new char[length];
			var characterCount = _xmlType.GetStream().Read(characters, 0, length);
			return new string(characters, 0, characterCount);
		}

		public void Dispose()
		{
			_xmlType.Dispose();
		}
	}

	public class OracleClobValue : OracleLargeTextValue, IDisposable
	{
		private readonly OracleClob _clob;
		private readonly string _dataTypeName;

		public override string DataTypeName { get { return _dataTypeName; } }

		public override long Length { get { return _clob.Length; } }

		public override bool IsNull { get { return _clob.IsNull; } }

		protected override string GetValue()
		{
			return _clob.Value;
		}

		public OracleClobValue(string dataTypeName, OracleClob clob)
		{
			_clob = clob;
			_dataTypeName = dataTypeName;
		}

		public override string GetChunk(int offset, int length)
		{
			var characters = new char[length];
			var characterCount = _clob.Read(characters, offset, length);
			return new string(characters, 0, characterCount);
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

		public string DataTypeName { get { return "BLOB"; } }
		
		public bool IsEditable { get { return false; } }

		public long Length { get; private set; }

		public byte[] Value
		{
			get { return _value ?? (_value = GetValue()); }
		}

		private byte[] GetValue()
		{
			return _blob.IsNull
					? new byte[0]
					: _blob.Value;
		}

		public OracleBlobValue(OracleBlob blob)
		{
			_blob = blob;
			Length = _blob.Length;
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
	}

	public class OracleTimestamp
	{
		private readonly OracleTimeStamp _oracleTimeStamp;

		public OracleTimestamp(OracleDataReader reader, int columnIndex)
		{
			_oracleTimeStamp = reader.GetOracleTimeStamp(columnIndex);
		}

		public bool IsNull { get { return _oracleTimeStamp.IsNull; } }

		public override string ToString()
		{
			return String.Format("{0}.{1}", CellValueConverter.FormatDateTime(_oracleTimeStamp.Value), _oracleTimeStamp.Nanosecond.ToString(CultureInfo.InvariantCulture));
		}
	}

	public class OracleTimestampWithTimeZone
	{
		private readonly OracleTimeStampTZ _oracleTimeStamp;

		public OracleTimestampWithTimeZone(OracleDataReader reader, int columnIndex)
		{
			_oracleTimeStamp = reader.GetOracleTimeStampTZ(columnIndex);
		}

		public bool IsNull { get { return _oracleTimeStamp.IsNull; } }

		public override string ToString()
		{
			return String.Format("{0}.{1} {2}", CellValueConverter.FormatDateTime(_oracleTimeStamp.Value), _oracleTimeStamp.Nanosecond.ToString(CultureInfo.InvariantCulture), _oracleTimeStamp.TimeZone);
		}
	}

	public class OracleNumber
	{
		private readonly OracleDecimal _oracleDecimal;

		public OracleNumber(OracleDataReader reader, int columnIndex)
		{
			_oracleDecimal = SetOutputFormat(reader.GetOracleDecimal(columnIndex));
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

		public override string ToString()
		{
			if (IsNull)
			{
				throw new InvalidOperationException();
			}

			return _oracleDecimal.ToString();
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

		public long Length { get; private set; }

		public byte[] Value { get { return _value; } }

		public OracleLongRawValue(OracleDataReader reader, int columnIndex)
		{
			_columnIndex = columnIndex;
			_reader = reader;
			var oracleBinary = reader.GetOracleBinary(columnIndex);
			_value = oracleBinary.IsNull ? new byte[0] : oracleBinary.Value;
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
	}

	internal static class OracleLargeObjectHelper
	{
		public static T ExecuteFunction<T>(OracleConnection connection, Func<T> function)
		{
			var closeConnection = false;

			try
			{
				if (connection.State != ConnectionState.Open)
				{
					connection.Open();
					closeConnection = true;
				}

				return function();
			}
			finally
			{
				if (closeConnection && connection.State != ConnectionState.Closed)
				{
					connection.Close();
				}
			}
		}
	}
}
