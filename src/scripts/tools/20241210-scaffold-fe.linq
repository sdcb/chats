<Query Kind="Program">
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.IO.Compression</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

async Task Main()
{
	string wwwroot = Path.Combine(new DirectoryInfo(Path.GetDirectoryName(Util.CurrentQueryPath)!).Parent!.Parent!.ToString(), "BE", "web", "wwwroot");
	Directory.Delete(wwwroot, recursive: true);
	using HttpClient http = new();
	using ZipArchive zip = new(await http.GetStreamAsync("https://chats.sdcb.pub/latest/chats-fe.zip"), ZipArchiveMode.Read, leaveOpen: false);
	foreach (ZipArchiveEntry entry in zip.Entries)
	{
		entry.Uncapsulate()._storedEntryName = entry.FullName.Replace("chats-fe/", "");
		ExtractRelativeToDirectory(entry, wwwroot, overwrite: false);
	}
	File.WriteAllBytes(Path.Combine(wwwroot, ".gitkeep"), new byte[0]);
	Console.WriteLine("Done!");
}

internal static void ExtractRelativeToDirectory(ZipArchiveEntry source, string destinationDirectoryName, bool overwrite)
{
	ArgumentNullException.ThrowIfNull(source);
	ArgumentNullException.ThrowIfNull(destinationDirectoryName);

	// Note that this will give us a good DirectoryInfo even if destinationDirectoryName exists:
	DirectoryInfo di = Directory.CreateDirectory(destinationDirectoryName);
	string destinationDirectoryFullPath = di.FullName;
	if (!destinationDirectoryFullPath.EndsWith(Path.DirectorySeparatorChar))
	{
		char sep = Path.DirectorySeparatorChar;
		destinationDirectoryFullPath = string.Concat(destinationDirectoryFullPath, new ReadOnlySpan<char>(in sep));
	}

	string fileDestinationPath = Path.GetFullPath(Path.Combine(destinationDirectoryFullPath, SanitizeEntryFilePath(source.FullName)));

	if (!fileDestinationPath.StartsWith(destinationDirectoryFullPath, PathInternal.StringComparison))
		throw new IOException("IO_ExtractingResultsInOutside");

	if (Path.GetFileName(fileDestinationPath).Length == 0)
	{
		// If it is a directory:

		if (source.Length != 0)
			throw new IOException("IO_DirectoryNameWithData");

		Directory.CreateDirectory(fileDestinationPath);
	}
	else
	{
		// If it is a file:
		// Create containing directory:
		Directory.CreateDirectory(Path.GetDirectoryName(fileDestinationPath)!);
		source.ExtractToFile(fileDestinationPath, overwrite: overwrite);
	}


	static string SanitizeEntryFilePath(string entryPath, bool preserveDriveRoot = false) => entryPath.Replace('\0', '_');
}

/// <summary>Contains internal path helpers that are shared between many projects.</summary>
internal static partial class PathInternal
{
	/// <summary>Returns a comparison that can be used to compare file and directory names for equality.</summary>
	internal static StringComparison StringComparison
	{
		get
		{
			return IsCaseSensitive ?
				StringComparison.Ordinal :
				StringComparison.OrdinalIgnoreCase;
		}
	}

	/// <summary>Gets whether the system is case-sensitive.</summary>
	internal static bool IsCaseSensitive
	{
		get
		{
			return !(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() || OperatingSystem.IsTvOS() || OperatingSystem.IsWatchOS());
		}
	}
}