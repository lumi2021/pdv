# PDV Lanchonete

Ponto de venda para lanchonete: cardápio, estoque com baixa automática por ingrediente, fila de pedidos em
tempo real, notas internas (sem valor fiscal) e um terminal web para o celular do garçom.
Programa de computador em C# (.NET 10 + Avalonia); os dados ficam num arquivo SQLite no próprio computador.

## Instalar (para quem vai usar)

**Windows:** baixe `PdvLanchonete-Setup-<versão>.exe` na página de [Releases](../../releases) e execute.
Instalar "para todos os usuários" também libera o celular no firewall; a instalação só para você funciona,
mas o Windows pergunta na primeira abertura se pode liberar a rede (marque *Redes privadas*).

**Linux (Ubuntu, Debian e derivados):** baixe `pdv-lanchonete_<versão>_amd64.deb` e instale com
`sudo apt install ./pdv-lanchonete_<versão>_amd64.deb`. Outras distribuições: extraia o
`PdvLanchonete-linux-x64.tar.gz`, dê permissão (`chmod +x PdvLanchonete`) e execute.

Na primeira abertura um assistente pergunta o nome do negócio, oferece um cardápio de exemplo e mostra o
QR Code para conectar o celular. Não há nada mais para instalar ou configurar: o banco é criado sozinho e uma
cópia de segurança é feita todos os dias (seção **Ajustes**).

### O celular do garçom

O celular só precisa estar **no mesmo Wi-Fi** do computador: escaneie o QR Code da seção *Celular*, digite o
nome e pronto. Não precisa instalar aplicativo. Se não abrir, rode `scripts/testar-rede.ps1` (Windows) ou
`scripts/testar-rede.sh` (Linux) com o PDV aberto; ele diz o que está bloqueando.
Roteiro completo de testes: [docs/roteiro-de-teste-em-rede.md](docs/roteiro-de-teste-em-rede.md).

Onde ficam os dados: `%LocalAppData%\PdvLanchonete` (Windows) ou `~/.local/share/PdvLanchonete` (Linux).
Desinstalar o programa **não** apaga os dados.

## Desenvolver

```
dotnet build Pdv.slnx
dotnet test Pdv.slnx
dotnet run --project src/Pdv.Desktop
```

| Projeto | O que tem |
|---|---|
| `Pdv.Core` | Entidades, regras e interfaces dos serviços (sem dependências externas) |
| `Pdv.Data` | EF Core + SQLite, migrations, serviços, backup |
| `Pdv.Server` | Servidor web embutido (Kestrel): API, SignalR e a página do celular |
| `Pdv.Desktop` | Aplicativo Avalonia |
| `tests/Pdv.Tests` | Testes (xUnit) |

**Mudou uma entidade?** Gere a migration e faça commit da pasta `src/Pdv.Data/Migrations`:
`./scripts/nova-migration.sh NomeDaMudanca` (ou `.ps1` no Windows). Em Debug o app avisa se esquecer.
Para apagar o banco de teste local: `scripts/resetar-banco-dev`.

### Publicação (GitHub Actions)

O workflow `Publicar para teste` roda os testes e, se passarem, gera para Windows e Linux o instalador
(`.exe` Inno Setup / `.deb`) e o arquivo compactado; instala o pacote, abre o programa de verdade e confere
que o servidor do celular responde (`scripts/ci/fumaca.*`). Os arquivos ficam em *Artifacts* de cada execução.
Para criar um Release (pré-lançamento): `git tag v0.1.0 && git push origin v0.1.0`.

Notas: a biblioteca de PDF (QuestPDF) usa a licença Community, gratuita para empresas com faturamento anual
abaixo de US$ 1 milhão. O executável do Windows não é assinado, então o SmartScreen mostra um aviso na primeira
abertura ("Mais informações" > "Executar assim mesmo").
