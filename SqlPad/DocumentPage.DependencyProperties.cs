using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Windows;

namespace SqlPad
{
	public partial class DocumentPage
	{
		#region dependency properties registration
		public static readonly DependencyProperty ConnectionStatusProperty = DependencyProperty.Register("ConnectionStatus", typeof(ConnectionStatus), typeof(DocumentPage), new FrameworkPropertyMetadata(ConnectionStatus.Connecting));
		public static readonly DependencyProperty EnableDebugModeProperty = DependencyProperty.Register("EnableDebugMode", typeof(bool), typeof(DocumentPage), new FrameworkPropertyMetadata(true));
		public static readonly DependencyProperty IsProductionConnectionProperty = DependencyProperty.Register("IsProductionConnection", typeof(bool), typeof(DocumentPage), new FrameworkPropertyMetadata(false));
		public static readonly DependencyProperty IsModifiedProperty = DependencyProperty.Register("IsModified", typeof(bool), typeof(DocumentPage), new FrameworkPropertyMetadata(false));
		public static readonly DependencyProperty IsRunningProperty = DependencyProperty.Register("IsRunning", typeof(bool), typeof(DocumentPage), new FrameworkPropertyMetadata(false));
		public static readonly DependencyProperty ConnectionErrorMessageProperty = DependencyProperty.Register("ConnectionErrorMessage", typeof(string), typeof(DocumentPage), new FrameworkPropertyMetadata(String.Empty));
		public static readonly DependencyProperty DocumentHeaderBackgroundColorCodeProperty = DependencyProperty.Register("DocumentHeaderBackgroundColorCode", typeof(string), typeof(DocumentPage), new FrameworkPropertyMetadata(DocumentHeaderBackgroundColorCodePropertyChangedCallbackHandler) { DefaultValue = WorkDocument.DefaultDocumentHeaderBackgroundColorCode });
		public static readonly DependencyProperty DocumentHeaderProperty = DependencyProperty.Register("DocumentHeader", typeof(string), typeof(DocumentPage), new FrameworkPropertyMetadata(DocumentHeaderPropertyChangedCallbackHandler));
		public static readonly DependencyProperty DocumentHeaderToolTipProperty = DependencyProperty.Register("DocumentHeaderToolTip", typeof(string), typeof(DocumentPage), new FrameworkPropertyMetadata(String.Empty));
		public static readonly DependencyProperty DateTimeFormatProperty = DependencyProperty.Register("DateTimeFormat", typeof(string), typeof(DocumentPage), new FrameworkPropertyMetadata());
		public static readonly DependencyProperty SchemaLabelProperty = DependencyProperty.Register("SchemaLabel", typeof(string), typeof(DocumentPage), new FrameworkPropertyMetadata(String.Empty));
		public static readonly DependencyProperty CurrentSchemaProperty = DependencyProperty.Register("CurrentSchema", typeof(string), typeof(DocumentPage), new FrameworkPropertyMetadata(CurrentSchemaPropertyChangedCallbackHandler));
		public static readonly DependencyProperty CurrentConnectionProperty = DependencyProperty.Register("CurrentConnection", typeof(ConnectionStringSettings), typeof(DocumentPage), new FrameworkPropertyMetadata(CurrentConnectionPropertyChangedCallbackHandler));
		public static readonly DependencyProperty BindVariablesProperty = DependencyProperty.Register("BindVariables", typeof(IReadOnlyList<BindVariableModel>), typeof(DocumentPage), new FrameworkPropertyMetadata(new BindVariableModel[0]));
		#endregion

		#region dependency property accessors
		[Bindable(true)]
		public ConnectionStatus ConnectionStatus
		{
			get { return (ConnectionStatus)GetValue(ConnectionStatusProperty); }
			set { SetValue(ConnectionStatusProperty, value); }
		}

		[Bindable(true)]
		public bool EnableDebugMode
		{
			get { return (bool)GetValue(EnableDebugModeProperty); }
			set { SetValue(EnableDebugModeProperty, value); }
		}

		[Bindable(true)]
		public bool IsProductionConnection
		{
			get { return (bool)GetValue(IsProductionConnectionProperty); }
			set { SetValue(IsProductionConnectionProperty, value); }
		}

		[Bindable(true)]
		public bool IsModified
		{
			get { return (bool)GetValue(IsModifiedProperty); }
			set { SetValue(IsModifiedProperty, value); }
		}

