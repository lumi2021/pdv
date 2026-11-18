#!/usr/bin/env bash
# Apaga o banco de DESENVOLVIMENTO (todos os dados de teste). O app recria tudo na próxima abertura.
pasta="${XDG_DATA_HOME:-$HOME/.local/share}/PdvLanchonete"
rm -f "$pasta"/pdv.db*
echo "Banco de desenvolvimento apagado em $pasta"
