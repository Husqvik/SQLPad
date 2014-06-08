namespace SqlPad.FindReplace
{
	public interface IEditor
	{
		string Text { get; }
		int SelectionStart { get; }
		int SelectionLength { get; }

		/// <summary>
		/// Selects the specified portion of Text and scrolls that part into view.
		/// </summary>
		/// <param name="start"></param>
		/// <param name="length"></param>
		void Select(int start, int length);

		void Replace(int start, int length, string replaceWith);

		/// <summary>
		/// This method is called before a replace all operation.
		/// </summary>
		void BeginChange();

		/// <summary>
		/// This method is called after a replace all operation.
		/// </summary>
		void EndChange();
	}
}