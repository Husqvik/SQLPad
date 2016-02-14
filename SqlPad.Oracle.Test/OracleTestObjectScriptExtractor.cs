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

		internal const string TablespaceCreateScript =
@"CREATE BIGFILE TABLESPACE ""TBS_HQ_PDB"" DATAFILE 
  'E:\ORACLE\ORADATA\HQ12C\HQ_PDB\TBS_HQ_PDB.DBF' SIZE 4294967296
  AUTOEXTEND ON NEXT 536870912 MAXSIZE 33554431M
  LOGGING ONLINE PERMANENT BLOCKSIZE 8192
  EXTENT MANAGEMENT LOCAL AUTOALLOCATE DEFAULT 
 NOCOMPRESS  SEGMENT SPACE MANAGEMENT AUTO
   ALTER DATABASE DATAFILE 
  'E:\ORACLE\ORADATA\HQ12C\HQ_PDB\TBS_HQ_PDB.DBF' RESIZE 15032385536";

		public Task<string> ExtractSchemaObjectScriptAsync(OracleSchemaObject schemaObject, CancellationToken cancellationToken)
		{
			return Task.FromResult(SelectionTableCreateScript);
		}

		public Task<string> ExtractNonSchemaObjectScriptAsync(string objectName, string objectType, CancellationToken cancellationToken)
		{
			return Task.FromResult(TablespaceCreateScript);
		}
	}
}