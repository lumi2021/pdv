#!/usr/bin/env bash
# Teste de fumaça do app já empacotado (Linux): abre o programa de verdade (numa tela virtual),
# espera o servidor do celular responder na rede e confere o básico. Falha se qualquer passo falhar.
# Uso: scripts/ci/fumaca.sh /caminho/do/PdvLanchonete
set -uo pipefail

exe="${1:?Informe o caminho do executável}"
log="$(mktemp)"
falhou=0

# O nome do processo no Linux é o nome do arquivo usado para abrir (um atalho como /usr/bin/pdv-lanchonete
# vira "pdv-lanchonete") e é cortado em 15 letras. Por isso procuramos pelo nome do próprio executável.
nome="$(basename "$exe")"
nome="${nome:0:15}"

verifica() { # descrição, comando...
    local desc="$1"; shift
    if "$@" >/dev/null 2>&1; then echo "OK    $desc"; else echo "FALHA $desc"; falhou=1; fi
}

if [ -z "${DISPLAY:-}" ]; then
    xvfb-run -a "$exe" >"$log" 2>&1 &
else
    "$exe" >"$log" 2>&1 &
fi

# O app escolhe a primeira porta livre a partir da 5080. Espera até 90 s o servidor subir.
porta=""
for _ in $(seq 1 90); do
    for p in $(seq 5080 5099); do
        if curl -fs --max-time 2 "http://127.0.0.1:$p/api/status" 2>/dev/null | grep -q "PDV Lanchonete"; then porta=$p; break 2; fi
    done
    sleep 1
done

if [ -z "$porta" ]; then
    echo "FALHA o servidor do celular não respondeu em 90 s"
    echo "---- saída do app ----"; cat "$log"
    pkill -9 -x "$nome" 2>/dev/null; exit 1
fi
echo "OK    servidor respondendo na porta $porta"

base="http://127.0.0.1:$porta"
verifica "a página do celular abre" bash -c "curl -fs '$base/' | grep -qi '<title>PDV'"
verifica "o script e o estilo da página existem" bash -c "curl -fs '$base/app.js' >/dev/null && curl -fs '$base/estilo.css' >/dev/null && curl -fs '$base/signalr.min.js' >/dev/null"
verifica "sem pareamento, o cardápio é recusado (401)" bash -c "[ \"\$(curl -s -o /dev/null -w '%{http_code}' '$base/api/cardapio')\" = 401 ]"
verifica "sem pareamento, o canal em tempo real é recusado (401)" bash -c "[ \"\$(curl -s -o /dev/null -w '%{http_code}' -X POST '$base/hubs/pedidos/negotiate?negotiateVersion=1')\" = 401 ]"
verifica "código de pareamento errado é recusado (403)" bash -c "[ \"\$(curl -s -o /dev/null -w '%{http_code}' -X POST -H 'content-type: application/json' -d '{\"codigo\":\"ZZZZZZZZ\",\"nome\":\"x\"}' '$base/api/parear')\" = 403 ]"

# Aceita conexões vindas da rede (e não só do próprio computador)?
ip="$(hostname -I 2>/dev/null | awk '{print $1}')"
if [ -n "$ip" ]; then
    verifica "responde também pelo endereço da rede ($ip)" bash -c "curl -fs --max-time 5 'http://$ip:$porta/api/status' | grep -q 'PDV Lanchonete'"
fi

verifica "o app continua aberto (não caiu)" pgrep -x "$nome"

pkill -9 -x "$nome" 2>/dev/null
sleep 1
if [ "$falhou" -ne 0 ]; then echo "---- saída do app ----"; cat "$log"; exit 1; fi
echo "TUDO OK"
