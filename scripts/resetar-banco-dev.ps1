# Apaga o banco de DESENVOLVIMENTO (todos os dados de teste). O app recria tudo na próxima abertura.
$pasta = Join-Path $env:LOCALAPPDATA 'PdvLanchonete'
Remove-Item (Join-Path $pasta 'pdv.db*') -Force -ErrorAction SilentlyContinue
Write-Host "Banco de desenvolvimento apagado em $pasta"
