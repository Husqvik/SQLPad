using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlPad.Oracle.SemanticModel
{
	public class OracleReferenceDataSource : IReferenceDataSource
	{
		private readonly string _statementText;
		private readonly IReadOnlyList<string> _keyDataTypes;

		public IReadOnlyList<ColumnHeader> ColumnHeaders { get; }

		public string ObjectName { get; }

		public string ConstraintName { get; }

		public StatementExecutionModel CreateExecutionModel(object[] keys)
		{
			if (keys == null)
			{
				throw new ArgumentNullException(nameof(keys));
			}

			if (keys.Length != _keyDataTypes.Count)
			{
				throw new ArgumentException($"Invalid number of values - expected: {_keyDataTypes.Count}; actual: {keys.Length}", nameof(keys));
			}

			return
				new StatementExecutionModel
				{
					StatementText = _statementText,
					BindVariables =
						_keyDataTypes.Select(
							(t, i) =>
							{
								var keyValue = keys[i];
								var providerValue = keyValue as IValue;
								return new BindVariableModel(
									new BindVariableConfiguration
									{
										Name = $"KEY{i}",
										DataType = t,
										Value = providerValue == null ? keyValue : providerValue.RawValue
									});
							}
							).ToArray()
				};
		}

		internal OracleReferenceDataSource(string objectName, string constraintName, string statementText, IReadOnlyList<ColumnHeader> columnHeaders, IReadOnlyList<string> keyDataTypes)
		{
			if (columnHeaders == null)
			{
				throw new ArgumentNullException(nameof(columnHeaders));
			}

			if (keyDataTypes == null)
			{
				throw new ArgumentNullException(nameof(keyDataTypes));
			}

			if (keyDataTypes.Count == 0)
			{
				throw new ArgumentException("Reference data source requires at least on key. ", nameof(keyDataTypes));
			}

			if (columnHeaders.Count != keyDataTypes.Count)
			{
				throw new ArgumentException($"Number of column headers ({columnHeaders.Count}) does not match the number of key data types ({keyDataTypes.Count}). ", nameof(columnHeaders));
			}

			ObjectName = objectName;
			ConstraintName = constraintName;
			_statementText = statementText;
			ColumnHeaders = columnHeaders;
			_keyDataTypes = keyDataTypes;
		}
	}
}
