using System;
using System.Threading;
using System.Threading.Tasks;
#if ORACLE_MANAGED_DATA_ACCESS_CLIENT
using Oracle.ManagedDataAccess.Client;
#else
using Oracle.DataAccess.Client;
#endif

namespace SqlPad.Oracle.ModelDataProviders
{
	internal interface IModelDataProvider
	{
		void InitializeCommand(OracleCommand command);
		
		Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken);

		void MapScalarValue(object value);

		bool HasScalarResult { get; }
		
		bool IsValid { get; }
	}

	internal abstract class ModelDataProvider<TModel> : IModelDataProvider
	{
		protected TModel DataModel { get; private set; }

		protected ModelDataProvider(TModel dataModel)
		{
			DataModel = dataModel;
		}

		public abstract void InitializeCommand(OracleCommand command);

		public virtual Task MapReaderData(OracleDataReader reader, CancellationToken cancellationToken)
		{
			throw new NotSupportedException("Override this method if you want to map data from the reader. ");
		}

		public virtual bool HasScalarResult => false;

	    public virtual bool IsValid => true;

	    public virtual void MapScalarValue(object value)
		{
			throw new NotSupportedException("Override this method to get scalar value from a query. ");
		}
	}
}
