using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;

namespace SqlPad
{
	public class PageModel : ModelBase
	{
		private readonly DocumentPage _documentPage;
		private readonly ObservableCollection<object[]> _resultRowItems = new ObservableCollection<object[]>();
		private readonly ObservableCollection<string> _schemas = new ObservableCollection<string>();
		private readonly ObservableCollection<CompilationError> _compilationErrors = new ObservableCollection<CompilationError>();
		private readonly SessionExecutionStatisticsCollection _sessionExecutionStatistics = new SessionExecutionStatisticsCollection();
		private readonly StringBuilder _databaseOutputBuilder = new StringBuilder();
		private ConnectionStringSettings _currentConnection;
		private string _currentSchema;
		private ICollection<BindVariableModel> _bindVariables;
		private bool _isTransactionControlEnabled = true;
		private bool _isModified;
		private bool _isRunning;
		private int _affectedRowCount = -1;
		private Visibility _statementExecutedSuccessfullyStatusMessageVisibility = Visibility.Collapsed;
		private Visibility _productionLabelVisibility = Visibility.Collapsed;
		private Visibility _bindVariableListVisibility = Visibility.Collapsed;
		private Visibility _gridRowInfoVisibity = Visibility.Collapsed;
		private Visibility _transactionControlVisibity = Visibility.Collapsed;
		private Visibility _executionPlanAvailable = Visibility.Collapsed;
		private Visibility _reconnectOptionVisibility = Visibility.Collapsed;
		private Visibility _schemaComboBoxVisibility = Visibility.Collapsed;
		private Visibility _connectProgressBarVisibility = Visibility.Visible;
		private string _dateTimeFormat;
		private string _connectionErrorMessage;
		private string _documentHeaderToolTip;
		private bool _showAllSessionExecutionStatistics;

		public PageModel(DocumentPage documentPage)
		{
			_documentPage = documentPage;
			_sessionExecutionStatistics.CollectionChanged += (sender, args) => RaisePropertyChanged("ExecutionStatisticsAvailable");
			_compilationErrors.CollectionChanged += (sender, args) => RaisePropertyChanged("CompilationErrorsVisible");
			SetUpSessionExecutionStatisticsFilter();
			SetUpSessionExecutionStatisticsSorting();
		}

		private void SetUpSessionExecutionStatisticsSorting()
		{
			var view = CollectionViewSource.GetDefaultView(_sessionExecutionStatistics);
			view.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
		}

		public Visibility StatementExecutionInfoSeparatorVisibility
		{
			get { return _gridRowInfoVisibity == Visibility.Collapsed && (AffectedRowCountVisibility == Visibility.Collapsed && StatementExecutedSuccessfullyStatusMessageVisibility == Visibility.Collapsed) ? Visibility.Collapsed : Visibility.Visible; }
		}

		public Visibility GridRowInfoVisibility
		{
			get { return _gridRowInfoVisibity; }
			set
			{
				if (UpdateValueAndRaisePropertyChanged(ref _gridRowInfoVisibity, value))
				{
					RaisePropertyChanged("StatementExecutionInfoSeparatorVisibility");
				}
			}
		}

		public Visibility TransactionControlVisibity
		{
			get { return _transactionControlVisibity; }
			set { UpdateValueAndRaisePropertyChanged(ref _transactionControlVisibity, value); }
		}

		public bool IsTransactionControlEnabled
		{
			get { return _isTransactionControlEnabled; }
			set { UpdateValueAndRaisePropertyChanged(ref _isTransactionControlEnabled, value); }
		}
		
		public bool IsModified
		{
			get { return _isModified; }
			set { UpdateValueAndRaisePropertyChanged(ref _isModified, value); }
		}

		public bool IsRunning
		{
			get { return _isRunning; }
			set { UpdateValueAndRaisePropertyChanged(ref _isRunning, value); }
		}

		public string DocumentHeaderToolTip
		{
			get { return _documentHeaderToolTip; }
			set { UpdateValueAndRaisePropertyChanged(ref _documentHeaderToolTip, value); }
		}

