using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Documents;

namespace SqlPad
{
	public partial class ProgramOverloadList
	{

		public ProgramOverloadList()
		{
			InitializeComponent();
		}

		public ICollection<ProgramOverloadDescription> FunctionOverloads
		{
			set
			{
				foreach (var overloadDescription in value)
				{
					var textBlock = new TextBlock();
					textBlock.Inlines.Add(overloadDescription.Name);
					textBlock.Inlines.Add("(");

					var i = 0;
					foreach (var parameter in overloadDescription.Parameters)
					{
						if (i > 0)
						{
							textBlock.Inlines.Add(", ");
						}

						Inline inline = new Run(parameter);
						if (i == overloadDescription.CurrentParameterIndex)
						{
							inline = new Bold(inline);
						}

						textBlock.Inlines.Add(inline);
						i++;
					}

					textBlock.Inlines.Add(")");

					if (!String.IsNullOrEmpty(overloadDescription.ReturnedDatatype))
					{
						textBlock.Inlines.Add(" RETURN: ");
						textBlock.Inlines.Add(overloadDescription.ReturnedDatatype);
					}

					ViewOverloads.Items.Add(textBlock);
				}
			}
		}
	}
}
