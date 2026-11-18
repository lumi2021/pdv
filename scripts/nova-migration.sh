#!/usr/bin/env bash
# Uso: ./scripts/nova-migration.sh NomeDaMudanca
# Gera a migration do EF para o Pdv.Data. Rode só quando mudar as entidades ou o PdvDbContext.
set -euo pipefail
[ $# -ge 1 ] || { echo "Uso: $0 NomeDaMudanca"; exit 1; }
cd "$(dirname "$0")/.."
dotnet tool restore
dotnet ef migrations add "$1" --project src/Pdv.Data
echo; echo "Pronto. Faça commit da pasta src/Pdv.Data/Migrations."
