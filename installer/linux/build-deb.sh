#!/usr/bin/env bash
# Monta o pacote .deb do PDV Lanchonete (Debian, Ubuntu e derivados).
# Uso: installer/linux/build-deb.sh <versao> <pasta-publicada> <arquivo-de-saida.deb>
set -euo pipefail

[ $# -eq 3 ] || { echo "Uso: $0 <versao> <pasta-publicada> <saida.deb>"; exit 1; }
versao="$1"; origem="$2"; saida="$3"
aqui="$(cd "$(dirname "$0")" && pwd)"
raiz="$(mktemp -d)"
trap 'rm -rf "$raiz"' EXIT
chmod 755 "$raiz"

install -d "$raiz/DEBIAN" "$raiz/opt/pdv-lanchonete" "$raiz/usr/bin" \
           "$raiz/usr/share/applications" "$raiz/usr/share/icons/hicolor/256x256/apps"

cp -r "$origem"/. "$raiz/opt/pdv-lanchonete/"
chmod 755 "$raiz/opt/pdv-lanchonete/PdvLanchonete"
ln -s /opt/pdv-lanchonete/PdvLanchonete "$raiz/usr/bin/pdv-lanchonete"
install -m 644 "$aqui/pdv-lanchonete.desktop" "$raiz/usr/share/applications/pdv-lanchonete.desktop"
install -m 644 "$aqui/../../src/Pdv.Desktop/Assets/icone.png" "$raiz/usr/share/icons/hicolor/256x256/apps/pdv-lanchonete.png"
install -m 755 "$aqui/postinst" "$raiz/DEBIAN/postinst"
install -m 755 "$aqui/postrm" "$raiz/DEBIAN/postrm"

cat > "$raiz/DEBIAN/control" <<CONTROLE
Package: pdv-lanchonete
Version: $versao
Section: misc
Priority: optional
Architecture: amd64
Maintainer: PDV Lanchonete <pdv@localhost>
Depends: libicu76 | libicu74 | libicu72 | libicu70 | libicu-dev, libfontconfig1, libice6, libsm6, libx11-6
Installed-Size: $(du -sk "$raiz/opt" | cut -f1)
Description: Ponto de venda para lanchonete
 Cardapio, estoque com baixa por ingrediente, fila de pedidos em tempo real,
 notas internas e terminal web para o celular do garcom.
CONTROLE

dpkg-deb --build --root-owner-group "$raiz" "$saida"
echo "Pacote criado: $saida"
