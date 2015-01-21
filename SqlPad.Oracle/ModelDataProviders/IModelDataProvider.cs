using System;
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
		
		void MapReaderData(OracleDataReader reader);

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

		public virtual void MapReaderData(OracleDataReader reader)
		{
			throw new NotSupportedException("Override this method if you want to map data from the reader. ");
		}

		public virtual bool HasScalarResult { get { return false; } }

		public virtual bool IsValid { get { return true; } }

		public virtual void MapScalarValue(object value)
		{
			throw new NotSupportedException("Override this method to get scalar value from a query. ");
		}
	}
}
