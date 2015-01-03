using System.Threading;
using System.Threading.Tasks;

namespace SqlPad
{
	public interface ISqlParser
	{
		StatementCollection Parse(string sqlText);

		Task<StatementCollection> ParseAsync(string sqlText, CancellationToken cancellationToken);

		bool IsLiteral(string terminalId);

		bool IsAlias(string terminalId);

		bool CanAddPairCharacter(string tokenValue, char character);
	}
}