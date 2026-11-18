# Uso: .\scripts\nova-migration.ps1 NomeDaMudanca
# Gera a migration do EF para o Pdv.Data. Rode só quando mudar as entidades ou o PdvDbContext.
param([Parameter(Mandatory = $true)][string]$Nome)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
dotnet tool restore
dotnet ef migrations add $Nome --project src/Pdv.Data
Write-Host "`nPronto. Faça commit da pasta src/Pdv.Data/Migrations."
