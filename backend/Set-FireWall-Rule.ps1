# 需要管理员权限运行
param(
    [string]$RuleName = "Allow_Port_5080",
    [int]$Port = 5080
)

# 检查是否以管理员身份运行
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "需要管理员权限运行此脚本。请以管理员身份启动PowerShell。"
    exit 1
}

try {
    # 添加TCP规则
    New-NetFirewallRule -DisplayName $RuleName -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow -Enabled True
    Write-Host "已成功添加TCP防火墙规则：$RuleName" -ForegroundColor Green

    # 添加UDP规则
    New-NetFirewallRule -DisplayName "$RuleName-UDP" -Direction Inbound -Protocol UDP -LocalPort $Port -Action Allow -Enabled True
    Write-Host "已成功添加UDP防火墙规则：$RuleName-UDP" -ForegroundColor Green
}
catch {
    Write-Error "添加防火墙规则时出错：$_"
    exit 1
}

# 显示已创建的规则
Write-Host "`n已创建的防火墙规则：" -ForegroundColor Yellow
Get-NetFirewallRule -DisplayName "$RuleName*" | Format-Table DisplayName, Enabled, Direction, Action, Protocol, LocalPort