using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Linq;
using System.Windows;

namespace SqlPad
{
	public class PageModel : ModelBase
	{
		private readonly DocumentPage _documentPage;
		private readonly ObservableCollection<string> _schemas = new ObservableCollection<string>();
		private ConnectionStringSettings _currentConnection;
		private string _currentSchema;
		private string _schemaLabel;
		private IReadOnlyList<BindVariableModel> _bindVariables;
		private bool _isModified;
		private bool _isRunning;
		private OutputViewerModel _activeOutputModel;

		private Visibility _productionLabelVisibility = Visibility.Collapsed;
		private Visibility _bindVariableListVisibility = Visibility.Collapsed;
		private Visibility _reconnectOptionVisibility = Visibility.Collapsed;
		private Visibility _schemaComboBoxVisibility = Visibility.Collapsed;
		private Visibility _connectProgressBarVisibility = Visibility.Visible;
		
		private string _dateTimeFormat;
		private string _connectionErrorMessage;
		private string _documentHeaderToolTip;

		public PageModel(DocumentPage documentPage)
		{
			_documentPage = documentPage;
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

		public string SchemaLabel
		{
			get { return _schemaLabel; }
			set { UpdateValueAndRaisePropertyChanged(ref _schemaLabel, value); }
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

		public string ConnectionErrorMessage
		{
			get { return _connectionErrorMessage; }
			set { UpdateValueAndRaisePropertyChanged(ref _connectionErrorMessage, value); }
		}

		public IReadOnlyList<BindVariableModel> BindVariables
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

		public ObservableCollection<string> Schemas { get { return _schemas; } }

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

		public OutputViewerModel ActiveOutputModel
		{
			get { return _activeOutputModel; }
			set { UpdateValueAndRaisePropertyChanged(ref _activeOutputModel, value); }
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
