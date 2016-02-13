using System.Threading;
using System.Threading.Tasks;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.Test
{
	public class OracleTestObjectScriptExtractor : IOracleObjectScriptExtractor
	{
		internal const string SelectionTableCreateScript =
@"CREATE TABLE ""HUSQVIK"".""SELECTION"" 
(""SELECTION_ID"" NUMBER, 
	""CURRENTSTARTED"" NUMBER, 
	""CURRENTCOMPLETES"" NUMBER, 
	""STATUS"" NUMBER, 
	""RESPONDENTBUCKET_ID"" NUMBER, 
	""PROJECT_ID"" NUMBER, 
	""NAME"" VARCHAR2(100)
   ) SEGMENT CREATION IMMEDIATE 
PCTFREE 10 PCTUSED 40 INITRANS 1 MAXTRANS 255 
NOCOMPRESS LOGGING
STORAGE(INITIAL 65536 NEXT 1048576 MINEXTENTS 1 MAXEXTENTS 2147483645
PCTINCREASE 0 FREELISTS 1 FREELIST GROUPS 1
BUFFER_POOL DEFAULT FLASH_CACHE DEFAULT CELL_FLASH_CACHE DEFAULT)
TABLESPACE ""TBS_HQ_PDB""";

		public Task<string> ExtractSchemaObjectScriptAsync(OracleSchemaObject schemaObject, CancellationToken cancellationToken, bool suppressUserCancellationException = false)
		{
			return Task.FromResult(SelectionTableCreateScript);
		}
	}
}