using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

namespace SqlPad
{
	public partial class OutputViewer
	{
		#region dependency properties registration
		public static readonly DependencyProperty ShowAllSessionExecutionStatisticsProperty = DependencyProperty.Register(nameof(ShowAllSessionExecutionStatistics), typeof(bool), typeof(OutputViewer), new FrameworkPropertyMetadata(ShowAllSessionExecutionStatisticsPropertyChangedCallbackHandler));
		public static readonly DependencyProperty EnableDatabaseOutputProperty = DependencyProperty.Register(nameof(EnableDatabaseOutput), typeof(bool), typeof(OutputViewer), new FrameworkPropertyMetadata(false));
		public static readonly DependencyProperty IsPinnedProperty = DependencyProperty.Register(nameof(IsPinned), typeof(bool), typeof(OutputViewer), new FrameworkPropertyMetadata(false));
		public static readonly DependencyProperty EnableReferenceConstraintChildrenProperty = DependencyProperty.Register(nameof(EnableReferenceConstraintChildren), typeof(bool), typeof(OutputViewer), new FrameworkPropertyMetadata(false));
		public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(OutputViewer), new FrameworkPropertyMetadata(TitlePropertyChangedCallbackHandler));
		public static readonly DependencyProperty DatabaseOutputProperty = DependencyProperty.Register(nameof(DatabaseOutput), typeof(string), typeof(OutputViewer), new FrameworkPropertyMetadata(String.Empty));
		public static readonly DependencyProperty LastStatementTextProperty = DependencyProperty.Register(nameof(LastStatementText), typeof(string), typeof(OutputViewer), new FrameworkPropertyMetadata(String.Empty));

		public static readonly DependencyProperty IsDebuggerControlEnabledProperty = DependencyProperty.Register(nameof(IsDebuggerControlEnabled), typeof(bool), typeof(OutputViewer), new FrameworkPropertyMetadata(false));
		public static readonly DependencyProperty IsTransactionControlEnabledProperty = DependencyProperty.Register(nameof(IsTransactionControlEnabled), typeof(bool), typeof(OutputViewer), new FrameworkPropertyMetadata(true));
		public static readonly DependencyProperty TransactionControlVisibityProperty = DependencyProperty.Register(nameof(TransactionControlVisibity), typeof(Visibility), typeof(OutputViewer), new FrameworkPropertyMetadata(Visibility.Collapsed));

		public static readonly DependencyProperty SelectedCellNumericInfoVisibilityProperty = DependencyProperty.Register(nameof(SelectedCellNumericInfoVisibility), typeof(Visibility), typeof(OutputViewer), new FrameworkPropertyMetadata(Visibility.Collapsed));
		public static readonly DependencyProperty SelectedCellInfoVisibilityProperty = DependencyProperty.Register(nameof(SelectedCellInfoVisibility), typeof(Visibility), typeof(OutputViewer), new FrameworkPropertyMetadata(Visibility.Collapsed));

		public static readonly DependencyProperty SelectedCellValueCountProperty = DependencyProperty.Register(nameof(SelectedCellValueCount), typeof(int), typeof(OutputViewer), new FrameworkPropertyMetadata(0));
		public static readonly DependencyProperty SelectedCellSumProperty = DependencyProperty.Register(nameof(SelectedCellSum), typeof(decimal), typeof(OutputViewer), new FrameworkPropertyMetadata(0m));
		public static readonly DependencyProperty SelectedCellAverageProperty = DependencyProperty.Register(nameof(SelectedCellAverage), typeof(decimal), typeof(OutputViewer), new FrameworkPropertyMetadata(0m));
		public static readonly DependencyProperty SelectedCellMinProperty = DependencyProperty.Register(nameof(SelectedCellMin), typeof(decimal), typeof(OutputViewer), new FrameworkPropertyMetadata(0m));
		public static readonly DependencyProperty SelectedCellMaxProperty = DependencyProperty.Register(nameof(SelectedCellMax), typeof(decimal), typeof(OutputViewer), new FrameworkPropertyMetadata(0m));
		#endregion

		#region dependency property accessors
		[Bindable(true)]
		public bool ShowAllSessionExecutionStatistics
		{
			get { return (bool)GetValue(ShowAllSessionExecutionStatisticsProperty); }
			set { SetValue(ShowAllSessionExecutionStatisticsProperty, value); }
		}

