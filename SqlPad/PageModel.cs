using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace SqlPad
{
	public class PageModel : ModelBase
	{
		private readonly DocumentPage _documentPage;
		private readonly ObservableCollection<object[]> _resultRowItems = new ObservableCollection<object[]>();
		private readonly ObservableCollection<string> _schemas = new ObservableCollection<string>();
		private readonly ObservableCollection<SessionExecutionStatisticsRecord> _sessionExecutionStatistics = new ObservableCollection<SessionExecutionStatisticsRecord>();
		private string _documentHeader;
		private int _currentLine;
		private int _currentColumn;
		private int _affectedRowCount = -1;
		private int? _selectionLength;
		private Visibility _selectionTextVisibility = Visibility.Collapsed;
		private Visibility _productionLabelVisibility = Visibility.Collapsed;
		private ConnectionStringSettings _currentConnection;
		private string _currentSchema;
		private ICollection<BindVariableModel> _bindVariables;
		private Visibility _bindVariableListVisibility = Visibility.Collapsed;
		private Visibility _gridRowInfoVisibity = Visibility.Collapsed;
		private string _textExecutionPlan;
		private string _dateTimeFormat;
		private bool _showAllSessionExecutionStatistics;

		public PageModel(DocumentPage documentPage)
		{
			_documentPage = documentPage;
			_sessionExecutionStatistics.CollectionChanged += (sender, args) => RaisePropertyChanged("ExecutionStatisticsAvailable");
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
			get { return _gridRowInfoVisibity == Visibility.Collapsed && AffectedRowCountVisibility == Visibility.Collapsed ? Visibility.Collapsed : Visibility.Visible; }
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

		public string DocumentHeader
		{
			get { return _documentHeader; }
			set { UpdateValueAndRaisePropertyChanged(ref _documentHeader, value); }
		}

		public string DateTimeFormat
		{
			get { return _dateTimeFormat; }
			set { UpdateValueAndRaisePropertyChanged(ref _dateTimeFormat, value); }
		}

		public string TextExecutionPlan
		{
			get { return _textExecutionPlan; }
			set
			{
				if (!UpdateValueAndRaisePropertyChanged(ref _textExecutionPlan, value))
				{
					return;
				}
				
				RaisePropertyChanged("IsExecutionPlanAvailable");
			}
		}

		public Visibility IsExecutionPlanAvailable
		{
			get { return String.IsNullOrEmpty(_textExecutionPlan) ? Visibility.Collapsed : Visibility.Visible; }
		}

		public Visibility ExecutionStatisticsAvailable
		{
			get { return _sessionExecutionStatistics.Count > 0 ? Visibility.Visible : Visibility.Collapsed; }
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

		public int CurrentLine
		{
			get { return _currentLine; }
			set { UpdateValueAndRaisePropertyChanged(ref _currentLine, value); }
		}

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

		public int CurrentColumn
		{
			get { return _currentColumn; }
			set { UpdateValueAndRaisePropertyChanged(ref _currentColumn, value); }
		}

		public int? SelectionLength
		{
			get { return _selectionLength; }
			set
			{
				if (UpdateValueAndRaisePropertyChanged(ref _selectionLength, value))
				{
					SelectionTextVisibility = _selectionLength == null ? Visibility.Collapsed : Visibility.Visible;
				}
			}
		}

		public Visibility SelectionTextVisibility
		{
			get { return _selectionTextVisibility; }
			set { UpdateValueAndRaisePropertyChanged(ref _selectionTextVisibility, value); }
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

		public ObservableCollection<SessionExecutionStatisticsRecord> SessionExecutionStatistics { get { return _sessionExecutionStatistics; } }

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
				try
				{
					_documentPage.InitializeInfrastructureComponents(value);
				}
				catch(Exception e)
				{
					Messages.ShowError(e.Message);

					if (_currentConnection == null)
						return;

					var originalValue = _currentConnection;
					_currentConnection = value;
					RevertConnection(originalValue);
					_documentPage.ReParse();

					return;
				}

				_currentConnection = value;

				SetSchemas();

				CurrentSchema = _documentPage.DatabaseModel.CurrentSchema;
			}
		}

		private void RevertConnection(ConnectionStringSettings originalValue)
		{
			try
			{
				_documentPage.InitializeInfrastructureComponents(originalValue);
				_documentPage.DatabaseModel.CurrentSchema = _currentSchema;

				Application.Current.Dispatcher.BeginInvoke(new Action(() =>
				{
					_currentConnection = originalValue;
					RaisePropertyChanged("CurrentConnection");
				}));
			}
			catch (Exception e)
			{
				Trace.WriteLine("CurrentConnection setter failed: " + e);
			}
		}

		private void SetSchemas()
		{
			var schemas = _documentPage.DatabaseModel.Schemas.OrderBy(s => s);
			_schemas.Clear();
			_schemas.AddRange(schemas);
			_currentSchema = null;
		}
	}

	public class StatementExecutionModel
	{
		public string StatementText { get; set; }
		
		public ICollection<BindVariableModel> BindVariables { get; set; }
		
		public bool GatherExecutionStatistics { get; set; }
	}

	public struct StatementExecutionResult
	{
		public int AffectedRowCount { get; set; }

		public bool ExecutedSucessfully { get; set; }
	}

	[DebuggerDisplay("SessionExecutionStatisticsRecord (Name={Name}; Value={Value})")]
	public struct SessionExecutionStatisticsRecord
	{
		public string Name { get; set; }
		
		public decimal Value { get; set; }
	}
}
