# Diagnóstico para quando o celular não abre a página do PDV. Rode com o PDV aberto neste computador.
# Uso: .\scripts\testar-rede.ps1
$porta = $null
foreach ($p in 5080..5099) {
    try {
        $r = Invoke-WebRequest -Uri "http://127.0.0.1:$p/api/status" -UseBasicParsing -TimeoutSec 2
        if ($r.Content -match 'PDV Lanchonete') { $porta = $p; break }
    } catch { }
}
if (-not $porta) {
    Write-Host 'O PDV não está respondendo neste computador.'
    Write-Host '-> Abra o PDV, espere a tela principal aparecer e rode este teste de novo.'
    exit 1
}
Write-Host "OK: o PDV está funcionando neste computador (porta $porta)."

$ips = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } |
    Select-Object -ExpandProperty IPAddress
if (-not $ips) {
    Write-Host 'Este computador não tem endereço de rede. Conecte-o ao Wi-Fi ou ao cabo da lanchonete.'
    exit 1
}

Write-Host "`nEndereços deste computador na rede (use o que começa com 192.168 ou 10.):"
foreach ($ip in $ips) {
    $ok = $false
    try { $ok = (Invoke-WebRequest -Uri "http://${ip}:$porta/api/status" -UseBasicParsing -TimeoutSec 3).StatusCode -eq 200 } catch { }
    Write-Host ("  http://{0}:{1}   ({2})" -f $ip, $porta, $(if ($ok) { 'responde' } else { 'NÃO responde' }))
}

Write-Host ''
$regra = netsh advfirewall firewall show rule name="PDV Lanchonete" 2>$null
if ($LASTEXITCODE -eq 0 -and $regra -match 'PDV Lanchonete') {
    Write-Host 'Firewall do Windows: existe a regra "PDV Lanchonete".'
} else {
    Write-Host 'Firewall do Windows: não há regra "PDV Lanchonete". Na primeira vez o Windows pergunta se pode liberar:'
    Write-Host '  marque "Redes privadas" e clique em "Permitir acesso". Se você já negou, rode como administrador:'
    Write-Host '  netsh advfirewall firewall add rule name="PDV Lanchonete" dir=in action=allow protocol=TCP localport=5080-5099 profile=private'
}
$perfil = Get-NetConnectionProfile -ErrorAction SilentlyContinue | Where-Object { $_.NetworkCategory -eq 'Public' }
if ($perfil) {
    Write-Host ''
    Write-Host 'ATENÇÃO: a rede atual está marcada como "Pública" no Windows e ela bloqueia o celular.'
    Write-Host '  Configurações > Rede e Internet > Wi-Fi > (sua rede) > Tipo de perfil de rede: Privada.'
}

Write-Host ''
Write-Host 'Se o celular ainda não abre a página:'
Write-Host "  1. O celular precisa estar no MESMO Wi-Fi deste computador (não use 'rede de convidados' nem dados móveis)."
Write-Host "  2. Alguns roteadores têm 'isolamento de clientes' (AP isolation): desative essa opção."
Write-Host '  3. Desligue a VPN, no computador e no celular.'
