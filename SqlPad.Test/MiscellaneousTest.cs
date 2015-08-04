using System;
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
				var dataTypes =
					new Dictionary<string, Type>
					{
						{ "INT", typeof(int) },
						{ "VARCHAR", typeof(string) }
					};

				var readOnlyDataTypes = new ReadOnlyDictionary<string, Type>(dataTypes);

				var executionResult =
					new StatementExecutionResult
					{
						ResultInfoColumnHeaders =
							new Dictionary<ResultInfo, IReadOnlyList<ColumnHeader>>
							{
								{
									new ResultInfo(),
									new[]
									{
										new ColumnHeader { ColumnIndex = 0, Name = "TestColumn1", DataType = typeof (int) },
										new ColumnHeader { ColumnIndex = 1, Name = "TestColumn2", DataType = typeof (string) }
									}
								}
							},
						Statement =
							new StatementExecutionModel
							{
								StatementText = "SELECT @testBindVariable1 TestColumn1, @testBindVariable2 TestColumn2",
								BindVariables =
									new[]
									{
										new BindVariableModel(new BindVariableConfiguration { Name = "testBindVariable1", DataType = "INT", DataTypes = readOnlyDataTypes }),
										new BindVariableModel(new BindVariableConfiguration { Name = "testBindVariable2", DataType = "VARCHAR", DataTypes = readOnlyDataTypes })
									}
							}
					};

				var builder = new StringBuilder();
				using (var writer = new StringWriter(builder))
				{
					CSharpQueryClassGenerator.Generate(executionResult, writer);
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

	private IEnumerable<ResultRow> Execute(Int32 testBindVariable1, String testBindVariable2)
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