		public string DocumentHeader
		{
			get { return _documentPage.WorkDocument.DocumentTitle; }
			set
			{
				if (_documentPage.WorkDocument.DocumentTitle == value)
				{
					return;
				}

				_documentPage.WorkDocument.DocumentTitle = value;
				SetDocumentModifiedIfSqlx();
				
				RaisePropertyChanged("DocumentHeader");
			}
		}

		private void SetDocumentModifiedIfSqlx()
		{
			if (!_documentPage.WorkDocument.IsSqlx || _documentPage.WorkDocument.IsModified)
			{
				return;
			}

			_documentPage.WorkDocument.IsModified = IsModified = true;
			RaisePropertyChanged("IsModified");
		}

		public string DateTimeFormat
		{
			get { return _dateTimeFormat; }
			set { UpdateValueAndRaisePropertyChanged(ref _dateTimeFormat, value); }
		}

		public string HeaderBackgroundColorCode
		{
			get { return _documentPage.WorkDocument.HeaderBackgroundColorCode; }
			set
			{
				if (_documentPage.WorkDocument.HeaderBackgroundColorCode == value)
				{
					return;
				}

				_documentPage.WorkDocument.HeaderBackgroundColorCode = value;
				SetDocumentModifiedIfSqlx();
				
				RaisePropertyChanged("HeaderBackgroundColorCode");
			}
		}

		public Visibility ExecutionStatisticsAvailable
		{
			get { return _sessionExecutionStatistics.Count > 0 ? Visibility.Visible : Visibility.Collapsed; }
		}

		public Visibility CompilationErrorsVisible
		{
			get { return _compilationErrors.Count > 0 ? Visibility.Visible : Visibility.Collapsed; }
		}

		public bool ShowAllSessionExecutionStatistics
		{
			get { return _showAllSessionExecutionStatistics; }
			set
			{
				if (UpdateValueAndRaisePropertyChanged(ref _showAllSessionExecutionStatistics, value))
				{
					SetUpSessionExecutionStatisticsFilter();
				}
			}
		}

		private void SetUpSessionExecutionStatisticsFilter()
		{
			var view = CollectionViewSource.GetDefaultView(_sessionExecutionStatistics);
			view.Filter = _showAllSessionExecutionStatistics
				? (Predicate<object>)null
				: ShowActiveSessionExecutionStatisticsFilter;
		}

		public bool ShowActiveSessionExecutionStatisticsFilter(object record)
		{
			var statisticsRecord = (SessionExecutionStatisticsRecord)record;
			return statisticsRecord.Value != 0;
		}

		public bool EnableDatabaseOutput
		{
			get { return _documentPage.DatabaseModel.EnableDatabaseOutput; }
			set { _documentPage.DatabaseModel.EnableDatabaseOutput = value; }
		}

		public bool KeepDatabaseOutputHistory { get; set; }

		public void WriteDatabaseOutput(string output)
		{
			if (!KeepDatabaseOutputHistory)
			{
				_databaseOutputBuilder.Clear();
			}

			if (!String.IsNullOrEmpty(output))
			{
				_databaseOutputBuilder.AppendLine(output);
			}
			
			RaisePropertyChanged("DatabaseOutput");
		}

		public string DatabaseOutput { get { return _databaseOutputBuilder.ToString(); } }

		public int AffectedRowCount
		{
			get { return _affectedRowCount; }
			set
			{
				if (!UpdateValueAndRaisePropertyChanged(ref _affectedRowCount, value))
					return;
				
				RaisePropertyChanged("AffectedRowCountVisibility");
				RaisePropertyChanged("StatementExecutionInfoSeparatorVisibility");
			}
		}

		public Visibility AffectedRowCountVisibility
		{
			get { return _affectedRowCount == -1 ? Visibility.Collapsed : Visibility.Visible; }
		}

