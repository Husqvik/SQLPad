#SQLPad 0.4.0.421

|Builds|Oracle.DataAccess|Oracle.ManagedDataAccess|
|:--:|:--:|:--:|
|**master**|[![Oracle.DataAccess](https://ci.appveyor.com/api/projects/status/t4iti5bn5ubs2k3k?svg=true&pendingText=pending&passingText=passed&failingText=failed)](https://ci.appveyor.com/project/Husqvik/sqlpad-j6chv)|[![Oracle.ManagedDataAccess](https://ci.appveyor.com/api/projects/status/m0pnyw7gp73tg59c?svg=true&pendingText=pending&passingText=passed&failingText=failed)](https://ci.appveyor.com/project/Husqvik/sqlpad)|

Summary
-------
SQLPad is an experimental SQL editor focused to quick and comfortable work.
The primary reason for it was to find out why there are not any SQL editors with advanced features
like programming IDEs with their intellisense and semantic analysis. To disprove it's not/hardly possible.

Features
--------
SQLPad is mainly inspired by ReSharper plugin to Visual Studio and LINQPad. Provides lightweight editor
but aims at thorough semantic analysis and refactoring features. The main driving factor is to reduce
dummy monkey work to minimum while focusing to results or analysis of a problem.

SQLPad provides:
* Smart code completion - schemas, objects, columns, functions, packages, sequences, types, etc;
* Semantic analysis - validating references to various database objects, immediately notifying about errors or problems;
* Fast overview over database object properties;
* Data export to various formats;
* Execution monitor.

SQLPad consists of a generic core application providing the user experience and a vendor specific module implementing
the parser, analyzer, validator and other vendor specific components. As of now the only supported vendor is Oracle (11g, 12c).

Since the implementation is still very experimental there are tons of missing features. Primary focus of implementation are queries, then DML. There is also limited support of PL/SQL. But there is some DDL supported at least by grammar.

Commands
--------
CTRL + Minus               - Go to previous edit

CTRL + SHIFT + Minus       - Go to next edit

CTRL + E                   - Explain plan

CTRL + Space               - Invoke code completion (depends on caret position)

CTRL + SHIFT + Space       - Show function overloads (only in function parameter list)

CTRL + D                   - Duplicate line/selection

CTRL + SHIFT + /           - Block comment

CTRL + ALT + /             - Line comment

ALT + Enter                - Context actions (depends on caret position)

CTRL + ALT + PageUp        - Navigate to previous highlight

CTRL + ALT + PageDown      - Navigate to next highlight

SHIFT + ALT + PageUp        - Navigate to previous error

SHIFT + ALT + PageDown      - Navigate to next error

CTRL + ALT + Home          - Navigate to query block root (related SELECT keyword)

F12				           - Navigate to definition, e. g., when column is propagated via multiple inline views

CTRL + Enter               - Execute command (hold SHIFT to gather execution statistics)

F9                         - Execute command (hold SHIFT to gather execution statistics)

CTRL + D                   - Duplicate line/selection

CTRL + S                   - Save current document

CTRL + SHIFT + S           - Save all open documents

CTRL + Comma               - Show recent documents

CTRL + ALT + F             - Format statement

SHIFT + ALT + F            - Format statement into single line

CTRL + SHIFT + F           - Statement normalization

SHIFT + ALT + F11          - Find usages (column/object identifiers/aliases, functions, bind variables, literals)

ESC                        - Cancel executing command

F5                         - Refresh database metadata

CTRL + T                   - Creates new document

CTRL + F4                  - Closes current document

CTRL + ALT + SHIFT + Up    - Move content up - select column, group by/order by expression

CTRL + ALT + SHIFT + Down  - Move content down - select column, group by/order by expression

ALT + Delete               - Safe delete (column or object alias)

CTRL + SHIFT + U           - Set text to upper case

CTRL + U                   - Set text to lower case

CTRL + SHIFT + Comma       - Decrease editor font size

CTRL + SHIFT + Period      - Increase editor font size

ALT + INSERT               - Code generation items

F1                         - Show documentation (built-in SQL functions and package functions, statements, data dictionary objects)

CTRL + ALT + H             - Show execution history

SHIFT + F6                 - Rename symbol

CTRL + SHIFT + V           - Clipboard history

CTRL + ALT + M             - Database monitor

Context actions
---------------
#####@SELECT:

Add CREATE TABLE AS

Clean redundant symbols

Toggle quoted notation

Toggle fully qualified references

Unnest inline view

Unquote - makes all query block columns case insensitive

Wrap current query block as common table expression

Wrap current query block as inline view


#####@Identifier:

Add missing column

Add to GROUP BY

Add to ORDER BY

Clean redundant symbols

Create script

Expand view

Propagate column

Resolve ambiguous column

Wrap current query block as inline view

Add named parameters

#####@Row source:

Add alias

Create script


#####@Bind variable:

Convert ORDER BY number column reference

Convert to literal

#####@Literal

Convert to bind variable

Split string


#####@*

Expand asterisk (hold SHIFT to select specific column)


#####@INTO

Add columns (hold SHIFT to select specific column)

#####@XMLTABLE, JSONTABLE, TABLE

Wrap current query block as inline view

#####@ORDER, BY:

Convert ORDER BY number column reference

Requirements
------------
Microsoft .NET 4.6

[Oracle Data Access Components 12c - 32 bit](http://www.oracle.com/technetwork/topics/dotnet/downloads/net-downloads-160392.html) - unmanaged driver preferred (due to certain limitations of managed driver, e. g., lack of XML and UDT types support)

Configuration
-------------

#####SqlPad.exe.config

Connection string must be Oracle ADO.NET compliant, see [http://www.oracle.com/technetwork/topics/dotnet/install121024-2704210.html](http://www.oracle.com/technetwork/topics/dotnet/install121024-2704210.html). Managed driver might require additional configuration, see [https://docs.oracle.com/html/E41125_02/featConfig.htm#BABEGGHD](https://docs.oracle.com/html/E41125_02/featConfig.htm#BABEGGHD);

	<connectionStrings>
		<clear/>
		<add name="Connection name 1" providerName="Oracle.DataAccess.Client" connectionString="DATA SOURCE=TNS_name;PASSWORD=password1;USER ID=user_name1" />
		<add name="Connection name 2" providerName="Oracle.DataAccess.Client" connectionString="DATA SOURCE=host:1521/service_name;PASSWORD=password2;USER ID=user_name2" />
		<!-- other connection strings -->
	</connectionStrings>

Each connection string requires an infrastructure factory configuration record:

	<databaseConnectionConfiguration>
		<infrastructureConfigurations>
			<infrastructure ConnectionStringName="Connection name 1" InfrastructureFactory="SqlPad.Oracle.OracleInfrastructureFactory, SqlPad.Oracle" />
			<infrastructure ConnectionStringName="Connection name 2" InfrastructureFactory="SqlPad.Oracle.OracleInfrastructureFactory, SqlPad.Oracle" IsProduction="true" />
			<!-- other connection configurations -->
		</infrastructureConfigurations>
	</databaseConnectionConfiguration>

`IsProduction` - indicates connection to a production system using red label

#####Configuration.xml

	<Configuration xmlns="http://husqvik.com/SqlPad/2014/02">
		<DataModel DataModelRefreshPeriod="60" />
		<ResultGrid DateFormat="yyyy-MM-dd HH:mm:ss" NullPlaceholder="(null)" FetchRowsBatchSize="500" />
		<Editor IndentationSize="2" />
	</Configuration>

`DataModelRefreshPeriod` - data dictionary refresh period in minutes; refresh can forced any time using `F5`

`DateFormat` - data grid date time format (.NET format)

`NullPlaceholder` - data grid NULL value representation (default '(null)')

`FetchRowsBatchSize` - number of rows fetched in one batch (default 100)

`IndentationSize` - number of spaces for tab indentation (default 4)

#####OracleConfiguration.xml

	<OracleConfiguration xmlns="http://husqvik.com/SqlPad/2014/08/Oracle">
		<StartupScript>
			-- Enter optional global script to execute when database connection is established.
		</StartupScript>
		<TKProfPath>c:\Oracle\product\12.1.0\dbhome_1\BIN\tkprof.exe</TKProfPath>
		<Connections>
			<Connection ConnectionName="Oracle 12c PDB EZ-Connect" RemoteTraceDirectory="c:\Oracle\diag\rdbms\hq12c\hq12c\trace\">
				<StartupScript>
					-- Enter optional script to execute when database connection is established.
				</StartupScript>
				<ExecutionPlan>
					<TargetTable Name="EXPLAIN_PLAN" />
				</ExecutionPlan>
			</Connection>
		</Connections>
		<Formatter>
			<FormatOptions Identifier="Lower" Alias="Lower" Keyword="Upper" ReservedWord="Upper" />
		</Formatter>
	</OracleConfiguration>

`StartupScript` - optional initialization script executed when connection to database is established

`RemoteTraceDirectory` - optional remote database trace directory; enables access to remote trace files from SQL Pad

`TKProfPath` - optional path to `TKProf.exe`; enables transient kernel profile output for extended SQL trace (10046) files.

`ExecutionPlan/TargetTable` - table name used for EXPLAIN PLAN function; table is not created automatically and must be created manually using script:

	CREATE GLOBAL TEMPORARY TABLE EXPLAIN_PLAN
	(
		STATEMENT_ID VARCHAR2(30), 
		PLAN_ID NUMBER, 
		TIMESTAMP DATE, 
		REMARKS VARCHAR2(4000), 
		OPERATION VARCHAR2(30), 
		OPTIONS VARCHAR2(255), 
		OBJECT_NODE VARCHAR2(128), 
		OBJECT_OWNER VARCHAR2(30), 
		OBJECT_NAME VARCHAR2(30), 
		OBJECT_ALIAS VARCHAR2(65), 
		OBJECT_INSTANCE INTEGER, 
		OBJECT_TYPE VARCHAR2(30), 
		OPTIMIZER VARCHAR2(255), 
		SEARCH_COLUMNS NUMBER, 
		ID INTEGER, 
		PARENT_ID INTEGER, 
		DEPTH INTEGER, 
		POSITION INTEGER, 
		COST INTEGER, 
		CARDINALITY INTEGER, 
		BYTES INTEGER, 
		OTHER_TAG VARCHAR2(255), 
		PARTITION_START VARCHAR2(255), 
		PARTITION_STOP VARCHAR2(255), 
		PARTITION_ID INTEGER, 
		OTHER LONG, 
		DISTRIBUTION VARCHAR2(30), 
		CPU_COST INTEGER, 
		IO_COST INTEGER, 
		TEMP_SPACE INTEGER, 
		ACCESS_PREDICATES VARCHAR2(4000), 
		FILTER_PREDICATES VARCHAR2(4000), 
		PROJECTION VARCHAR2(4000), 
		TIME INTEGER, 
		QBLOCK_NAME VARCHAR2(30), 
		OTHER_XML CLOB
	)
	ON COMMIT PRESERVE ROWS;

	CREATE OR REPLACE PUBLIC SYNONYM EXPLAIN_PLAN FOR EXPLAIN_PLAN;

Screenshots
------------------
#####Overview
![SQLPad overview 1](https://raw.githubusercontent.com/Husqvik/SQLPad/master/Screenshots/Overview1.png)
![SQLPad overview 2](https://raw.githubusercontent.com/Husqvik/SQLPad/master/Screenshots/Overview2.png)
![SQLPad overview 3](https://raw.githubusercontent.com/Husqvik/SQLPad/master/Screenshots/Overview3.png)
![SQLPad overview 4](https://raw.githubusercontent.com/Husqvik/SQLPad/master/Screenshots/Overview4.png)
#####Tooltips
![Table tooltip detail](https://raw.githubusercontent.com/Husqvik/SQLPad/master/Screenshots/TableTooltip.png)
![Column tooltip detail](https://raw.githubusercontent.com/Husqvik/SQLPad/master/Screenshots/ColumnTooltip.png)
#####Code completion
![Code completion 1](https://raw.githubusercontent.com/Husqvik/SQLPad/master/Screenshots/CodeComplete1.png)
![Code completion 2](https://raw.githubusercontent.com/Husqvik/SQLPad/master/Screenshots/CodeComplete2.png)
![Code completion 3](https://raw.githubusercontent.com/Husqvik/SQLPad/master/Screenshots/CodeComplete3.png)
#####Miscellaneous
![Expand asterisk details](https://raw.githubusercontent.com/Husqvik/SQLPad/master/Screenshots/ExpandAsterisk.png)
![Function overload list](https://raw.githubusercontent.com/Husqvik/SQLPad/master/Screenshots/FunctionOverloads.png)
![Execution statistics](https://raw.githubusercontent.com/Husqvik/SQLPad/master/Screenshots/ExecutionStatistics.png)
![Database output](https://raw.githubusercontent.com/Husqvik/SQLPad/master/Screenshots/DatabaseOutput.png)
![Trace events](https://raw.githubusercontent.com/Husqvik/SQLPad/master/Screenshots/TraceEvents.png)
![Statement execution history](https://raw.githubusercontent.com/Husqvik/SQLPad/master/Screenshots/StatementExecutionHistory.png)
#####Database Monitor
![Database Monitor overview 1](https://raw.githubusercontent.com/Husqvik/SQLPad/master/Screenshots/DatabaseMonitor1.png)

Known issues
------------

ANYDATA data type not supported because Oracle Data Access does not support it. Possible use of Devart Oracle provider.

Support for user defined types (UDT) is very cumbersome (and not enabled by default) due to Oracle Data Access implementation. Devart Oracle provider also solves this.

