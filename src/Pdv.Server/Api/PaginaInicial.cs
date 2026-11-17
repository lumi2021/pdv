namespace Pdv.Server.Api;

internal static class PaginaInicial
{
    // Provisória: no próximo passo vira o PDV web do garçom.
    public const string Html = """
        <!doctype html>
        <html lang="pt-BR">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>PDV Lanchonete</title>
          <style>
            body { font-family: system-ui, sans-serif; margin: 0; padding: 32px 20px; background: #f9fafb; color: #111827; }
            main { max-width: 480px; margin: 0 auto; }
            h1 { font-size: 24px; }
            .ok { background: #ecfdf5; color: #047857; padding: 12px 16px; border-radius: 8px; font-weight: 600; }
          </style>
        </head>
        <body>
          <main>
            <h1>PDV Lanchonete</h1>
            <p class="ok">✅ Conectado ao servidor.</p>
            <p>A tela de pedidos do garçom chega na próxima atualização.</p>
          </main>
        </body>
        </html>
        """;
}
