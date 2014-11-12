using System.Threading;
using System.Threading.Tasks;

namespace SqlPad
{
	public interface ISqlParser
	{
		StatementCollection Parse(string sqlText);

		Task<StatementCollection> ParseAsync(string sqlText, CancellationToken cancellationToken);

		bool IsReservedWord(string value);

		bool IsLiteral(string terminalId);

		bool IsAlias(string terminalId);
	}
}