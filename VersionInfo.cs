static class VersionInfo
{
	public const string MAINVERSION = "1.5.2";
	public const string RELEASEDATE = "August 22, 2013";
	public static bool INTERIM = false;

	public static string GetEmuVersion()
	{
		return INTERIM ? "SVN " + SubWCRev.SVN_REV : ("Version " + MAINVERSION);
	}
}
