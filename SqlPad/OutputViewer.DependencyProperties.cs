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
		public static readonly DependencyProperty EnableChildReferenceDataSourcesProperty = DependencyProperty.Register(nameof(EnableChildReferenceDataSources), typeof(bool), typeof(OutputViewer), new FrameworkPropertyMetadata(false));
		public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(OutputViewer), new FrameworkPropertyMetadata(TitlePropertyChangedCallbackHandler));
		public static readonly DependencyProperty DatabaseOutputProperty = DependencyProperty.Register(nameof(DatabaseOutput), typeof(string), typeof(OutputViewer), new FrameworkPropertyMetadata(String.Empty));
		public static readonly DependencyProperty ExecutionLogProperty = DependencyProperty.Register(nameof(ExecutionLog), typeof(string), typeof(OutputViewer), new FrameworkPropertyMetadata(String.Empty));
		public static readonly DependencyProperty ActiveResultViewerProperty = DependencyProperty.Register(nameof(ActiveResultViewer), typeof(ResultViewer), typeof(OutputViewer), new FrameworkPropertyMetadata());

		public static readonly DependencyProperty IsDebuggerControlVisibleProperty = DependencyProperty.Register(nameof(IsDebuggerControlVisible), typeof(bool), typeof(OutputViewer), new FrameworkPropertyMetadata(false));
		public static readonly DependencyProperty IsDebuggerControlEnabledProperty = DependencyProperty.Register(nameof(IsDebuggerControlEnabled), typeof(bool), typeof(OutputViewer), new FrameworkPropertyMetadata(true));
		public static readonly DependencyProperty IsTransactionControlEnabledProperty = DependencyProperty.Register(nameof(IsTransactionControlEnabled), typeof(bool), typeof(OutputViewer), new FrameworkPropertyMetadata(true));
		public static readonly DependencyProperty HasActiveTransactionProperty = DependencyProperty.Register(nameof(HasActiveTransaction), typeof(bool), typeof(OutputViewer), new FrameworkPropertyMetadata(false));
		#endregion

		#region dependency property accessors
		[Bindable(true)]
		public ResultViewer ActiveResultViewer
		{
			get { return (ResultViewer)GetValue(ActiveResultViewerProperty); }
			set { SetValue(ActiveResultViewerProperty, value); }
		}

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
		public bool HasActiveTransaction
		{
			get { return (bool)GetValue(HasActiveTransactionProperty); }
			private set { SetValue(HasActiveTransactionProperty, value); }
		}

		[Bindable(true)]
		public bool IsTransactionControlEnabled
		{
			get { return (bool)GetValue(IsTransactionControlEnabledProperty); }
			private set { SetValue(IsTransactionControlEnabledProperty, value); }
		}

		[Bindable(true)]
		public bool IsDebuggerControlVisible
		{
			get { return (bool)GetValue(IsDebuggerControlVisibleProperty); }
			private set { SetValue(IsDebuggerControlVisibleProperty, value); }
		}

		[Bindable(true)]
		public bool IsDebuggerControlEnabled
		{
			get { return (bool)GetValue(IsDebuggerControlEnabledProperty); }
			private set { SetValue(IsDebuggerControlEnabledProperty, value); }
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
		public bool EnableChildReferenceDataSources
		{
			get { return (bool)GetValue(EnableChildReferenceDataSourcesProperty); }
			set { SetValue(EnableChildReferenceDataSourcesProperty, value); }
		}

		[Bindable(true)]
		public string DatabaseOutput
		{
			get { return (string)GetValue(DatabaseOutputProperty); }
			private set { SetValue(DatabaseOutputProperty, value); }
		}

		[Bindable(true)]
		public string ExecutionLog
		{
			get { return (string)GetValue(ExecutionLogProperty); }
			private set { SetValue(ExecutionLogProperty, value); }
		}

		[Bindable(true)]
		public string Title
		{
			get { return (string)GetValue(TitleProperty); }
			set { SetValue(TitleProperty, value); }
		}

		private static void TitlePropertyChangedCallbackHandler(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			((OutputViewer)dependencyObject).ConnectionAdapter.Identifier = (string)args.NewValue;
		}
		#endregion
	}
}