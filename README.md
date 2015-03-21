SQLPad 0.2.0.116
================

SQLPad is an experimental SQL editor focused to quick and comfortable work.
The primary reason for it was to find out why there are not any SQL editors with advanced features
like programming IDEs with their intellisense and semantic analysis. To disprove it's not/hardly possible.

Features
--------
SQLPad is mainly inspired by ReSharper plugin to Visual Studio and LINQPad. Provides lightweight editor
but aims at thorough semantic analysis and refactoring features. The main driving factor is to reduce
dummy monkey work to minimum while focusing to results or analysis of a problem.

SQLPad provides:
* Smart code completion - schemas, objects, columns, functions, packages, sequences, types, etc.
* Semantic analysis - validating references to various database objects, immediately notifying about errors or problems.
* Fast overview over database object properties.

SQLPad consists of a generic core application providing the user experience and a vendor specific module implementing
the parser, analyzer, validator and other vendor specific components. As of now the only supported vendor is Oracle (11g, 12c).

Since the implementation is still very experimental there are tons of missing features. Primary focus of implementation are queries, then DML. But there is some DDL supported at least by grammar.

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

SHIFT + ALT + F11          - Find usages (column/object identifiers/aliases, functions, bind variables, literals)

ESC                        - Cancel executing command

F5                         - Refresh database metadata

CTRL + T                   - Creates new document

CTRL + F4                  - Closes current document

CTRL + ALT + SHIFT + Up    - Move content up - select column, group by/order by expression

CTRL + ALT + SHIFT + Down  - Move content down - select column, group by/order by expression

ALT + Delete               - Safe delete (column or object alias)

CTRL + SHIFT + U           - Set text to upper case

CTRL + SHIFT + L           - Set text to lower case

Context actions
---------------
#####@SELECT:

Wrap current query block as inline view

Wrap current query block as common table expression

Unquote - makes all query block columns case insensitive

Unnest inline view

Toggle quoted notation

Toggle fully qualified references

Clean redundant symbols

Add CREATE TABLE AS


#####@Identifier:

Clean redundant symbols

Resolve ambiguous column

Propagate column

Create script

Add missing column

Add to GROUP BY

Wrap current query block as inline view


#####@Row source:

Add alias

Create script


#####@Bind variable:

Convert to literal

Convert ORDER BY number column reference


#####@Literal

Convert to bind variable


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
.NET 4.5

[Oracle Data Access Components 12c - 32 bit](http://www.oracle.com/technetwork/topics/dotnet/downloads/net-downloads-160392.html) - unmanaged driver only (due to certain limitations of managed driver, e. g., lack of UDTs and XML types support)

Configuration
-------------

#####SqlPad.exe.config

Connection string must be Oracle ADO.NET compliant, see [http://www.oracle.com/technetwork/topics/dotnet/downloads/install121012-2088160.html](http://www.oracle.com/technetwork/topics/dotnet/downloads/install121012-2088160.html).

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
		</infrastructureConfigurations>
		<!-- other connection configurations -->
	</databaseConnectionConfiguration>

IsProduction - indicates connection to production system using red label

#####Configuration.xml

	<Configuration xmlns="http://husqvik.com/SqlPad/2014/02">
		<DataModel DataModelRefreshPeriod="60" />
		<ResultGrid DateFormat="yyyy-MM-dd HH:mm:ss" NullPlaceholder="(null)" />
	</Configuration>

DataModelRefreshPeriod - data dictionary refresh period in minutes; refresh can forced any time using F5

DateFormat - data grid date time format

NullPlaceholder - data grid NULL value representation

#####OracleConfiguration.xml

	<OracleConfiguration xmlns="http://husqvik.com/SqlPad/2014/08/Oracle">
		<ExecutionPlan>
			<TargetTable Name="EXPLAIN_PLAN" />
		</ExecutionPlan>
	</OracleConfiguration>

TargetTable - table name used for EXPLAIN PLAN function; table is not created automatically and must be created manually using script:

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