		[Bindable(true)]
		public bool IsRunning
		{
			get { return (bool)GetValue(IsRunningProperty); }
			private set { SetValue(IsRunningProperty, value); }
		}

		[Bindable(true)]
		public string ConnectionErrorMessage
		{
			get { return (string)GetValue(ConnectionErrorMessageProperty); }
			private set { SetValue(ConnectionErrorMessageProperty, value); }
		}

		[Bindable(true)]
		public string DocumentHeaderBackgroundColorCode
		{
			get { return (string)GetValue(DocumentHeaderBackgroundColorCodeProperty); }
			set { SetValue(DocumentHeaderBackgroundColorCodeProperty, value); }
		}

		private static void DocumentHeaderBackgroundColorCodePropertyChangedCallbackHandler(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var newColorCode = (string)args.NewValue;
			var documentPage = (DocumentPage)dependencyObject;
			documentPage.WorkDocument.HeaderBackgroundColorCode = newColorCode;
			documentPage.SetDocumentModifiedIfSqlx();
		}

		[Bindable(true)]
		public string DocumentHeaderToolTip
		{
			get { return (string)GetValue(DocumentHeaderToolTipProperty); }
			set { SetValue(DocumentHeaderToolTipProperty, value); }
		}

		[Bindable(true)]
		public string DateTimeFormat
		{
			get { return (string)GetValue(DateTimeFormatProperty); }
			set { SetValue(DateTimeFormatProperty, value); }
		}

		[Bindable(true)]
		public string DocumentHeader
		{
			get { return (string)GetValue(DocumentHeaderProperty); }
			set { SetValue(DocumentHeaderProperty, value); }
		}

		private static void DocumentHeaderPropertyChangedCallbackHandler(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var newHeader = (string)args.NewValue;
			var documentPage = (DocumentPage)dependencyObject;
			documentPage.WorkDocument.DocumentTitle = newHeader;
			documentPage.SetDocumentModifiedIfSqlx();
		}

		private void SetDocumentModifiedIfSqlx()
		{
			if (_isInitializing || !WorkDocument.IsSqlx || WorkDocument.IsModified)
			{
				return;
			}

			WorkDocument.IsModified = IsModified = true;
		}

		[Bindable(true)]
		public string SchemaLabel
		{
			get { return (string)GetValue(SchemaLabelProperty); }
			private set { SetValue(SchemaLabelProperty, value); }
		}

		[Bindable(true)]
		public string CurrentSchema
		{
			get { return (string)GetValue(CurrentSchemaProperty); }
			set { SetValue(CurrentSchemaProperty, value); }
		}

		private static void CurrentSchemaPropertyChangedCallbackHandler(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var newSchema = (string)args.NewValue;
			if (String.IsNullOrEmpty(newSchema))
			{
				return;
			}

			var documentPage = (DocumentPage)dependencyObject;
			documentPage.DatabaseModel.CurrentSchema = newSchema;

			if (documentPage.DatabaseModel.IsInitialized)
			{
				documentPage.ReParse();
			}
		}

		[Bindable(true)]
		public ConnectionStringSettings CurrentConnection
		{
			get { return (ConnectionStringSettings)GetValue(CurrentConnectionProperty); }
			set { SetValue(CurrentConnectionProperty, value); }
		}

		private static void CurrentConnectionPropertyChangedCallbackHandler(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var newConnectionString = (ConnectionStringSettings)args.NewValue;
			var documentPage = (DocumentPage)dependencyObject;
			documentPage.ConnectionStatus = ConnectionStatus.Connecting;

			if (String.IsNullOrEmpty(documentPage.WorkDocument.DocumentTitle))
			{
				documentPage.DocumentHeader = documentPage.WorkDocument.File == null ? InitialDocumentHeader : documentPage.WorkDocument.File.Name;
			}
			else
			{
				documentPage.DocumentHeader = documentPage.WorkDocument.DocumentTitle;
			}

			documentPage.InitializeInfrastructureComponents(newConnectionString);
		}

		[Bindable(true)]
		public IReadOnlyList<BindVariableModel> BindVariables
		{
			get { return (IReadOnlyList<BindVariableModel>)GetValue(BindVariablesProperty); }
			set { SetValue(BindVariablesProperty, value); }
		}
		#endregion
	}
}