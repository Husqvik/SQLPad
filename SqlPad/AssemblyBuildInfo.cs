using System;
using System.Globalization;

namespace SqlPad
{
	[AttributeUsage(AttributeTargets.Assembly)]
	public class AssemblyBuildInfo : Attribute
	{
		public const string TimestampMask = "yyyy-MM-dd HH:mm:ss";

		public AssemblyBuildInfo(string lastGitCommitHash, string timestampString)
		{
			VersionTimestampString = timestampString;
			VersionTimestamp = DateTime.ParseExact(timestampString, TimestampMask, CultureInfo.InvariantCulture);
			LastGitCommitHash = lastGitCommitHash;
		}

		public DateTime VersionTimestamp { get; private set; }

		public string VersionTimestampString { get; private set; }

		public string LastGitCommitHash { get; private set; }
	}
}
