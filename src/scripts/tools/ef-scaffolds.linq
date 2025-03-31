<Query Kind="Statements">
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

string projectFolder = Path.Combine(new FileInfo(Util.CurrentQueryPath).Directory!.Parent!.Parent!.ToString(), "BE");
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
	//Util.Cmd(cmd.Dump("command text"));
	cmd.Dump("command text");

	// 使用 Process.Start 执行命令并打印日志
	ProcessStartInfo processStartInfo = new()
	{
		FileName = cmd.Split(' ', 2)[0],
		Arguments = cmd.Split(' ', 2)[1],
		RedirectStandardOutput = true,
		RedirectStandardError = true,
		UseShellExecute = false,
		CreateNoWindow = true,
		StandardOutputEncoding = Encoding.UTF8, 
		StandardErrorEncoding = Encoding.UTF8,
	};
	processStartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

	using Process process = Process.Start(processStartInfo)!;
	if (process != null)
	{
		// 异步读取标准输出
		Task outputTask = Task.Run(() =>
		{
			using StreamReader reader = process.StandardOutput;
			string? line;
			while ((line = reader.ReadLine()) != null)
			{
				Console.WriteLine(Util.FixedFont(Util.Metatext(line)));
			}
		});

		// 异步读取错误输出
		Task errorTask = Task.Run(() =>
		{
			using StreamReader reader = process.StandardError;
			string? line;
			while ((line = reader.ReadLine()) != null)
			{
				Console.WriteLine(Util.FixedFont(Util.WithStyle(line, "color: red")));
			}
		});

		// 等待进程完成
		process.WaitForExit();

		// 确保输出和错误流的任务完成
		Task.WaitAll(outputTask, errorTask);
	}
	else
	{
		"Failed to start process.".Dump("Error");
	}
}