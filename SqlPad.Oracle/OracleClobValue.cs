using System;
using System.Collections.Generic;
using System.Data;
using Oracle.DataAccess.Client;
using Oracle.DataAccess.Types;

namespace SqlPad.Oracle
{
	public class OracleClobValue : ILargeTextValue, IDisposable
	{
		private const int PreviewLength = 1023;
		public const string Ellipsis = "\u2026";

		private readonly OracleClob _clob;
		private string _value;

		public string DataTypeName { get; private set; }

		public bool IsEditable { get { return false; } }

		public string Preview { get; private set; }

		public long Length { get; private set; }

		public string Value
		{
			get { return _value ?? (_value = GetValue()); }
		}

		private string GetValue()
		{
			return _clob.IsNull
					? String.Empty
					: OracleLargeObjectHelper.ExecuteFunction(_clob.Connection, () => _clob.Value);
		}

		public OracleClobValue(string dataTypeName, OracleClob clob)
		{
			_clob = clob;
			DataTypeName = dataTypeName;
			Length = _clob.Length;
			Preview = Length > PreviewLength ? String.Format("{0}{1}", GetChunk(0, PreviewLength), Ellipsis) : Value;
		}

		public string GetChunk(int offset, int length)
		{
			var characters = new char[length];
			var characterCount = OracleLargeObjectHelper.ExecuteFunction(_clob.Connection, () => _clob.Read(characters, offset, length));
			return new string(characters, 0, characterCount);
		}

		public void Dispose()
		{
			_clob.Dispose();
		}

		public override string ToString()
		{
			return Preview;
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
					: OracleLargeObjectHelper.ExecuteFunction(_blob.Connection, () => _blob.Value);
		}

		public OracleBlobValue(OracleBlob blob)
		{
			_blob = blob;
			Length = _blob.Length;
		}

		public byte[] GetChunk(int offset, int length)
		{
			var buffer = new byte[length];
			var bytesRead = OracleLargeObjectHelper.ExecuteFunction(_blob.Connection, () => _blob.Read(buffer, offset, length));
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

		public byte[] GetChunk(int offset, int length)
		{
			if (offset != 0)
			{
				throw new NotSupportedException("'offset' parameter is not supported. ");
			}

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
