using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using NUnit.Framework;
using Shouldly;

namespace SqlPad.Test
{
	public class MiscellaneousTest
	{
		[TestFixture]
		public class ExtensionsTest
		{
			[Test]
			public void TestToHexString()
			{
				var bytes = Encoding.UTF8.GetBytes("d676čžřčýžřáýíýžážřá");
				var hextString = bytes.ToHexString();

				const string expectedResult = "64363736C48DC5BEC599C48DC3BDC5BEC599C3A1C3BDC3ADC3BDC5BEC3A1C5BEC599C3A1";
				hextString.ShouldBe(expectedResult);
			}
		}

		[TestFixture]
		public class CSharpQueryClassGeneratorTest
		{
			[Test]
			public void TestCSharpQueryClassGenerator()
			{
				var intType = new BindVariableType("INT", typeof(int), false);
				var varcharType = new BindVariableType("VARCHAR", typeof(string), false);

				var dataTypes =
					new Dictionary<string, BindVariableType>
					{
						{ intType.Name, intType },
						{ varcharType.Name, varcharType }
					};

				var readOnlyDataTypes = new ReadOnlyDictionary<string, BindVariableType>(dataTypes);

				var columnHeaders =
					new[]
					{
						new ColumnHeader { ColumnIndex = 0, Name = "TestColumn1", DataType = typeof (int) },
						new ColumnHeader { ColumnIndex = 1, Name = "TestColumn2", DataType = typeof (string) }
					};

				var statementModel =
					new StatementExecutionModel
					{
						StatementText = "SELECT @testBindVariable1 TestColumn1, @testBindVariable2 TestColumn2",
						BindVariables =
							new[]
							{
								new BindVariableModel(new BindVariableConfiguration { Name = "testBindVariable1", DataType = intType.Name, DataTypes = readOnlyDataTypes }),
								new BindVariableModel(new BindVariableConfiguration { Name = "testBindVariable2", DataType = varcharType.Name, DataTypes = readOnlyDataTypes })
							}
					};

				var builder = new StringBuilder();
				using (var writer = new StringWriter(builder))
				{
					CSharpQueryClassGenerator.Generate(statementModel, columnHeaders, writer);
				}

				var result = builder.ToString();

				const string expectedResult =
@"using System;
using System.Data;

public class Query
{
	private readonly IDbConnection _connection;

	private const string CommandText =
@""SELECT @testBindVariable1 TestColumn1, @testBindVariable2 TestColumn2"";

	public Query(IDbConnection connection)
	{
		_connection = connection;
	}

	private IEnumerable<ResultRow> Execute(System.Int32 testBindVariable1, System.String testBindVariable2)
	{
		using (var command = _connection.CreateCommand())
		{
			command.CommandText = CommandText;
			
			var parametertestBindVariable1 = command.CreateParameter();
			parametertestBindVariable1.Value = testBindVariable1;
			command.Parameters.Add(parametertestBindVariable1);

			var parametertestBindVariable2 = command.CreateParameter();
			parametertestBindVariable2.Value = testBindVariable2;
			command.Parameters.Add(parametertestBindVariable2);

			_connection.Open();

			using (var reader = command.ExecuteReader())
			{
				while (reader.Read())
				{
					var row =
						new ResultRow
						{
							TestColumn1 = GetReaderValue<Int32?>(reader[nameof(ResultRow.TestColumn1)]),
							TestColumn2 = GetReaderValue<String>(reader[nameof(ResultRow.TestColumn2)])
						};

					yield return row;
				}
			}

			_connection.Close();
		}
	}

	private static T GetReaderValue<T>(object value)
	{
		return value == DBNull.Value
			? default(T)
			: (T)value;
	}
}

public class ResultRow
{
	public Int32? TestColumn1 { get; set; }
	public String TestColumn2 { get; set; }
}
";
				result.ShouldBe(expectedResult);
			}
		}
	}
}