<Query Kind="Statements" />

string projectFolder = new FileInfo(Util.CurrentQueryPath).Directory!.Parent!.ToString();
string provider = "Microsoft.EntityFrameworkCore.SqlServer";

Directory.SetCurrentDirectory(projectFolder);

{
	string contextName = "ChatsDB";
	string connectionStringName = "ConnectionStrings:ChatsDB";
	string options = string.Join(" ", new[]
	{
		"--data-annotations",
		"--force",
		$"--context {contextName}",
		"--output-dir DB",
		"--verbose",
		$"--no-onconfiguring",
	});
	string cmd = $"dotnet ef dbcontext scaffold Name={connectionStringName} {provider} {options}";
	Util.Cmd(cmd.Dump("command text"));
}