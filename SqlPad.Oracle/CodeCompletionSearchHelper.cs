using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.DataDictionary;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle
{
	internal static class CodeCompletionSearchHelper
	{
		private const string ContextNamespaceUserEnvironment = "USERENV";

		private static readonly HashSet<OracleProgramIdentifier> SpecificCodeCompletionFunctionIdentifiers =
			new HashSet<OracleProgramIdentifier>
			{
				OracleProgramIdentifier.IdentifierBuiltInProgramConvert,
				OracleProgramIdentifier.IdentifierBuiltInProgramRegularExpressionReplace,
				OracleProgramIdentifier.IdentifierBuiltInProgramRegularExpressionCount,
				OracleProgramIdentifier.IdentifierBuiltInProgramRegularExpressionInstr,
				OracleProgramIdentifier.IdentifierBuiltInProgramRegularExpressionSubstring,
				OracleProgramIdentifier.IdentifierBuiltInProgramRound,
				OracleProgramIdentifier.IdentifierBuiltInProgramToChar,
				OracleProgramIdentifier.IdentifierBuiltInProgramTrunc,
				OracleProgramIdentifier.IdentifierBuiltInProgramToDate,
				OracleProgramIdentifier.IdentifierBuiltInProgramToNumber,
				OracleProgramIdentifier.IdentifierBuiltInProgramToTimestamp,
				OracleProgramIdentifier.IdentifierBuiltInProgramToTimestampWithTimeZone,
				OracleProgramIdentifier.IdentifierBuiltInProgramSysContext,
				OracleProgramIdentifier.IdentifierBuiltInProgramNextDay,
				OracleProgramIdentifier.IdentifierBuiltInProgramNumberToYearToMonthInterval,
				OracleProgramIdentifier.IdentifierBuiltInProgramNumberToDayToSecondInterval,
				OracleProgramIdentifier.IdentifierBuiltInProgramBFileName,
				OracleProgramIdentifier.IdentifierDbmsRandomString,
				OracleProgramIdentifier.IdentifierDbmsCryptoHash,
				OracleProgramIdentifier.IdentifierGatherSchemaStats,
				OracleProgramIdentifier.IdentifierGatherTableStats
			};

		public static IEnumerable<ICodeCompletionItem> ResolveSpecificFunctionParameterCodeCompletionItems(StatementGrammarNode currentNode, IEnumerable<OracleCodeCompletionFunctionOverload> functionOverloads, OracleDatabaseModelBase databaseModel)
		{
			var completionItems = new List<ICodeCompletionItem>();
			var specificFunctionOverloads =
				functionOverloads
					.Where(o => SpecificCodeCompletionFunctionIdentifiers.Contains(o.ProgramMetadata.Identifier) || IsDbmsMetadata(o.ProgramMetadata))
					.ToArray();

			if (specificFunctionOverloads.Length == 0 || !currentNode.Id.In(Terminals.StringLiteral, Terminals.NumberLiteral, Terminals.LeftParenthesis, Terminals.Comma))
			{
				return completionItems;
			}

			var truncFunctionOverload = specificFunctionOverloads
				.FirstOrDefault(o =>
					o.CurrentParameterIndex == 1 && o.ProgramMetadata.Identifier.In(OracleProgramIdentifier.IdentifierBuiltInProgramTrunc, OracleProgramIdentifier.IdentifierBuiltInProgramRound) &&
					String.Equals(o.ProgramMetadata.Parameters[o.CurrentParameterIndex + 1].DataType, "VARCHAR2"));

			if (truncFunctionOverload != null && HasSingleStringLiteralParameterOrNoParameterToken(truncFunctionOverload))
			{
				var addRoundInformation = truncFunctionOverload.ProgramMetadata.Identifier == OracleProgramIdentifier.IdentifierBuiltInProgramRound;
				completionItems.Add(BuildParameterCompletionItem(currentNode, "CC", "CC - One greater than the first two digits of a four-digit year"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "YYYY", $"YYYY (YEAR) - Year{(addRoundInformation ? " (rounds up on July 1)" : null)}"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "I", "I (IYYY) - ISO Year"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "Q", $"Q - Quarter{(addRoundInformation ? " (rounds up on the sixteenth day of the second month of the quarter)" : null)}"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "MM", $"MM (MON, MONTH) - Month{(addRoundInformation ? " (rounds up on the sixteenth day)" : null)}"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "WW", "WW - Same day of the week as the first day of the year"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "IW", "IW - Same day of the week as the first day of the ISO year"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "W", "W - Same day of the week as the first day of the month"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "D", "D (DY, DAY) - Starting day of the week"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "HH", "HH - Hour"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "MI", "MI - Minute"));
			}

			var toCharFunctionOverload = specificFunctionOverloads
				.FirstOrDefault(o =>
					o.CurrentParameterIndex == 1 && o.ProgramMetadata.Identifier == OracleProgramIdentifier.IdentifierBuiltInProgramToChar &&
					String.Equals(o.ProgramMetadata.Parameters[o.CurrentParameterIndex + 1].DataType, "VARCHAR2"));

			if (toCharFunctionOverload != null && HasSingleStringLiteralParameterOrNoParameterToken(toCharFunctionOverload))
			{
				BuildCommonDateFormatCompletionItems(currentNode, completionItems);
				completionItems.Add(BuildParameterCompletionItem(currentNode, "J", "J - Julian day; the number of days since January 1, 4712 BC"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "JSP", "JSP - Julian day spelled"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "Q", "Q - Quarter of year (1, 2, 3, 4; January - March = 1)"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "WW", "WW - Week of year (1-53) where week 1 starts on the first day of the year and continues to the seventh day of the year"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "W", "W - Week of month (1-5) where week 1 starts on the first day of the month and ends on the seventh"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "IW", "IW - Week of year (1-52 or 1-53) based on the ISO standard"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "E", "E - Abbreviated era name (Japanese Imperial, ROC Official, and Thai Buddha calendars)"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "EE", "EE - Full era name (Japanese Imperial, ROC Official, and Thai Buddha calendars)"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "CC", "CC - Century"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "YEAR", "YEAR - Year, spelled out"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "SYEAR", "SYEAR - Year, spelled out; prefixes BC dates with a minus sign (-)"));
			}

			toCharFunctionOverload = specificFunctionOverloads
				.FirstOrDefault(o =>
					o.CurrentParameterIndex == 2 && o.ProgramMetadata.Identifier.In(OracleProgramIdentifier.IdentifierBuiltInProgramToChar, OracleProgramIdentifier.IdentifierBuiltInProgramToNumber) &&
					String.Equals(o.ProgramMetadata.Parameters[o.CurrentParameterIndex + 1].DataType, "VARCHAR2"));

			if (toCharFunctionOverload != null && HasSingleStringLiteralParameterOrNoParameterToken(toCharFunctionOverload))
			{
				const string itemText = "NLS_NUMERIC_CHARACTERS = '<decimal separator><group separator>' NLS_CURRENCY = 'currency_symbol' NLS_ISO_CURRENCY = <territory> NLS_DATE_LANGUAGE = <language>";
				const string itemDescription = "NLS_NUMERIC_CHARACTERS = '<decimal separator><group separator>' NLS_CURRENCY = 'currency_symbol' NLS_ISO_CURRENCY = <territory> NLS_DATE_LANGUAGE = <language>";
				completionItems.Add(BuildParameterCompletionItem(currentNode, itemText, itemDescription));
			}

			var toToDateOrTimestampFunctionOverload = specificFunctionOverloads
				.FirstOrDefault(
					o => o.CurrentParameterIndex == 1 && o.ProgramMetadata.Identifier.In(OracleProgramIdentifier.IdentifierBuiltInProgramToDate, OracleProgramIdentifier.IdentifierBuiltInProgramToTimestamp, OracleProgramIdentifier.IdentifierBuiltInProgramToTimestampWithTimeZone) &&
					     String.Equals(o.ProgramMetadata.Parameters[o.CurrentParameterIndex + 1].DataType, "VARCHAR2"));

			if (toToDateOrTimestampFunctionOverload != null && HasSingleStringLiteralParameterOrNoParameterToken(toToDateOrTimestampFunctionOverload))
			{
				BuildCommonDateFormatCompletionItems(currentNode, completionItems);
				completionItems.Add(BuildParameterCompletionItem(currentNode, "J", "J - Julian day; the number of days since January 1, 4712 BC. Number specified with J must be integers. "));
			}

			var toToDateOrTimestampFunctionNlsParameter = specificFunctionOverloads
				.FirstOrDefault(
					o => o.CurrentParameterIndex == 2 && o.ProgramMetadata.Identifier.In(OracleProgramIdentifier.IdentifierBuiltInProgramToDate, OracleProgramIdentifier.IdentifierBuiltInProgramToTimestamp, OracleProgramIdentifier.IdentifierBuiltInProgramToTimestampWithTimeZone) &&
						 String.Equals(o.ProgramMetadata.Parameters[o.CurrentParameterIndex + 1].DataType, "VARCHAR2"));

			if (toToDateOrTimestampFunctionNlsParameter != null && HasSingleStringLiteralParameterOrNoParameterToken(toToDateOrTimestampFunctionNlsParameter))
			{
				completionItems.Add(BuildParameterCompletionItem(currentNode, "NLS_DATE_LANGUAGE = <language>", "NLS_DATE_LANGUAGE = <language>"));
			}

			var randomStringFunctionOverload = specificFunctionOverloads
				.FirstOrDefault(o => IsParameterSupported(o, 0, "\"OPT\"") && o.ProgramMetadata.Identifier.In(OracleProgramIdentifier.IdentifierDbmsRandomString));
			if (randomStringFunctionOverload != null && HasSingleStringLiteralParameterOrNoParameterToken(randomStringFunctionOverload))
			{
				completionItems.Add(BuildParameterCompletionItem(currentNode, "U", "U (u) - uppercase alpha characters"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "L", "L (l) - lowercase alpha characters"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "A", "A (a) - mixed case alpha characters"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "X", "X (x) - uppercase alpha-numeric characters"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "P", "P (p) - printable characters"));
			}

			var gatherTableStatsFunctionOverload = specificFunctionOverloads
				.FirstOrDefault(o => IsParameterSupported(o, 0, "\"OWNNAME\"") && o.ProgramMetadata.Identifier.In(OracleProgramIdentifier.IdentifierGatherTableStats, OracleProgramIdentifier.IdentifierGatherSchemaStats));
			if (gatherTableStatsFunctionOverload != null && HasSingleStringLiteralParameterOrNoParameterToken(gatherTableStatsFunctionOverload))
			{
				var tableItems = databaseModel.Schemas.Select(s => BuildParameterCompletionItem(currentNode, s, s));
				completionItems.AddRange(tableItems);
			}

			gatherTableStatsFunctionOverload = specificFunctionOverloads
				.FirstOrDefault(o => IsParameterSupported(o, 1, "\"TABNAME\"") && o.ProgramMetadata.Identifier.In(OracleProgramIdentifier.IdentifierGatherTableStats));
			if (gatherTableStatsFunctionOverload != null && HasSingleStringLiteralParameterOrNoParameterToken(gatherTableStatsFunctionOverload))
			{
				var namedParameterExists = GetNamedParameterIndex(gatherTableStatsFunctionOverload, "\"OWNNAME\"", out int parameterIndex);
				if (!namedParameterExists)
				{
					parameterIndex = 0;
				}

				var ownerParameterReference =
					parameterIndex != -1 && gatherTableStatsFunctionOverload.ProgramReference.ParameterReferences.Count > parameterIndex
						? gatherTableStatsFunctionOverload.ProgramReference.ParameterReferences[parameterIndex]
						: null;

				var parameterValueNode = ownerParameterReference?.ValueNode;
				var schemaName =
					String.Equals(parameterValueNode?.FirstTerminalNode.Id, Terminals.StringLiteral) && ownerParameterReference.ValueNode?.TerminalCount == 1
						? ownerParameterReference.ValueNode?.FirstTerminalNode.Token.Value.ToPlainString()
						: databaseModel.CurrentSchema;

				schemaName = schemaName.ToQuotedIdentifier();

				var tableItems =
					databaseModel.AllObjects.Values
						.OfType<OracleTable>()
						.Where(o => String.Equals(o.Owner, schemaName))
						.Select(o =>
						{
							var name = o.Name.Trim('"');
							return BuildParameterCompletionItem(currentNode, name, name);
						});

				completionItems.AddRange(tableItems);
			}

			var dbmsCrptoHashFunctionOverload = specificFunctionOverloads
				.FirstOrDefault(o => IsParameterSupported(o, 1, "\"TYP\"") && o.ProgramMetadata.Identifier.In(OracleProgramIdentifier.IdentifierDbmsCryptoHash));
			if (dbmsCrptoHashFunctionOverload != null && HasSingleNumberLiteralParameterOrNoParameterToken(dbmsCrptoHashFunctionOverload))
			{
				completionItems.Add(BuildParameterCompletionItem(currentNode, "1", "1 - DBMS_CRYPTO.HASH_MD4 - MD4", false));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "2", "2 - DBMS_CRYPTO.HASH_MD5 - MD5", false));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "3", "3 - DBMS_CRYPTO.HASH_SH1 - SH1", false));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "4", "4 - DBMS_CRYPTO.HASH_SH256 - SH256", false));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "5", "5 - DBMS_CRYPTO.HASH_SH384 - SH384", false));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "6", "6 - DBMS_CRYPTO.HASH_SH512 - SH512", false));
			}

			var numberToYearToMonthIntervalFunctionOverload = specificFunctionOverloads
				.FirstOrDefault(o => o.CurrentParameterIndex == 1 && o.ProgramMetadata.Identifier.In(OracleProgramIdentifier.IdentifierBuiltInProgramNumberToYearToMonthInterval));
			if (numberToYearToMonthIntervalFunctionOverload != null && HasSingleStringLiteralParameterOrNoParameterToken(numberToYearToMonthIntervalFunctionOverload))
			{
				completionItems.Add(BuildParameterCompletionItem(currentNode, "MONTH", "MONTH"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "YEAR", "YEAR"));
			}

			var numberToDayToSecondIntervalFunctionOverload = specificFunctionOverloads
				.FirstOrDefault(o => o.CurrentParameterIndex == 1 && o.ProgramMetadata.Identifier.In(OracleProgramIdentifier.IdentifierBuiltInProgramNumberToDayToSecondInterval));
			if (numberToDayToSecondIntervalFunctionOverload != null && HasSingleStringLiteralParameterOrNoParameterToken(numberToDayToSecondIntervalFunctionOverload))
			{
				completionItems.Add(BuildParameterCompletionItem(currentNode, "SECOND", "SECOND"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "MINUTE", "MINUTE"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "HOUR", "HOUR"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "DAY", "DAY"));
			}

			var nextDayFunctionOverload = specificFunctionOverloads.FirstOrDefault(o => o.ProgramMetadata.Identifier == OracleProgramIdentifier.IdentifierBuiltInProgramNextDay);
			if (nextDayFunctionOverload != null && nextDayFunctionOverload.CurrentParameterIndex == 1)
			{
				var task = databaseModel.GetWeekdayNames(CancellationToken.None);
				var namespaceItems = task.Result.Select(n => BuildParameterCompletionItem(currentNode, n, n));
				completionItems.AddRange(namespaceItems);
			}

			var bFileNameFunction = specificFunctionOverloads.FirstOrDefault(o => o.ProgramMetadata.Identifier == OracleProgramIdentifier.IdentifierBuiltInProgramBFileName);
			if (bFileNameFunction != null && bFileNameFunction.CurrentParameterIndex == 0)
			{
				var directoryItems = databaseModel.AllObjects.Values.OfType<OracleDirectory>().Select(d => BuildDirectoryParameterCompletionItem(currentNode, d));
				completionItems.AddRange(directoryItems);
			}

			var sysContextFunctionOverload = specificFunctionOverloads.FirstOrDefault(o => o.ProgramMetadata.Identifier == OracleProgramIdentifier.IdentifierBuiltInProgramSysContext);
			if (sysContextFunctionOverload != null)
			{
				if (sysContextFunctionOverload.CurrentParameterIndex == 0 && sysContextFunctionOverload.ProgramMetadata.Parameters[sysContextFunctionOverload.CurrentParameterIndex + 1].DataType == "VARCHAR2" &&
				    HasSingleStringLiteralParameterOrNoParameterToken(sysContextFunctionOverload))
				{
					var task = databaseModel.GetContextData(CancellationToken.None);
					var namespaces = task.Result.Select(d => d.Key).Concat(Enumerable.Repeat(ContextNamespaceUserEnvironment, 1)).Distinct();
					var namespaceItems = namespaces.Select(n => BuildParameterCompletionItem(currentNode, n, n));
					completionItems.AddRange(namespaceItems);
				}
				else if (sysContextFunctionOverload.CurrentParameterIndex == 1 && sysContextFunctionOverload.ProgramMetadata.Parameters[sysContextFunctionOverload.CurrentParameterIndex + 1].DataType == "VARCHAR2")
				{
					var firstParameter = sysContextFunctionOverload.ProgramReference.ParameterReferences[0].ParameterNode[NonTerminals.Expression];
					var contextNamespace = HasSingleStringLiteralParameterOrNoParameterToken(sysContextFunctionOverload, 0) ? firstParameter.ChildNodes[0].Token.Value.ToUpperInvariant() : null;
					if (contextNamespace.ToPlainString() == ContextNamespaceUserEnvironment)
					{
						completionItems.Add(BuildParameterCompletionItem(currentNode, "ACTION", "ACTION - Identifies the position in the module (application name) and is set through the DBMS_APPLICATION_INFO package or OCI. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "AUDITED_CURSORID", "AUDITED_CURSORID - Returns the cursor ID of the SQL that triggered the audit. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "AUTHENTICATED_IDENTITY", "AUTHENTICATED_IDENTITY - Returns the identity used in authentication. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "AUTHENTICATION_DATA", "AUTHENTICATION_DATA - Data being used to authenticate the login user. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "AUTHENTICATION_METHOD", "AUTHENTICATION_METHOD - Returns the method of authentication. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "BG_JOB_ID", "BG_JOB_ID - Job ID of the current session if it was established by an Oracle Database background process. Null if the session was not established by a background process. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "CLIENT_IDENTIFIER", "CLIENT_IDENTIFIER - Returns an identifier that is set by the application through the DBMS_SESSION.SET_IDENTIFIER procedure. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "CLIENT_INFO", "CLIENT_INFO - Returns up to 64 bytes of user session information that can be stored by an application using the DBMS_APPLICATION_INFO package. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "CURRENT_BIND", "CURRENT_BIND - The bind variables for fine-grained auditing. You can specify this attribute only inside the event handler for the fine-grained auditing feature. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "CURRENT_EDITION_ID", "CURRENT_EDITION_ID - The identifier of the current edition. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "CURRENT_EDITION_NAME", "CURRENT_EDITION_NAME - The name of the current edition. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "CURRENT_SCHEMA", "CURRENT_SCHEMA - The name of the currently active default schema. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "CURRENT_SCHEMAID", "CURRENT_SCHEMAID - Identifier of the currently active default schema. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "CURRENT_SQL", "CURRENT_SQL - CURRENT_SQL returns the first 4K bytes of the current SQL that triggered the fine-grained auditing event. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "CURRENT_SQL<n>", "CURRENT_SQL<n> - The CURRENT_SQLn attributes return subsequent 4K-byte increments, where n can be an integer from 1 to 7, inclusive. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "CURRENT_SQL_LENGTH", "CURRENT_SQL_LENGTH - The length of the current SQL statement that triggers fine-grained audit or row-level security (RLS) policy functions or event handlers. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "CURRENT_USER", "CURRENT_USER - The name of the database user whose privileges are currently active. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "CURRENT_USERID", "CURRENT_USERID - The identifier of the database user whose privileges are currently active. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "DATABASE_ROLE", "DATABASE_ROLE - The database role using the SYS_CONTEXT function with the USERENV namespace. The role is one of the following: PRIMARY, PHYSICAL STANDBY, LOGICAL STANDBY, SNAPSHOT STANDBY. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "DB_DOMAIN", "DB_DOMAIN - Domain of the database as specified in the DB_DOMAIN initialization parameter. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "DB_NAME", "DB_NAME - Name of the database as specified in the DB_NAME initialization parameter. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "DB_UNIQUE_NAME", "DB_UNIQUE_NAME - Name of the database as specified in the DB_UNIQUE_NAME initialization parameter. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "DBLINK_INFO", "DBLINK_INFO - Returns the source of a database link session. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "ENTRYID", "DBLINK_INFO - The current audit entry number. The audit entryid sequence is shared between fine-grained audit records and regular audit records. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "ENTERPRISE_IDENTITY", "ENTERPRISE_IDENTITY - Returns the user's enterprise-wide identity. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "FG_JOB_ID", "FG_JOB_ID - Job ID of the current session if it was established by a client foreground process. NULL if the session was not established by a foreground process. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "GLOBAL_CONTEXT_MEMORY", "GLOBAL_CONTEXT_MEMORY - Returns the number being used in the System Global Area by the globally accessed context. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "GLOBAL_UID", "GLOBAL_UID - Returns the global user ID from Oracle Internet Directory for Enterprise User Security (EUS) logins; returns null for all other logins. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "HOST", "HOST - Name of the host machine from which the client has connected. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "IDENTIFICATION_TYPE", "IDENTIFICATION_TYPE - Returns the way the user's schema was created in the database. Specifically, it reflects the IDENTIFIED clause in the CREATE/ALTER USER syntax. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "INSTANCE", "INSTANCE - The instance identification number of the current instance. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "INSTANCE_NAME", "INSTANCE_NAME - The name of the instance. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "IP_ADDRESS", "IP_ADDRESS - IP address of the machine from which the client is connected. If the client and server are on the same machine and the connection uses IPv6 addressing, then ::1 is returned. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "ISDBA", "ISDBA - Returns TRUE if the user has been authenticated as having DBA privileges either through the operating system or through a password file. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "LANG", "LANG - The abbreviated name for the language, a shorter form than the existing 'LANGUAGE' parameter. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "LANGUAGE", "LANGUAGE - The language and territory currently used by your session, along with the database character set. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "MODULE", "MODULE - The application name (module) set through the DBMS_APPLICATION_INFO package or OCI. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "NETWORK_PROTOCOL", "NETWORK_PROTOCOL - Network protocol being used for communication, as specified in the 'PROTOCOL=protocol' portion of the connect string. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "NLS_CALENDAR", "NLS_CALENDAR - The current calendar of the current session. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "NLS_CURRENCY", "NLS_CURRENCY - The currency of the current session. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "NLS_DATE_FORMAT", "NLS_DATE_FORMAT - The date format for the session. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "NLS_DATE_LANGUAGE", "NLS_DATE_LANGUAGE - The language used for expressing dates. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "NLS_SORT", "NLS_SORT - BINARY or the linguistic sort basis. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "NLS_TERRITORY", "NLS_TERRITORY - The territory of the current session. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "OS_USER", "OS_USER - Operating system user name of the client process that initiated the database session. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "POLICY_INVOKER", "POLICY_INVOKER - The invoker of row-level security (RLS) policy functions. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "PROXY_ENTERPRISE_IDENTITY", "PROXY_ENTERPRISE_IDENTITY - Returns the Oracle Internet Directory DN when the proxy user is an enterprise user. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "PROXY_USER", "PROXY_USER - Name of the database user who opened the current session on behalf of SESSION_USER. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "PROXY_USERID", "PROXY_USERID - Identifier of the database user who opened the current session on behalf of SESSION_USER. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "SERVER_HOST", "SERVER_HOST - The host name of the machine on which the instance is running. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "SERVICE_NAME", "SERVICE_NAME - The name of the service to which a given session is connected. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "SESSION_EDITION_ID", "SESSION_EDITION_ID - The identifier of the session edition. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "SESSION_EDITION_NAME", "SESSION_EDITION_NAME - The name of the session edition. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "SESSION_USER", "SESSION_USER - The name of the database user at logon. For enterprise users, returns the schema. For other users, returns the database user name. This value remains the same throughout the duration of the session. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "SESSION_USERID", "SESSION_USERID - The identifier of the database user at logon. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "SESSIONID", "SESSIONID - The auditing session identifier. You cannot use this attribute in distributed SQL statements. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "SID", "SID - The session ID. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "STATEMENTID", "STATEMENTID - The auditing statement identifier. STATEMENTID represents the number of SQL statements audited in a given session. "));
						completionItems.Add(BuildParameterCompletionItem(currentNode, "TERMINAL", "TERMINAL - The operating system identifier for the client of the current session. "));
					}
					else if (!String.IsNullOrEmpty(contextNamespace))
					{
						var task = databaseModel.GetContextData(CancellationToken.None);
						task.Wait();
						var attributes = task.Result[contextNamespace.ToPlainString()];
						var attributeItems = attributes.Select(a => BuildParameterCompletionItem(currentNode, a, a));
						completionItems.AddRange(attributeItems);
					}
				}
			}

			if (IsAtRegexModifierParameter(specificFunctionOverloads))
			{
				completionItems.Add(BuildParameterCompletionItem(currentNode, "i", "i - specifies case-insensitive matching"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "c", "c - specifies case-sensitive matching"));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "n", "n - allows the period (.), which is the match-any-character character, to match the newline character. If you omit this parameter, then the period does not match the newline character. "));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "m", "m - treats the source string as multiple lines. Oracle interprets the caret (^) and dollar sign ($) as the start and end, respectively, of any line anywhere in the source string, rather than only at the start or end of the entire source string. If you omit this parameter, then Oracle treats the source string as a single line. "));
				completionItems.Add(BuildParameterCompletionItem(currentNode, "x", "x - ignores whitespace characters. By default, whitespace characters match themselves. "));
			}
			else if (IsAtDbmsMetadataObjectTypeParameter(specificFunctionOverloads))
			{
				var parameters =
					new[]
					{
						"AQ_QUEUE",
						"AQ_QUEUE_TABLE",
						"AQ_TRANSFORM",
						"ASSOCIATION",
						"AUDIT",
						"AUDIT_OBJ",
						"CLUSTER",
						"COMMENT",
						"CONSTRAINT",
						"CONTEXT",
						"DATABASE_EXPORT",
						"DB_LINK",
						"DEFAULT_ROLE",
						"DIMENSION",
						"DIRECTORY",
						"FGA_POLICY",
						"FUNCTION",
						"INDEX_STATISTICS",
						"INDEX",
						"INDEXTYPE",
						"JAVA_SOURCE",
						"JOB",
						"LIBRARY",
						"MATERIALIZED_VIEW",
						"MATERIALIZED_VIEW_LOG",
						"OBJECT_GRANT",
						"OPERATOR",
						"PACKAGE",
						"PACKAGE_SPEC",
						"PACKAGE_BODY",
						"PROCEDURE",
						"PROFILE",
						"PROXY",
						"REF_CONSTRAINT",
						"REFRESH_GROUP",
						"RESOURCE_COST",
						"RLS_CONTEXT",
						"RLS_GROUP",
						"RLS_POLICY",
						"RMGR_CONSUMER_GROUP",
						"RMGR_INTITIAL_CONSUMER_GROUP",
						"RMGR_PLAN",
						"RMGR_PLAN_DIRECTIVE",
						"ROLE",
						"ROLE_GRANT",
						"ROLLBACK_SEGMENT",
						"SCHEMA_EXPORT",
						"SEQUENCE",
						"SYNONYM",
						"SYSTEM_GRANT",
						"TABLE",
						"TABLE_DATA",
						"TABLE_EXPORT",
						"TABLE_STATISTICS",
						"TABLESPACE",
						"TABLESPACE_QUOTA",
						"TRANSPORTABLE_EXPORT",
						"TRIGGER",
						"TRUSTED_DB_LINK",
						"TYPE",
						"TYPE_SPEC",
						"TYPE_BODY",
						"USER",
						"VIEW",
						"XMLSCHEMA",
						"XS_USER",
						"XS_ROLE",
						"XS_ROLESET",
						"XS_ROLE_GRANT",
						"XS_SECURITY_CLASS",
						"XS_DATA_SECURITY",
						"XS_ACL",
						"XS_ACL_PARAM"
					};

				foreach (var parameter in parameters)
				{
					completionItems.Add(BuildParameterCompletionItem(currentNode, parameter, parameter));
				}
			}

			var convertFunctionOverload = specificFunctionOverloads
					.FirstOrDefault(o => (o.CurrentParameterIndex == 1 || o.CurrentParameterIndex == 2) && o.ProgramMetadata.Identifier == OracleProgramIdentifier.IdentifierBuiltInProgramConvert);
			if (convertFunctionOverload != null && HasSingleStringLiteralParameterOrNoParameterToken(convertFunctionOverload))
			{
				completionItems.AddRange(databaseModel.CharacterSets.Select(cs => BuildParameterCompletionItem(currentNode, cs, cs)));
			}

			return completionItems;
		}

		private static bool IsParameterSupported(OracleCodeCompletionFunctionOverload programOverload, int parameterIndex, string parameterName)
		{
			var namedParameterExists = GetNamedParameterIndex(programOverload, parameterName, out int namedParameterIndex);
			if (programOverload.CurrentParameterIndex == namedParameterIndex)
			{
				return true;
			}

			return programOverload.CurrentParameterIndex == parameterIndex && !namedParameterExists;
		}

		private static bool GetNamedParameterIndex(OracleCodeCompletionFunctionOverload programOverload, string parameterName, out int parameterIndex)
		{
			var namedParameterExists = false;
			for (var i = 0; i < programOverload.ProgramReference.ParameterReferences.Count; i++)
			{
				var parameterReference = programOverload.ProgramReference.ParameterReferences[i];
				if (parameterReference.OptionalIdentifierTerminal == null)
				{
					continue;
				}

				namedParameterExists = true;

				if (String.Equals(parameterName, parameterReference.OptionalIdentifierTerminal.Token.Value.ToQuotedIdentifier()))
				{
					parameterIndex = i;
					return true;
				}
			}

			parameterIndex = -1;
			return namedParameterExists;
		}

		private static OracleCodeCompletionItem BuildDirectoryParameterCompletionItem(StatementGrammarNode currentNode, OracleDirectory directory)
		{
			var directoryNameLabel = $"{directory.Owner.Trim('"')}.{directory.Name.Trim('"')}";
			return BuildParameterCompletionItem(currentNode, directoryNameLabel, directoryNameLabel, description: directory.Path);
		}

		private static bool IsAtDbmsMetadataObjectTypeParameter(IEnumerable<OracleCodeCompletionFunctionOverload> functionOverloads)
		{
			return functionOverloads.Any(o => IsDbmsMetadata(o.ProgramMetadata) && o.ProgramMetadata.Parameters.Count >= o.CurrentParameterIndex + 1 && String.Equals(o.ProgramMetadata.Parameters[o.CurrentParameterIndex + 1].Name, "\"OBJECT_TYPE\""));
		}

		private static bool IsAtRegexModifierParameter(IEnumerable<OracleCodeCompletionFunctionOverload> functionOverloads)
		{
			return functionOverloads
				.Any(o =>
					(o.ProgramMetadata.Identifier == OracleProgramIdentifier.IdentifierBuiltInProgramRegularExpressionCount && o.CurrentParameterIndex == 3) ||
					(o.ProgramMetadata.Identifier == OracleProgramIdentifier.IdentifierBuiltInProgramRegularExpressionInstr && o.CurrentParameterIndex == 5) ||
					(o.ProgramMetadata.Identifier == OracleProgramIdentifier.IdentifierBuiltInProgramRegularExpressionReplace && o.CurrentParameterIndex == 5) ||
					(o.ProgramMetadata.Identifier == OracleProgramIdentifier.IdentifierBuiltInProgramRegularExpressionSubstring && o.CurrentParameterIndex == 4));
		}

		private static void BuildCommonDateFormatCompletionItems(StatementGrammarNode currentNode, ICollection<ICodeCompletionItem> completionItems)
		{
			completionItems.Add(BuildParameterCompletionItem(currentNode, "DL", "DL - long date format - NLS dependent"));
			completionItems.Add(BuildParameterCompletionItem(currentNode, "DS", "DS - short date format - NLS dependent"));
			completionItems.Add(BuildParameterCompletionItem(currentNode, "TS", "TS - short time format - NLS dependent"));
			completionItems.Add(BuildParameterCompletionItem(currentNode, "YYYY-MM-DD", $"YYYY-MM-DD - ISO date - {DateTime.Now.ToString("yyyy-MM-dd")}"));
			completionItems.Add(BuildParameterCompletionItem(currentNode, "YYYY-MM-DD HH24:MI:SS", $"YYYY-MM-DD HH24:MI:SS - ISO date time - {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}"));
			completionItems.Add(BuildParameterCompletionItem(currentNode, "YYYY-MM-DD\"T\"HH24:MI:SS", $"YYYY-MM-DD\"T\"HH24:MI:SS - XML date time - {DateTime.Now.ToString("yyyy-MM-dd\"T\"HH:mm:ss")}"));
			completionItems.Add(BuildParameterCompletionItem(currentNode, "YYYY-MM-DD HH24:MI:SS.FF9 TZH:TZM", $"YYYY-MM-DD HH24:MI:SS.FF9 TZH:TZM - {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffffff000 zzz")}"));
			completionItems.Add(BuildParameterCompletionItem(currentNode, "DY, DD MON YYYY HH24:MI:SS TZD", $"DY, DD MON YYYY HH24:MI:SS TZD - {DateTime.Now.ToString("r")} - NLS dependent"));
			completionItems.Add(BuildParameterCompletionItem(currentNode, "DAY", "DAY (NAME OF DAY); Day (Name of day), padded with blanks to display width of the widest name of day in the date language used for this element"));
			completionItems.Add(BuildParameterCompletionItem(currentNode, "D", "D - Day of week (1-7)"));
			completionItems.Add(BuildParameterCompletionItem(currentNode, "DD", "DD - Day of month (1-31)"));
			completionItems.Add(BuildParameterCompletionItem(currentNode, "DDD", "DDD - Day of year (1-366)"));
			completionItems.Add(BuildParameterCompletionItem(currentNode, "MON", "MON - Abbreviated name of month"));
			completionItems.Add(BuildParameterCompletionItem(currentNode, "MONTH", "MONTH - Name of month, padded with blanks to display width of the widest name of month in the date language used for this element"));
			completionItems.Add(BuildParameterCompletionItem(currentNode, "SSSSS", "SSSSS - Seconds past midnight (0-86399)"));
			completionItems.Add(BuildParameterCompletionItem(currentNode, "RM", "RM - Roman numeral month (I-XII; January = I)"));
			completionItems.Add(BuildParameterCompletionItem(currentNode, "TZD", "TZD - Daylight savings information; the TZD value is an abbreviated time zone string with daylight savings information. It must correspond with the region specified in TZR. "));
			completionItems.Add(BuildParameterCompletionItem(currentNode, "TZH", "TZH - Time zone hour"));
			completionItems.Add(BuildParameterCompletionItem(currentNode, "TZM", "TZM - Time zone minute"));
			completionItems.Add(BuildParameterCompletionItem(currentNode, "TZR", "TZR - Time zone region information; the value must be one of the time zone regions supported in the database. "));
			completionItems.Add(BuildParameterCompletionItem(currentNode, "Y", "Y - Last digit of year"));
			completionItems.Add(BuildParameterCompletionItem(currentNode, "YY", "YY - Last two digits of year"));
			completionItems.Add(BuildParameterCompletionItem(currentNode, "YYY", "YYY - Last three digits of year"));
		}

		private static bool HasSingleStringLiteralParameterOrNoParameterToken(OracleCodeCompletionFunctionOverload functionOverload, int? parameterIndex = null)
		{
			return HasSingleLiteralParameterOrNoParameterToken(functionOverload, Terminals.StringLiteral, parameterIndex);
		}

		private static bool HasSingleNumberLiteralParameterOrNoParameterToken(OracleCodeCompletionFunctionOverload functionOverload, int? parameterIndex = null)
		{
			return HasSingleLiteralParameterOrNoParameterToken(functionOverload, Terminals.NumberLiteral, parameterIndex);
		}

		private static bool HasSingleLiteralParameterOrNoParameterToken(OracleCodeCompletionFunctionOverload functionOverload, string literalTerminalId, int? parameterIndex)
		{
			parameterIndex = parameterIndex ?? functionOverload.CurrentParameterIndex;
			if (parameterIndex >= functionOverload.ProgramReference.ParameterReferences.Count)
			{
				return functionOverload.ProgramReference.ParameterReferences.Count == parameterIndex;
			}

			var firstParameter = functionOverload.ProgramReference.ParameterReferences[parameterIndex.Value];
			var parameterExpression = firstParameter.ParameterNode[NonTerminals.Expression];
			return parameterExpression != null && parameterExpression.ChildNodes.Count == 1 && parameterExpression.ChildNodes[0].Id == literalTerminalId;
		}

		private static bool IsDbmsMetadata(OracleProgramMetadata metadata)
		{
			return metadata.Owner.GetTargetSchemaObject()?.FullyQualifiedName == OracleObjectIdentifier.IdentifierDbmsMetadata;
		}

		private static OracleCodeCompletionItem BuildParameterCompletionItem(StatementGrammarNode node, string parameterValue, string label, bool addApostrophes = true, string description = null)
		{
			var value = parameterValue.ToOracleString();
			var text = addApostrophes ? $"'{value}'" : value;

			return
				new OracleCodeCompletionItem
				{
					Category = OracleCodeCompletionCategory.FunctionParameter,
					Label = label,
					StatementNode = String.Equals(node.Id, Terminals.StringLiteral) ? node : null,
					Text = text,
					Description = description
				};
		}

		public static bool IsMatch(string fullyQualifiedName, string inputPhrase)
		{
			if (String.IsNullOrEmpty(fullyQualifiedName))
			{
				return false;
			}

			inputPhrase = inputPhrase?.Trim('"');
			return String.IsNullOrWhiteSpace(inputPhrase) ||
			       ResolveSearchPhrases(inputPhrase).All(p => fullyQualifiedName.ToUpperInvariant().Contains(p));
		}

		private static IEnumerable<string> ResolveSearchPhrases(string inputPhrase)
		{
			var builder = new StringBuilder();
			var containsSmallLetter = false;
			foreach (var character in inputPhrase)
			{
				if (containsSmallLetter && Char.IsUpper(character) && builder.Length > 0)
				{
					yield return builder.ToString().ToUpperInvariant();
					containsSmallLetter = false;
					builder.Clear();
				}
				else if (Char.IsLower(character))
				{
					containsSmallLetter = true;
				}

				builder.Append(character);
			}

			if (builder.Length > 0)
			{
				yield return builder.ToString().ToUpperInvariant();
			}
		}
	}
}
