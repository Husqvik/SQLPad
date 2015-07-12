namespace SqlPad
{
	public class PageModel : ModelBase
	{
		private readonly DocumentPage _documentPage;
		private bool _isModified;

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

		public string DateTimeFormat
		{
			get { return _dateTimeFormat; }
			set { UpdateValueAndRaisePropertyChanged(ref _dateTimeFormat, value); }
		}
	}
}
