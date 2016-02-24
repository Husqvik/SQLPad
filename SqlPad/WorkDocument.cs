using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace SqlPad
{
	[DebuggerDisplay("WorkDocument (DocumentFileName={DocumentFileName}; TabIndex={TabIndex}; ConnectionName={ConnectionName}; SchemaName={SchemaName}); FontSize={FontSize}")]
	public class WorkDocument
	{
		private const string ExtensionSqlx = ".SQLX";
		public const string DefaultDocumentHeaderBackgroundColorCode = "#FFFFFFFF";
		private double _fontSize;
		private TimeSpan _refreshInterval;
		private List<bool> _foldingStates;
		private HashSet<BreakpointData> _breakpoints;
		private string[] _watchItems;

		public WorkDocument()
		{
			DocumentId = Guid.NewGuid();
			TabIndex = -1;
			HeaderBackgroundColorCode = DefaultDocumentHeaderBackgroundColorCode;
		}

		private List<bool> FoldingStatesInternal => _foldingStates ?? (_foldingStates = new List<bool>());

		public string[] WatchItems
		{
			get { return _watchItems ?? (_watchItems = new string[0]); }
			set { _watchItems = value; }
		}

		public int DebuggerViewDefaultTabIndex { get; set; }

		public IReadOnlyList<bool> FoldingStates => FoldingStatesInternal.AsReadOnly();

		public bool IsSqlx => File != null && IsSqlxExtension(File.Extension);

		public static bool IsSqlxFile(string fileName)
		{
			return IsSqlxExtension(new FileInfo(fileName).Extension);
		}

		private static bool IsSqlxExtension(string extension)
		{
			return extension.ToUpperInvariant() == ExtensionSqlx;
		}

		public void UpdateFoldingStates(IEnumerable<bool> foldingStates)
		{
			FoldingStatesInternal.Clear();
			FoldingStatesInternal.AddRange(foldingStates);
		}

		public string Identifier => File?.Name ?? DocumentId.ToString("N");

		public Guid DocumentId { get; private set; }

		public FileInfo File => String.IsNullOrEmpty(DocumentFileName) ? null : new FileInfo(DocumentFileName);

	    public string DocumentFileName { get; set; }
		
		public string DocumentTitle { get; set; }
		
		public string ConnectionName { get; set; }
		
		public string SchemaName { get; set; }

		public string Text { get; set; }
		
		public int CursorPosition { get; set; }
		
		public int SelectionStart { get; set; }
		
		public int SelectionLength { get; set; }

		public SelectionType SelectionType { get; set; }

		public int TabIndex { get; set; }
		
		public bool IsModified { get; set; }
		
		public double VisualTop { get; set; }
		
		public double VisualLeft { get; set; }

		public double EditorGridRowHeight { get; set; }
		
		public double EditorGridColumnWidth { get; set; }

		public bool EnableDatabaseOutput { get; set; }

		public bool KeepDatabaseOutputHistory { get; set; }
		
		public string HeaderBackgroundColorCode { get; set; }

		public bool BreakOnExceptions { get; set; }

		public double FontSize
		{
			get
			{
				if (_fontSize == 0)
				{
					_fontSize = 12;
				}
				
				return _fontSize;
			}
			set { _fontSize = value; }
		}

		public TimeSpan RefreshInterval
		{
			get
			{
				if (_refreshInterval == TimeSpan.Zero)
				{
					_refreshInterval = TimeSpan.FromSeconds(60);
				}

				return _refreshInterval;
			}
			set { _refreshInterval = value; }
		}

		public ICollection<BreakpointData> Breakpoints => _breakpoints ?? (_breakpoints = new HashSet<BreakpointData>());

		public WorkDocument CloneAsRecent()
		{
			if (String.IsNullOrWhiteSpace(DocumentFileName))
			{
				throw new InvalidOperationException("Only persisted work document can be referred as recent document. ");
			}

			return new WorkDocument
			{
				ConnectionName = ConnectionName,
				SchemaName = SchemaName,
				DocumentFileName = DocumentFileName,
				DocumentTitle = DocumentTitle,
				CursorPosition = CursorPosition,
				FontSize = FontSize,
				_foldingStates = _foldingStates,
				KeepDatabaseOutputHistory = KeepDatabaseOutputHistory,
				EnableDatabaseOutput = EnableDatabaseOutput,
				EditorGridColumnWidth = EditorGridColumnWidth,
				EditorGridRowHeight = EditorGridRowHeight,
				SelectionLength = SelectionLength,
				SelectionStart = SelectionStart,
				VisualLeft = VisualLeft,
				VisualTop = VisualTop,
				HeaderBackgroundColorCode = HeaderBackgroundColorCode
			};
		}
	}
}
