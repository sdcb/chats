# Entity Framework Scaffold Script
# 等效于 ef-scaffolds.linq 的 PowerShell 版本
# 用法: .\ef-scaffolds.ps1
# dotnet tool install --global dotnet-ef
# dotnet tool update  --global dotnet-ef


# 确保当前脚本目录作为起点
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFolder = Join-Path (Split-Path (Split-Path $scriptPath -Parent) -Parent) "BE/web"
$provider = "Microsoft.EntityFrameworkCore.SqlServer"

Write-Host "切换到项目目录: $projectFolder" -ForegroundColor Green
Set-Location $projectFolder

# 检查项目文件是否存在
if (-not (Test-Path "Chats.BE.csproj")) {
    Write-Error "未找到 Chats.BE.csproj 项目文件"
    exit 1
}

# 设置环境变量
$env:ASPNETCORE_ENVIRONMENT = "Development"

# 配置参数
$contextName = "ChatsDB"
$connectionStringName = "ConnectionStrings:ChatsDB"
$options = @(
    "--data-annotations",
    "--force",
    "--context", $contextName,
    "--output-dir", "../db",
    "--verbose",
    "--namespace", "Chats.DB",
    "--no-onconfiguring"
)

# 构建完整命令参数
$arguments = @("ef", "dbcontext", "scaffold", "Name=$connectionStringName", $provider) + $options

Write-Host "执行命令: dotnet $($arguments -join ' ')" -ForegroundColor Yellow
Write-Host "======================================" -ForegroundColor Cyan

try {
    # 使用 Start-Process 执行命令，更简单的方式
    $process = Start-Process -FilePath "dotnet" -ArgumentList $arguments -NoNewWindow -Wait -PassThru
    
    if ($process.ExitCode -eq 0) {
        Write-Host "======================================" -ForegroundColor Cyan
        Write-Host "Entity Framework scaffold 成功完成!" -ForegroundColor Green
    } else {
        Write-Host "======================================" -ForegroundColor Cyan
        Write-Host "Entity Framework scaffold 失败，退出代码: $($process.ExitCode)" -ForegroundColor Red
        exit $process.ExitCode
    }
}
catch {
    Write-Error "执行过程中发生错误: $($_.Exception.Message)"
    exit 1
}
finally {
    # 恢复原始位置
    Set-Location $scriptPath
}