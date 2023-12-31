﻿Param ([Parameter(Mandatory=$true)][string]$Version)

$workdir = (Get-Location)
Push-Location -Path "src\Xecrets.File.Cli\bin\Release\net8.0\publish\osx-x64"
try {
    wsl chmod 775 XecretsFileCli
    wsl mkdir XecretsFileCli-Osx-x64-${Version}
    wsl cp -r XecretsFileCli XecretsFileCli-Osx-x64-${Version}
    wsl tar -czvf XecretsFileCli-Osx-x64-${Version}.tar.gz XecretsFileCli-Osx-x64-${Version}
    wsl rm -rf XecretsFileCli-Osx-x64-${Version}
}
finally
{
    Pop-Location
}
