using System;
using System.Collections;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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
	public static class OracleDataAccessExtensions
	{
		private const int MaximumValueSize = 32767;
		private const int MaximumCharacterValueSize = 2000;
		private static readonly FieldInfo FieldReaderInternalTypes = typeof(OracleDataReader).GetField("m_oraType", BindingFlags.Instance | BindingFlags.NonPublic);
		private static readonly FieldInfo FieldCommandParameters = typeof(OracleCommand).GetField("m_parameters", BindingFlags.Instance | BindingFlags.NonPublic);

		internal static OracleParameter AddSimpleParameter(this OracleCommand command, string parameterName, object value, string databaseType = null, int? size = null)
		{
			var parameter = command.CreateParameter();
			parameter.ParameterName = parameterName;
			parameter.Direction = ParameterDirection.InputOutput;
			parameter.Value = Equals(value, String.Empty) ? null : value;

			switch (databaseType)
			{
				case TerminalValues.Char:
					parameter.OracleDbType = OracleDbType.Char;
					parameter.Size = MaximumCharacterValueSize;
					break;
				case TerminalValues.Clob:
					parameter.OracleDbType = OracleDbType.Clob;
					break;
				case TerminalValues.Timestamp:
					parameter.OracleDbType = OracleDbType.TimeStamp;
					break;
				case TerminalValues.Date:
					parameter.OracleDbType = OracleDbType.Date;
					break;
				case TerminalValues.Number:
					parameter.OracleDbType = OracleDbType.Decimal;
					break;
				case TerminalValues.NChar:
					parameter.OracleDbType = OracleDbType.NChar;
					parameter.Size = MaximumCharacterValueSize;
					break;
				case OracleBindVariable.DataTypeUnicodeClob:
					parameter.OracleDbType = OracleDbType.NClob;
					break;
				case TerminalValues.NVarchar2:
					parameter.OracleDbType = OracleDbType.NVarchar2;
					parameter.Size = MaximumValueSize / 2;
					break;
				case TerminalValues.Varchar2:
					parameter.OracleDbType = OracleDbType.Varchar2;
					parameter.Size = MaximumValueSize;
					break;
				case OracleBindVariable.DataTypeRefCursor:
					parameter.OracleDbType = OracleDbType.RefCursor;
					break;
				case TerminalValues.Raw:
					parameter.Value = value as byte[] ?? HexStringToByteArray((string)value);
					parameter.OracleDbType = OracleDbType.Raw;
					parameter.Size = MaximumValueSize;
					break;
				case TerminalValues.Blob:
					parameter.Value = value as byte[] ?? HexStringToByteArray((string)value);
					parameter.OracleDbType = OracleDbType.Blob;
					break;
			}

			if (size.HasValue)
			{
				parameter.Size = size.Value;
			}

			command.Parameters.Add(parameter);

			return parameter;
		}

		private static readonly ArgumentException InvalidHexFormatException = new ArgumentException("Binary value must be entered in hexadecimal format. ");

		private static byte[] HexStringToByteArray(string hexString)
		{
			if (string.IsNullOrEmpty(hexString))
			{
				return null;
			}

			var hexStringLength = hexString.Length;
			if (hexStringLength % 2 == 1)
			{
				throw InvalidHexFormatException;
			}

			hexString = hexString.ToUpperInvariant();
			var byteArray = new byte[hexStringLength / 2];
			for (var i = 0; i < hexStringLength; i += 2)
			{
				var firstCharacter = hexString[i];
				var secondCharacter = hexString[i + 1];
				if (firstCharacter > 'F' || secondCharacter > 'F' || firstCharacter < '0' || secondCharacter < '0' ||
					(firstCharacter > '9' && firstCharacter < 'A') || (secondCharacter > '9' && secondCharacter < 'A'))
				{
					throw InvalidHexFormatException;
				}

				var byteValue = (firstCharacter > 0x40 ? firstCharacter - 0x37 : firstCharacter - 0x30) << 4;
				byteValue += secondCharacter > 0x40 ? secondCharacter - 0x37 : secondCharacter - 0x30;
				byteArray[i / 2] = Convert.ToByte(byteValue);
			}

			return byteArray;
		}

		public static int[] GetInternalDataTypes(this OracleDataReader reader)
		{
#if ORACLE_MANAGED_DATA_ACCESS_CLIENT
			return new int[reader.FieldCount];
#else
			return ((IEnumerable)FieldReaderInternalTypes.GetValue(reader)).Cast<int>().ToArray();
#endif
		}

		public static void ResetParametersToAvoidOdacBug(this OracleCommand command)
		{
			FieldCommandParameters.SetValue(command, null);
		}

		public static Task<bool> ReadAsynchronous(this OracleDataReader reader, CancellationToken cancellationToken)
		{
			return App.ExecuteAsynchronous(reader.Close, reader.Read, cancellationToken);
		}

		public static Task<object> GetValueAsynchronous(this OracleDataReader reader, int index, CancellationToken cancellationToken)
		{
			return App.ExecuteAsynchronous(reader.Close, () => reader.GetValue(index), cancellationToken);
		}

		public static Task<int> ExecuteNonQueryAsynchronous(this OracleCommand command, CancellationToken cancellationToken)
		{
			return App.ExecuteAsynchronous(command.CancelIgnoreFailure, command.ExecuteNonQuery, cancellationToken);
		}

		public static Task<object> ExecuteScalarAsynchronous(this OracleCommand command, CancellationToken cancellationToken)
		{
			return App.ExecuteAsynchronous(command.CancelIgnoreFailure, command.ExecuteScalar, cancellationToken);
		}

		public static Task<OracleDataReader> ExecuteReaderAsynchronous(this OracleCommand command, CommandBehavior behavior, CancellationToken cancellationToken)
		{
			return App.ExecuteAsynchronous(command.CancelIgnoreFailure, () => command.ExecuteReader(behavior), cancellationToken);
		}

		public static Task OpenAsynchronous(this OracleConnection connection, CancellationToken cancellationToken)
		{
			return App.ExecuteAsynchronous<object>(delegate { /* ODAC does not support cancellation of a connection being opened. */ }, delegate { connection.Open(); return null; }, cancellationToken);
		}

		public static Task CloseAsynchronous(this OracleConnection connection, CancellationToken cancellationToken)
		{
			return App.ExecuteAsynchronous<object>(delegate { }, delegate { connection.Close(); return null; }, cancellationToken);
		}

		public static Task<int> ReadAsynchronous(this OracleBlob blob, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			return App.ExecuteAsynchronous(delegate { }, () => blob.Read(buffer, offset, count), cancellationToken);
		}

		public static Task<int> ReadAsynchronous(this OracleClob clob, char[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			return App.ExecuteAsynchronous(delegate { }, () => clob.Read(buffer, offset, count), cancellationToken);
		}

		public static Task<int> ReadAsynchronous(this OracleXmlStream xmlStream, char[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			return App.ExecuteAsynchronous(delegate { }, () => xmlStream.Read(buffer, offset, count), cancellationToken);
		}

		public static Task RollbackAsynchronous(this OracleTransaction transaction)
		{
			return App.ExecuteAsynchronous<object>(delegate { }, delegate { transaction.Rollback(); return null; }, CancellationToken.None);
		}

		public static Task CommitAsynchronous(this OracleTransaction transaction)
		{
			return App.ExecuteAsynchronous<object>(delegate { }, delegate { transaction.Commit(); return null; }, CancellationToken.None);
		}

		private static void CancelIgnoreFailure(this OracleCommand command)
		{
			try
			{
				command.Cancel();
			}
			catch (Exception e)
			{
				Trace.WriteLine($"Command cancellation failed: {e}");
			}
		}

		public static async Task<bool> EnsureConnectionOpen(this OracleConnection connection, string connectionString, CancellationToken cancellationToken)
		{
			if (connection.State == ConnectionState.Open)
			{
				return false;
			}

			connection.ConnectionString = connectionString;
			await connection.OpenAsynchronous(cancellationToken);
			return true;
		}

		public static async Task SetSchema(this OracleCommand command, string schema, CancellationToken cancellationToken)
		{
			command.CommandText = $"ALTER SESSION SET CURRENT_SCHEMA = \"{schema}\"";
			await command.ExecuteNonQueryAsynchronous(cancellationToken);
		}
	}

	public static class OracleReaderValueConvert
	{
		public static int? ToInt32(object value)
		{
			return value.IsNull() ? null : (int?)Convert.ToInt32(value);
		}

		public static long? ToInt64(object value)
		{
			return value.IsNull() ? null : (long?)Convert.ToInt64(value);
		}

		public static decimal? ToDecimal(object value)
		{
			return value.IsNull() ? null : (decimal?)Convert.ToDecimal(value);
		}

		public static DateTime? ToDateTime(object value)
		{
			return value.IsNull() ? null : (DateTime?)Convert.ToDateTime(value);
		}

		public static TimeSpan? ToTimeSpanFromMicroseconds(object value)
		{
			return value.IsNull() ? null : (TimeSpan?)TimeSpan.FromTicks(Convert.ToInt64(value) * 10);
		}

		public static string ToString(object value)
		{
			return value.IsNull() ? String.Empty : (string)value;
		}

		public static bool? ToBoolean(object value)
		{
			return value.IsNull() ? null : (bool?)value;
		}

		private static bool IsNull(this object value)
		{
			return value == DBNull.Value;
		}
	}
}
