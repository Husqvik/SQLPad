using System.Xml.Serialization;

namespace SqlPad.Oracle
{
	public partial class DocumentationPackageSubProgram
	{
		[XmlIgnore]
		public DocumentationPackage PackageDocumentation { get; internal set; }
	}
}