		private static void ShowAllSessionExecutionStatisticsPropertyChangedCallbackHandler(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var outputViewer = (OutputViewer)dependencyObject;
			var viewSource = (CollectionViewSource)outputViewer.TabStatistics.Resources["SortedSessionExecutionStatistics"];
			viewSource.View.Refresh();
		}

		[Bindable(true)]
		public Visibility SelectedCellNumericInfoVisibility
		{
			get { return (Visibility)GetValue(SelectedCellNumericInfoVisibilityProperty); }
			private set { SetValue(SelectedCellNumericInfoVisibilityProperty, value); }
		}

		[Bindable(true)]
		public Visibility SelectedCellInfoVisibility
		{
			get { return (Visibility)GetValue(SelectedCellInfoVisibilityProperty); }
			private set { SetValue(SelectedCellInfoVisibilityProperty, value); }
		}

		[Bindable(true)]
		public Visibility TransactionControlVisibity
		{
			get { return (Visibility)GetValue(TransactionControlVisibityProperty); }
			private set { SetValue(TransactionControlVisibityProperty, value); }
		}

		[Bindable(true)]
		public bool IsTransactionControlEnabled
		{
			get { return (bool)GetValue(IsTransactionControlEnabledProperty); }
			private set { SetValue(IsTransactionControlEnabledProperty, value); }
		}

		[Bindable(true)]
		public bool IsDebuggerControlEnabled
		{
			get { return (bool)GetValue(IsDebuggerControlEnabledProperty); }
			private set { SetValue(IsDebuggerControlEnabledProperty, value); }
		}

		[Bindable(true)]
		public int SelectedCellValueCount
		{
			get { return (int)GetValue(SelectedCellValueCountProperty); }
			private set { SetValue(SelectedCellValueCountProperty, value); }
		}

		[Bindable(true)]
		public decimal SelectedCellSum
		{
			get { return (decimal)GetValue(SelectedCellSumProperty); }
			private set { SetValue(SelectedCellSumProperty, value); }
		}

		[Bindable(true)]
		public decimal SelectedCellAverage
		{
			get { return (decimal)GetValue(SelectedCellAverageProperty); }
			private set { SetValue(SelectedCellAverageProperty, value); }
		}

		[Bindable(true)]
		public decimal SelectedCellMin
		{
			get { return (decimal)GetValue(SelectedCellMinProperty); }
			private set { SetValue(SelectedCellMinProperty, value); }
		}

		[Bindable(true)]
		public decimal SelectedCellMax
		{
			get { return (decimal)GetValue(SelectedCellMaxProperty); }
			private set { SetValue(SelectedCellMaxProperty, value); }
		}

		[Bindable(true)]
		public bool EnableDatabaseOutput
		{
			get { return (bool)GetValue(EnableDatabaseOutputProperty); }
			set { SetValue(EnableDatabaseOutputProperty, value); }
		}

		[Bindable(true)]
		public bool IsPinned
		{
			get { return (bool)GetValue(IsPinnedProperty); }
			set { SetValue(IsPinnedProperty, value); }
		}

		[Bindable(true)]
		public bool EnableReferenceConstraintChildren
		{
			get { return (bool)GetValue(EnableReferenceConstraintChildrenProperty); }
			set { SetValue(EnableReferenceConstraintChildrenProperty, value); }
		}

		[Bindable(true)]
		public string DatabaseOutput
		{
			get { return (string)GetValue(DatabaseOutputProperty); }
			private set { SetValue(DatabaseOutputProperty, value); }
		}

		[Bindable(true)]
		public string LastStatementText
		{
			get { return (string)GetValue(LastStatementTextProperty); }
			private set { SetValue(LastStatementTextProperty, value); }
		}

		[Bindable(true)]
		public string Title
		{
			get { return (string)GetValue(TitleProperty); }
			set { SetValue(TitleProperty, value); }
		}

		private static void TitlePropertyChangedCallbackHandler(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			((OutputViewer)dependencyObject)._connectionAdapter.Identifier = (string)args.NewValue;
		}
		#endregion
	}
}