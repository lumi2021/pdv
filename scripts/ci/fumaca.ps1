# Teste de fumaça do app já empacotado (Windows): abre o programa, espera o servidor do celular
# responder e confere o básico. Uso: .\scripts\ci\fumaca.ps1 C:\caminho\PdvLanchonete.exe
param([Parameter(Mandatory = $true)][string]$Exe)
$ErrorActionPreference = 'Stop'
$falhou = $false
function Verifica($desc, [scriptblock]$teste) {
    $ok = $false
    try { $ok = [bool](& $teste) } catch { $ok = $false }
    Write-Host ("{0} {1}" -f ($(if ($ok) { 'OK   ' } else { 'FALHA' }), $desc))
    if (-not $ok) { $script:falhou = $true }
}
function Codigo($url, $metodo = 'GET', $corpo = $null) {
    try {
        $p = @{ Uri = $url; Method = $metodo; UseBasicParsing = $true; TimeoutSec = 5 }
        if ($corpo) { $p.Body = $corpo; $p.ContentType = 'application/json' }
        (Invoke-WebRequest @p).StatusCode
    } catch { if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 } }
}

$proc = Start-Process -FilePath $Exe -PassThru
$porta = $null
for ($i = 0; $i -lt 90 -and -not $porta; $i++) {
    foreach ($p in 5080..5099) {
        try {
            $r = Invoke-WebRequest -Uri "http://127.0.0.1:$p/api/status" -UseBasicParsing -TimeoutSec 2
            if ($r.Content -match 'PDV Lanchonete') { $porta = $p; break }
        } catch { }
    }
    if (-not $porta) { Start-Sleep -Seconds 1 }
}
if (-not $porta) { Write-Host 'FALHA o servidor do celular não respondeu em 90 s'; Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue; exit 1 }
Write-Host "OK    servidor respondendo na porta $porta"

$base = "http://127.0.0.1:$porta"
Verifica 'a página do celular abre' { (Invoke-WebRequest "$base/" -UseBasicParsing).Content -match '<title>PDV' }
Verifica 'script, estilo e canal em tempo real existem' { (Codigo "$base/app.js") -eq 200 -and (Codigo "$base/estilo.css") -eq 200 -and (Codigo "$base/signalr.min.js") -eq 200 }
Verifica 'sem pareamento, o cardápio é recusado (401)' { (Codigo "$base/api/cardapio") -eq 401 }
Verifica 'sem pareamento, o canal em tempo real é recusado (401)' { (Codigo "$base/hubs/pedidos/negotiate?negotiateVersion=1" 'POST') -eq 401 }
Verifica 'código de pareamento errado é recusado (403)' { (Codigo "$base/api/parear" 'POST' '{"codigo":"ZZZZZZZZ","nome":"x"}') -eq 403 }
Verifica 'o app continua aberto (não caiu)' { -not (Get-Process -Id $proc.Id -ErrorAction SilentlyContinue).HasExited }

Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
if ($falhou) { exit 1 }
Write-Host 'TUDO OK'
