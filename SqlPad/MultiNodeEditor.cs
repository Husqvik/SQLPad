using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlPad
{
	public class MultiNodeEditor
	{
		private readonly ICollection<StatementDescriptionNode> _nodes;
		
		public event EventHandler Completed = delegate { };

		public MultiNodeEditor(IEnumerable<StatementDescriptionNode> nodes)
		{
			_nodes = nodes.ToArray();
		}

		public ICollection<StatementDescriptionNode> Nodes
		{
			get { return _nodes; }
		}
	}
}