#!/usr/bin/env bash
# Diagnóstico para quando o celular não abre a página do PDV. Rode com o PDV aberto neste computador.
# Uso: ./scripts/testar-rede.sh
porta=""
for p in $(seq 5080 5099); do
    if curl -fs --max-time 2 "http://127.0.0.1:$p/api/status" 2>/dev/null | grep -q "PDV Lanchonete"; then porta=$p; break; fi
done

if [ -z "$porta" ]; then
    echo "O PDV não está respondendo neste computador."
    echo "-> Abra o PDV, espere a tela principal aparecer e rode este teste de novo."
    exit 1
fi
echo "OK: o PDV está funcionando neste computador (porta $porta)."

ips="$(hostname -I 2>/dev/null)"
if [ -z "$ips" ]; then
    echo "Este computador não tem endereço de rede. Conecte-o ao Wi-Fi ou ao cabo da lanchonete."
    exit 1
fi

echo
echo "Endereços deste computador na rede (use o que começa com 192.168 ou 10.):"
for ip in $ips; do
    case "$ip" in *:*) continue ;; esac   # só IPv4
    if curl -fs --max-time 3 "http://$ip:$porta/api/status" >/dev/null 2>&1; then
        echo "  http://$ip:$porta   (responde)"
    else
        echo "  http://$ip:$porta   (NÃO responde)"
    fi
done

echo
if command -v ufw >/dev/null 2>&1 && ufw status 2>/dev/null | grep -q "Status: active"; then
    if ufw status | grep -Eq "5080:5099|$porta"; then
        echo "Firewall (ufw): ligado, com as portas do PDV liberadas."
    else
        echo "Firewall (ufw): ligado e SEM as portas do PDV liberadas. Se o celular não abrir, rode:"
        echo "  sudo ufw allow 5080:5099/tcp"
    fi
else
    echo "Firewall (ufw): desligado ou não instalado."
fi

echo
echo "Se o celular ainda não abre a página:"
echo "  1. O celular precisa estar no MESMO Wi-Fi deste computador (não use 'rede de convidados' nem dados móveis)."
echo "  2. Alguns roteadores têm 'isolamento de clientes' (AP isolation): desative essa opção."
echo "  3. Desligue a VPN, no computador e no celular."
