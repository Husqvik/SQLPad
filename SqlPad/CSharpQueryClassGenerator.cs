using System;
using System.IO;
using System.Text;

namespace SqlPad
{
	public static class CSharpQueryClassGenerator
	{
		private const string ExportClassTemplate =
@"using System;
using System.Data;

public class Query
{{
	private readonly IDbConnection _connection;

	private const string CommandText =
@""{0}"";

	public Query(IDbConnection connection)
	{{
		_connection = connection;
	}}

	private IEnumerable<ResultRow> Execute({1})
	{{
		using (var command = _connection.CreateCommand())
		{{
			command.CommandText = CommandText;
			{2}			_connection.Open();

			using (var reader = command.ExecuteReader())
			{{
				while (reader.Read())
				{{
					var row =
						new ResultRow
						{{
{3}
						}};

					yield return row;
				}}
			}}

			_connection.Close();
		}}
	}}

	private static T GetReaderValue<T>(object value)
	{{
		return value == DBNull.Value
			? default(T)
			: (T)value;
	}}
}}
";

		public static void Generate(StatementExecutionResult executionResult, TextWriter writer)
		{
			var columnMapBuilder = new StringBuilder();
			var resultRowPropertyBuilder = new StringBuilder();
			var bindVariableBuilder = new StringBuilder();
			var parameterBuilder = new StringBuilder();

			var index = 0;
			if (executionResult.Statement.BindVariables.Count > 0)
			{
				parameterBuilder.AppendLine();

				foreach (var bindVariable in executionResult.Statement.BindVariables)
				{
					index++;

					bindVariableBuilder.Append(bindVariable.InputType);
					bindVariableBuilder.Append(" ");
					bindVariableBuilder.Append(bindVariable.Name);

					if (index < executionResult.Statement.BindVariables.Count)
					{
						bindVariableBuilder.Append(", ");
					}

					var parameterName = $"parameter{bindVariable.Name}";
					parameterBuilder.Append("\t\t\tvar ");
					parameterBuilder.Append(parameterName);
					parameterBuilder.AppendLine(" = command.CreateParameter();");
					parameterBuilder.Append("\t\t\t");
					parameterBuilder.Append(parameterName);
					parameterBuilder.Append(".Value = ");
					parameterBuilder.Append(bindVariable.Name);
					parameterBuilder.AppendLine(";");
					parameterBuilder.Append("\t\t\tcommand.Parameters.Add(");
					parameterBuilder.Append(parameterName);
					parameterBuilder.AppendLine(");");
					parameterBuilder.AppendLine();
				}
			}

			index = 0;
			foreach (var column in executionResult.ColumnHeaders)
			{
				index++;

				var dataTypeName = String.Equals(column.DataType.Namespace, "System")
					? column.DataType.Name
					: column.DataType.FullName;

				if (column.DataType.IsValueType)
				{
					dataTypeName = $"{dataTypeName}?";
				}

				columnMapBuilder.Append("\t\t\t\t\t\t\t");
				columnMapBuilder.Append(column.Name);
				columnMapBuilder.Append(" = GetReaderValue<");
				columnMapBuilder.Append(dataTypeName);
				columnMapBuilder.Append(">(reader[nameof(ResultRow.");
				columnMapBuilder.Append(column.Name);
				columnMapBuilder.Append(")])");

				if (index < executionResult.ColumnHeaders.Count)
				{
					columnMapBuilder.AppendLine(",");
				}

				resultRowPropertyBuilder.Append("\tpublic ");
				resultRowPropertyBuilder.Append(dataTypeName);
				resultRowPropertyBuilder.Append(" ");
				resultRowPropertyBuilder.Append(column.Name);
				resultRowPropertyBuilder.AppendLine(" { get; set; }");
			}

			var statementText = executionResult.Statement.StatementText.Replace("\"", "\"\"");
			var queryClass = String.Format(ExportClassTemplate, statementText, bindVariableBuilder, parameterBuilder, columnMapBuilder);

			writer.WriteLine(queryClass);
			writer.WriteLine("public class ResultRow");
			writer.WriteLine("{");
			writer.Write(resultRowPropertyBuilder);
			writer.WriteLine("}");
		}
	}
}