		public Visibility StatementExecutedSuccessfullyStatusMessageVisibility
		{
			get { return _statementExecutedSuccessfullyStatusMessageVisibility; }
			set
			{
				if (UpdateValueAndRaisePropertyChanged(ref _statementExecutedSuccessfullyStatusMessageVisibility, value))
				{
					RaisePropertyChanged("StatementExecutionInfoSeparatorVisibility");
				}
			}
		}

		public Visibility ProductionLabelVisibility
		{
			get { return _productionLabelVisibility; }
			set { UpdateValueAndRaisePropertyChanged(ref _productionLabelVisibility, value); }
		}

		public Visibility BindVariableListVisibility
		{
			get { return _bindVariableListVisibility; }
			private set { UpdateValueAndRaisePropertyChanged(ref _bindVariableListVisibility, value); }
		}

		public Visibility ConnectProgressBarVisibility
		{
			get { return _connectProgressBarVisibility; }
			set { UpdateValueAndRaisePropertyChanged(ref _connectProgressBarVisibility, value); }
		}

		public Visibility ReconnectOptionVisibility
		{
			get { return _reconnectOptionVisibility; }
			set { UpdateValueAndRaisePropertyChanged(ref _reconnectOptionVisibility, value); }
		}

		public Visibility ExecutionPlanAvailable
		{
			get { return _executionPlanAvailable; }
			set { UpdateValueAndRaisePropertyChanged(ref _executionPlanAvailable, value); }
		}

		public string ConnectionErrorMessage
		{
			get { return _connectionErrorMessage; }
			set { UpdateValueAndRaisePropertyChanged(ref _connectionErrorMessage, value); }
		}

		public ICollection<BindVariableModel> BindVariables
		{
			get { return _bindVariables; }
			set
			{
				UpdateValueAndRaisePropertyChanged(ref _bindVariables, value);

				var showBindVariableList = _bindVariables != null && _bindVariables.Count > 0;
				if (!showBindVariableList && BindVariableListVisibility == Visibility.Visible)
				{
					BindVariableListVisibility = Visibility.Collapsed;
				}
				else if (showBindVariableList && BindVariableListVisibility == Visibility.Collapsed)
				{
					BindVariableListVisibility = Visibility.Visible;
				}
			}
		}

		public ObservableCollection<object[]> ResultRowItems { get { return _resultRowItems; } }

		public ObservableCollection<string> Schemas { get { return _schemas; } }

		public ObservableCollection<CompilationError> CompilationErrors { get { return _compilationErrors; } }

		public SessionExecutionStatisticsCollection SessionExecutionStatistics { get { return _sessionExecutionStatistics; } }

		public string CurrentSchema
		{
			get { return _currentSchema; }
			set
			{
				if (value == null)
					return;

				if (!UpdateValueAndRaisePropertyChanged(ref _currentSchema, value))
					return;

				_documentPage.DatabaseModel.CurrentSchema = value;
				_documentPage.ReParse();
			}
		}

		public ConnectionStringSettings CurrentConnection
		{
			get { return _currentConnection; }
			set
			{
				ReconnectOptionVisibility = Visibility.Collapsed;
				ConnectProgressBarVisibility = Visibility.Visible;

				_documentPage.InitializeInfrastructureComponents(value);
				_currentConnection = value;
			}
		}

		public Visibility SchemaComboBoxVisibility
		{
			get { return _schemaComboBoxVisibility; }
			set { UpdateValueAndRaisePropertyChanged(ref _schemaComboBoxVisibility, value); }
		}

		public void ResetSchemas()
		{
			_schemas.Clear();
			_currentSchema = null;
			SchemaComboBoxVisibility = Visibility.Collapsed;
		}

		public void SetSchemas(IEnumerable<string> schemas)
		{
			ResetSchemas();
			_schemas.AddRange(schemas.OrderBy(s => s));

			if (_schemas.Count > 0)
			{
				SchemaComboBoxVisibility = Visibility.Visible;
			}
		}
	}
}
