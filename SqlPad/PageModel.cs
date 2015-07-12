using System.Collections.Generic;
using System.Windows;

namespace SqlPad
{
	public class PageModel : ModelBase
	{
		private readonly DocumentPage _documentPage;
		private IReadOnlyList<BindVariableModel> _bindVariables;
		private bool _isModified;

		private Visibility _productionLabelVisibility = Visibility.Collapsed;
		private Visibility _bindVariableListVisibility = Visibility.Collapsed;
		
		private string _dateTimeFormat;
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
			get { return _documentPage.IsBusy; }
		}

		public void NotifyIsRunning()
		{
			RaisePropertyChanged("IsRunning");
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
	}
}
