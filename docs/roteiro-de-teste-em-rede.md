# Roteiro de teste em rede real

Faça com o PDV instalado (não pelo Visual Studio) e **pelo menos dois celulares de verdade** no Wi-Fi da
lanchonete. Marque cada item. Se algum falhar, anote o que viu, a hora, e envie também o arquivo mais recente
de **Ajustes > Abrir pasta de registros**.

## 0. Antes de começar
- [ ] Computador e celulares no **mesmo Wi-Fi** (não é a "rede de convidados"; sem VPN; dados móveis desligados nos celulares).
- [ ] No computador: instalar pelo instalador, abrir, passar pelo assistente de primeira abertura.
- [ ] Rodar `scripts/testar-rede` (`.ps1` no Windows, `.sh` no Linux). Deve dizer "responde" para o endereço 192.168.x.x / 10.x.x.x.
- [ ] Windows: a rede está como **Privada** (não Pública).

## 1. Conectar os celulares
- [ ] Celular A: escanear o QR Code da seção *Celular*, digitar o nome, entrar. O nome aparece na lista de celulares conectados.
- [ ] Celular B: repetir. A lista mostra os dois.
- [ ] Digitar o endereço à mão no navegador do celular (sem QR) e o código de 8 caracteres: entra.
- [ ] Digitar um código errado: mostra "Código inválido". Depois de 5 erros seguidos o código é trocado sozinho.
- [ ] "Gerar novo código": o código antigo deixa de funcionar.
- [ ] "Desconectar" um celular: ele volta para a tela de conexão em até alguns segundos.

## 2. Fluxo do garçom
- [ ] Cardápio aparece com categorias; produto sem estoque aparece "Esgotado"; o estoque baixo mostra "Restam N".
- [ ] Montar um pedido (mesa + observação num item) e enviar. Ele aparece **na hora** na tela Pedidos do computador (sem clicar em nada).
- [ ] Pedir mais do que o estoque permite: aparece uma mensagem clara dizendo o que falta, e nada é gravado.
- [ ] No computador: *Começar a preparar* > *Marcar como pronto*. O celular que fez o pedido **vibra e mostra aviso** de pedido pronto, e o outro celular também vê a mudança.
- [ ] Entregar o pedido: some da fila e a nota aparece na seção *Notas*, com o valor certo e o aviso "sem valor fiscal".
- [ ] Cancelar um pedido que ainda não começou: o estoque volta. Cancelar um já pronto "sem devolver": o estoque não volta.
- [ ] Exportar a nota em PDF: abre, mostra o nome do negócio, itens e total.

## 3. Situações do dia a dia
- [ ] Os dois celulares enviam pedidos ao mesmo tempo (um em cada mesa): os números são diferentes e o estoque fecha certo.
- [ ] Celular bloqueia a tela por 2 minutos e é desbloqueado: a lista de pedidos se atualiza sozinha.
- [ ] Celular sai do Wi-Fi e volta: aparece o aviso de "sem conexão" e some ao voltar; o rascunho do pedido continua lá.
- [ ] Fechar a aba do navegador e reabrir o endereço: continua conectado (sem digitar o código de novo).
- [ ] Reiniciar o roteador: depois que o Wi-Fi volta, os celulares reconectam sozinhos.
- [ ] Fechar o PDV e abrir de novo: os celulares continuam conectados e os pedidos estão lá.
- [ ] Trocar o computador de Wi-Fi (ou o roteador dar outro IP): a seção *Celular* mostra o novo endereço e o QR novo.

## 4. Segurança básica
- [ ] De um celular **não conectado**, abrir `http://IP:PORTA/api/pedidos`: deve dar erro (não mostrar pedidos).
- [ ] Celular desconectado pelo computador não consegue mais enviar pedidos.

## 5. Dados e cópias de segurança
- [ ] *Ajustes > Fazer uma cópia agora*: aparece na lista; a pasta abre.
- [ ] No dia seguinte, existe uma cópia automática nova sem ninguém pedir.
- [ ] Instalar uma versão nova por cima: dados, pedidos e notas continuam; existe uma cópia "Antes de atualizar".
- [ ] Desinstalar e reinstalar: os dados continuam lá.

## 6. Resultado
Anote: sistema operacional e versão, modelo dos celulares e navegador, versão do PDV (em *Ajustes*), e tudo o que falhou.
