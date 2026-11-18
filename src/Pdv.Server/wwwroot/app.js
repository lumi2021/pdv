'use strict';

/* PDV Lanchonete — terminal do garçom. JavaScript puro, sem dependências nem etapa de build. */

const CHAVE_DISPOSITIVO = 'pdv.dispositivo';
const CHAVE_RASCUNHO = 'pdv.rascunho';
const INTERVALO_ATUALIZACAO_MS = 5000;

const moeda = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });
const dinheiro = (valor) => moeda.format(valor);

const STATUS = {
  APreparar: { texto: 'Na fila', classe: 'fila' },
  EmPreparo: { texto: 'Preparando', classe: 'preparo' },
  Pronto: { texto: 'Pronto!', classe: 'pronto' },
};

const estado = {
  token: null,
  nome: null,
  codigoDaUrl: null,
  mensagemPareamento: '',
  aba: 'pedido',
  produtos: [],
  categoria: 'Todos',
  fila: [],
  prontosVistos: null,
  carrinho: new Map(), // produtoId -> { produto, quantidade, observacao }
  identificacao: '',
  observacao: '',
  carrinhoAberto: false,
  enviando: false,
  erroEnvio: '',
  online: true,
};

const refs = {};
let temporizador = null;
let temporizadorAviso = null;

/* ------------------------------------------------------------------ utilidades */

function h(tag, props, ...filhos) {
  const el = document.createElement(tag);
  for (const [chave, valor] of Object.entries(props || {})) {
    if (valor === null || valor === undefined || valor === false) continue;
    if (chave === 'class') el.className = valor;
    else if (chave.startsWith('on')) el.addEventListener(chave.slice(2), valor);
    else if (chave === 'value') el.value = valor;
    else if (chave === 'disabled' || chave === 'hidden') el[chave] = Boolean(valor);
    else el.setAttribute(chave, valor === true ? '' : valor);
  }
  for (const filho of filhos.flat()) {
    if (filho === null || filho === undefined || filho === false) continue;
    el.append(filho.nodeType ? filho : document.createTextNode(String(filho)));
  }
  return el;
}

function lerArmazenado(chave) {
  try {
    const texto = localStorage.getItem(chave);
    return texto ? JSON.parse(texto) : null;
  } catch {
    return null;
  }
}

function gravarArmazenado(chave, valor) {
  try {
    if (valor === null) localStorage.removeItem(chave);
    else localStorage.setItem(chave, JSON.stringify(valor));
  } catch {
    /* navegação privativa: segue sem guardar */
  }
}

function haQuantoTempo(iso) {
  const minutos = Math.max(0, Math.round((Date.now() - new Date(iso).getTime()) / 60000));
  if (minutos < 1) return 'agora';
  if (minutos < 60) return `há ${minutos} min`;
  return `há ${Math.floor(minutos / 60)} h`;
}

function mostrarAviso(texto, tipo = 'ok') {
  const el = document.getElementById('aviso');
  el.textContent = texto;
  el.className = 'aviso' + (tipo === 'erro' ? ' erro' : '');
  el.hidden = false;
  clearTimeout(temporizadorAviso);
  temporizadorAviso = setTimeout(() => { el.hidden = true; }, 4000);
}

/* ------------------------------------------------------------------ comunicação com o servidor */

class ErroApp extends Error {
  constructor(mensagem, { sessaoEncerrada = false, offline = false } = {}) {
    super(mensagem);
    this.sessaoEncerrada = sessaoEncerrada;
    this.offline = offline;
  }
}

function definirConexao(online) {
  if (estado.online === online) return;
  estado.online = online;
  if (refs.bannerOffline) refs.bannerOffline.hidden = online;
  if (refs.ponto) refs.ponto.className = 'ponto' + (online ? '' : ' off');
}

async function api(caminho, { metodo = 'GET', corpo, semToken = false } = {}) {
  const cabecalhos = { Accept: 'application/json' };
  if (corpo !== undefined) cabecalhos['Content-Type'] = 'application/json';
  if (estado.token && !semToken) cabecalhos.Authorization = `Bearer ${estado.token}`;

  let resposta;
  try {
    resposta = await fetch(caminho, {
      method: metodo,
      headers: cabecalhos,
      body: corpo !== undefined ? JSON.stringify(corpo) : undefined,
    });
  } catch {
    definirConexao(false);
    throw new ErroApp('Sem conexão com o servidor. Confira o Wi-Fi do celular.', { offline: true });
  }
  definirConexao(true);

  let dados = null;
  try { dados = await resposta.json(); } catch { /* resposta sem corpo */ }

  if (resposta.status === 401 && !semToken) {
    esquecerDispositivo('Este celular foi desconectado. Leia o QR Code no computador para conectar de novo.');
    throw new ErroApp('Celular desconectado.', { sessaoEncerrada: true });
  }

  if (!resposta.ok) {
    throw new ErroApp(dados?.mensagem ?? 'Algo deu errado. Tente de novo.');
  }
  return dados;
}

function tratar(erro) {
  if (!erro.sessaoEncerrada) mostrarAviso(erro.message, 'erro');
}

/* ------------------------------------------------------------------ pareamento e entrada */

function esquecerDispositivo(mensagem) {
  estado.token = null;
  estado.nome = null;
  gravarArmazenado(CHAVE_DISPOSITIVO, null);
  pararAtualizacoes();
  estado.mensagemPareamento = mensagem ?? '';
  renderizar();
}

async function parear(codigo, nome) {
  const resposta = await api('/api/parear', { metodo: 'POST', corpo: { codigo, nome }, semToken: true });
  estado.token = resposta.token;
  estado.nome = resposta.nome;
  gravarArmazenado(CHAVE_DISPOSITIVO, { token: resposta.token, nome: resposta.nome });
  estado.codigoDaUrl = null;
  estado.mensagemPareamento = '';
  history.replaceState(null, '', location.pathname);
  await entrar();
}

async function entrar() {
  const eu = await api('/api/eu');
  estado.nome = eu.nome;
  await Promise.all([carregarCardapio(), carregarFila()]);
  estado.prontosVistos = new Set(estado.fila.filter((p) => p.status === 'Pronto').map((p) => p.id));
  restaurarRascunho();
  renderizar();
  iniciarAtualizacoes();
}

async function carregarCardapio() {
  estado.produtos = await api('/api/cardapio');
  for (const item of estado.carrinho.values()) {
    item.produto = estado.produtos.find((p) => p.id === item.produto.id) ?? item.produto;
  }
}

async function carregarFila() {
  estado.fila = await api('/api/pedidos');
}

/* ------------------------------------------------------------------ rascunho do pedido (sobrevive a recarregar a página) */

function salvarRascunho() {
  gravarArmazenado(CHAVE_RASCUNHO, {
    identificacao: estado.identificacao,
    observacao: estado.observacao,
    itens: [...estado.carrinho.values()].map((i) => [i.produto.id, i.quantidade, i.observacao]),
  });
}

function restaurarRascunho() {
  const rascunho = lerArmazenado(CHAVE_RASCUNHO);
  if (!rascunho || estado.carrinho.size > 0) return;
  estado.identificacao = rascunho.identificacao ?? '';
  estado.observacao = rascunho.observacao ?? '';
  for (const [id, quantidade, observacao] of rascunho.itens ?? []) {
    const produto = estado.produtos.find((p) => p.id === id);
    if (produto && quantidade > 0) estado.carrinho.set(id, { produto, quantidade, observacao: observacao ?? '' });
  }
}

/* ------------------------------------------------------------------ atualização periódica */

function iniciarAtualizacoes() {
  pararAtualizacoes();
  temporizador = setInterval(atualizar, INTERVALO_ATUALIZACAO_MS);
}

function pararAtualizacoes() {
  clearInterval(temporizador);
  temporizador = null;
}

async function atualizar() {
  if (document.hidden || !estado.token || estado.enviando) return;

  try {
    const filaAntes = JSON.stringify(estado.fila);
    await carregarFila();
    avisarPedidosProntos();
    if (JSON.stringify(estado.fila) !== filaAntes) renderizarListaPedidos();
    atualizarEmblema();

    if (estado.aba === 'pedido' && !estado.carrinhoAberto) {
      const produtosAntes = JSON.stringify(estado.produtos);
      await carregarCardapio();
      if (JSON.stringify(estado.produtos) !== produtosAntes) {
        renderizarListaProdutos();
        renderizarBarraCarrinho();
      }
    }
  } catch {
    /* api() já marcou a conexão como perdida; tenta de novo no próximo ciclo */
  }
}

function avisarPedidosProntos() {
  const prontos = estado.fila.filter((p) => p.status === 'Pronto');
  const novos = prontos.filter((p) => !estado.prontosVistos?.has(p.id));
  estado.prontosVistos = new Set(prontos.map((p) => p.id));

  if (novos.length > 0) {
    const p = novos[0];
    mostrarAviso(`Pedido #${p.numero} (${p.identificacao}) está pronto!`);
    if (navigator.vibrate) navigator.vibrate([200, 100, 200]);
  }
}

/* ------------------------------------------------------------------ carrinho */

function quantidadeNoCarrinho(produtoId) {
  return estado.carrinho.get(produtoId)?.quantidade ?? 0;
}

function totais() {
  let itens = 0;
  let total = 0;
  for (const item of estado.carrinho.values()) {
    itens += item.quantidade;
    total += item.quantidade * item.produto.preco;
  }
  return { itens, total };
}

function alterarQuantidade(produto, delta) {
  let nova = quantidadeNoCarrinho(produto.id) + delta;

  if (produto.disponivel !== null && nova > produto.disponivel) {
    mostrarAviso(`Só restam ${produto.disponivel} de ${produto.nome}.`, 'erro');
    nova = produto.disponivel;
  }

  if (nova <= 0) {
    estado.carrinho.delete(produto.id);
  } else {
    const item = estado.carrinho.get(produto.id) ?? { produto, quantidade: 0, observacao: '' };
    item.produto = produto;
    item.quantidade = nova;
    estado.carrinho.set(produto.id, item);
  }

  salvarRascunho();
  renderizarListaProdutos();
  renderizarBarraCarrinho();

  if (estado.carrinhoAberto) {
    if (estado.carrinho.size === 0) estado.carrinhoAberto = false;
    renderizarCarrinho();
  }
}

function abrirCarrinho() {
  estado.carrinhoAberto = true;
  estado.erroEnvio = '';
  renderizarCarrinho();
}

function fecharCarrinho() {
  estado.carrinhoAberto = false;
  renderizarCarrinho();
}

async function enviarPedido() {
  const identificacao = estado.identificacao.trim();
  if (!identificacao) {
    estado.erroEnvio = 'Informe a mesa ou o nome do cliente.';
    renderizarCarrinho();
    document.getElementById('campo-identificacao')?.focus();
    return;
  }

  estado.enviando = true;
  estado.erroEnvio = '';
  renderizarCarrinho();

  try {
    const pedido = await api('/api/pedidos', {
      metodo: 'POST',
      corpo: {
        identificacao,
        observacao: estado.observacao.trim() || null,
        itens: [...estado.carrinho.values()].map((i) => ({
          produtoId: i.produto.id,
          quantidade: i.quantidade,
          observacao: i.observacao.trim() || null,
        })),
      },
    });

    estado.carrinho.clear();
    estado.identificacao = '';
    estado.observacao = '';
    estado.carrinhoAberto = false;
    salvarRascunho();
    estado.enviando = false;
    mostrarAviso(`Pedido #${pedido.numero} enviado para a cozinha.`);
    await Promise.allSettled([carregarCardapio(), carregarFila()]);
    estado.prontosVistos = new Set(estado.fila.filter((p) => p.status === 'Pronto').map((p) => p.id));
  } catch (erro) {
    estado.enviando = false;
    if (!erro.sessaoEncerrada) {
      estado.erroEnvio = erro.offline
        ? `${erro.message} Antes de enviar de novo, confira na aba Pedidos se ele já não foi enviado.`
        : erro.message;
      try { await carregarCardapio(); } catch { /* segue com os dados antigos */ }
    }
  }

  if (estado.token) renderizar();
}

/* ------------------------------------------------------------------ ações nos pedidos */

async function entregar(pedido) {
  try {
    await api(`/api/pedidos/${pedido.id}/status`, { metodo: 'POST', corpo: { status: 'Entregue' } });
    mostrarAviso(`Pedido #${pedido.numero} entregue.`);
    await carregarFila();
    renderizarListaPedidos();
    atualizarEmblema();
  } catch (erro) {
    tratar(erro);
  }
}

async function cancelar(pedido) {
  if (!confirm(`Cancelar o pedido #${pedido.numero} (${pedido.identificacao})?`)) return;

  try {
    await api(`/api/pedidos/${pedido.id}/cancelar`, { metodo: 'POST', corpo: { devolverEstoque: true } });
    mostrarAviso(`Pedido #${pedido.numero} cancelado.`);
    await Promise.all([carregarFila(), carregarCardapio()]);
    renderizarListaPedidos();
    renderizarListaProdutos();
    atualizarEmblema();
  } catch (erro) {
    tratar(erro);
  }
}

/* ------------------------------------------------------------------ telas */

function renderizar() {
  const app = document.getElementById('app');
  refs.ponto = null;
  refs.bannerOffline = null;

  if (!estado.token || estado.codigoDaUrl) {
    app.replaceChildren(telaParear());
    return;
  }

  app.replaceChildren(telaPrincipal());
  renderizarListaProdutos();
  renderizarListaPedidos();
  renderizarBarraCarrinho();
  atualizarEmblema();
  renderizarCarrinho();
}

function renderizarCarregando() {
  document.getElementById('app').replaceChildren(
    h('div', { class: 'centro' }, h('p', { class: 'vazio' }, 'Conectando ao servidor…')));
}

function telaParear() {
  const temCodigo = Boolean(estado.codigoDaUrl);

  const campoCodigo = h('input', {
    type: 'text', id: 'campo-codigo', placeholder: 'Ex.: K7M2QX9P', maxlength: 12,
    autocapitalize: 'characters', autocomplete: 'off', autocorrect: 'off', spellcheck: 'false',
  });
  const campoNome = h('input', {
    type: 'text', id: 'campo-nome', placeholder: 'Ex.: João', maxlength: 60, autocomplete: 'given-name',
  });
  const erro = h('div', { class: 'erro-caixa', role: 'alert', hidden: !estado.mensagemPareamento }, estado.mensagemPareamento);
  const botao = h('button', { class: 'botao primario grande', type: 'submit' }, 'Conectar');

  const formulario = h('form', {
    class: 'cartao pareamento',
    onsubmit: async (evento) => {
      evento.preventDefault();
      botao.disabled = true;
      erro.hidden = true;
      try {
        await parear(temCodigo ? estado.codigoDaUrl : campoCodigo.value, campoNome.value);
      } catch (e) {
        if (estado.token) {
          // Pareou, mas não conseguiu carregar os dados: entra mesmo assim e continua tentando.
          renderizar();
          iniciarAtualizacoes();
          return;
        }
        // Se o código do QR falhou, deixa o garçom digitar um código novo.
        estado.codigoDaUrl = null;
        estado.mensagemPareamento = e.message;
        renderizar();
      }
    },
  },
  h('h1', {}, 'Conectar este celular'),
  h('p', {}, temCodigo
    ? 'Falta só o seu nome para começar.'
    : 'Leia o QR Code que aparece no computador (tela Celular) com a câmera, ou digite o código que aparece embaixo dele.'),
  erro,
  temCodigo ? null : h('label', { class: 'campo' }, h('span', {}, 'Código do computador'), campoCodigo),
  h('label', { class: 'campo' }, h('span', {}, 'Seu nome'), campoNome),
  botao);

  return h('div', { class: 'centro' }, formulario);
}

function telaPrincipal() {
  refs.ponto = h('span', { class: 'ponto' + (estado.online ? '' : ' off') });
  refs.bannerOffline = h('div', { class: 'banner-offline', hidden: estado.online }, 'Sem conexão com o servidor. Tentando de novo…');
  refs.chips = h('div', { class: 'chips' });
  refs.produtos = h('div', {});
  refs.listaPedidos = h('div', {});
  refs.painelPedido = h('main', { class: estado.aba === 'pedido' ? '' : 'oculto' }, refs.chips, refs.produtos);
  refs.painelPedidos = h('main', { class: estado.aba === 'pedidos' ? '' : 'oculto' }, refs.listaPedidos);
  refs.barra = h('div', { class: 'barra-carrinho', hidden: true });
  refs.emblema = h('span', { class: 'emblema', hidden: true });
  refs.abaPedido = h('button', { onclick: () => trocarAba('pedido') }, 'Novo pedido');
  refs.abaPedidos = h('button', { onclick: () => trocarAba('pedidos') }, 'Pedidos', refs.emblema);
  refs.carrinho = h('div', { class: 'sobreposicao', hidden: true });
  marcarAbaAtiva();

  return h('div', { style: 'display:contents' },
    h('header', { class: 'topo' },
      h('div', { class: 'topo-titulo' }, 'PDV Lanchonete'),
      h('div', { class: 'topo-info' }, refs.ponto, estado.nome)),
    refs.bannerOffline,
    refs.painelPedido,
    refs.painelPedidos,
    refs.barra,
    h('nav', { class: 'nav' }, refs.abaPedido, refs.abaPedidos),
    refs.carrinho);
}

function trocarAba(aba) {
  estado.aba = aba;
  refs.painelPedido.className = aba === 'pedido' ? '' : 'oculto';
  refs.painelPedidos.className = aba === 'pedidos' ? '' : 'oculto';
  marcarAbaAtiva();
  renderizarBarraCarrinho();
  window.scrollTo(0, 0);
  if (aba === 'pedidos') {
    carregarFila().then(renderizarListaPedidos).then(atualizarEmblema).catch(() => {});
  }
}

function marcarAbaAtiva() {
  refs.abaPedido.className = estado.aba === 'pedido' ? 'ativa' : '';
  refs.abaPedidos.className = estado.aba === 'pedidos' ? 'ativa' : '';
}

/* ---- Novo pedido ---- */

function categoriasDoCardapio() {
  const nomes = [...new Set(estado.produtos.map((p) => p.categoria || 'Outros'))];
  return ['Todos', ...nomes];
}

function renderizarListaProdutos() {
  if (!refs.produtos) return;

  const categorias = categoriasDoCardapio();
  if (!categorias.includes(estado.categoria)) estado.categoria = 'Todos';

  refs.chips.replaceChildren(...categorias.map((nome) =>
    h('button', {
      class: 'chip-filtro' + (nome === estado.categoria ? ' ativo' : ''),
      onclick: () => { estado.categoria = nome; renderizarListaProdutos(); },
    }, nome)));

  const visiveis = estado.produtos.filter((p) =>
    estado.categoria === 'Todos' || (p.categoria || 'Outros') === estado.categoria);

  if (visiveis.length === 0) {
    refs.produtos.replaceChildren(h('p', { class: 'vazio' },
      estado.produtos.length === 0
        ? 'O cardápio está vazio. Cadastre os produtos no computador.'
        : 'Nenhum produto nesta categoria.'));
    return;
  }

  refs.produtos.replaceChildren(...visiveis.map(cartaoProduto));
}

function cartaoProduto(produto) {
  const esgotado = produto.disponivel === 0;
  const aviso = esgotado
    ? 'Esgotado'
    : produto.disponivel !== null && produto.disponivel <= 5
      ? `Restam ${produto.disponivel}`
      : null;

  return h('div', { class: 'cartao produto' + (esgotado ? ' esgotado' : '') },
    h('div', {},
      h('div', { class: 'produto-nome' }, produto.nome),
      h('div', { class: 'produto-preco' }, dinheiro(produto.preco)),
      aviso ? h('div', { class: 'produto-aviso' }, aviso) : null),
    esgotado ? null : controleDeQuantidade(produto));
}

function controleDeQuantidade(produto) {
  const quantidade = quantidadeNoCarrinho(produto.id);

  if (quantidade === 0) {
    return h('button', { class: 'botao primario', onclick: () => alterarQuantidade(produto, 1) }, 'Adicionar');
  }

  return h('div', { class: 'quantidade' },
    h('button', { class: 'botao redondo', 'aria-label': 'Tirar um', onclick: () => alterarQuantidade(produto, -1) }, '−'),
    h('span', { class: 'quantidade-valor' }, quantidade),
    h('button', { class: 'botao redondo', 'aria-label': 'Adicionar mais um', onclick: () => alterarQuantidade(produto, 1) }, '+'));
}

function renderizarBarraCarrinho() {
  if (!refs.barra) return;

  const { itens, total } = totais();
  refs.barra.hidden = !(estado.aba === 'pedido' && itens > 0);
  refs.barra.replaceChildren(
    h('button', { class: 'botao primario grande', onclick: abrirCarrinho },
      `Ver pedido · ${itens} ${itens === 1 ? 'item' : 'itens'} · ${dinheiro(total)}`));
}

function renderizarCarrinho() {
  if (!refs.carrinho) return;

  refs.carrinho.hidden = !estado.carrinhoAberto;
  refs.carrinho.replaceChildren();
  if (!estado.carrinhoAberto) return;

  const { itens, total } = totais();

  refs.carrinho.append(
    h('div', { class: 'folha' },
      h('div', { class: 'folha-topo' },
        h('h2', {}, 'Resumo do pedido'),
        h('button', { class: 'botao texto', onclick: fecharCarrinho }, 'Voltar')),
      h('div', { class: 'folha-corpo' },
        [...estado.carrinho.values()].map(linhaDoCarrinho),
        h('label', { class: 'campo' },
          h('span', {}, 'Mesa ou nome do cliente'),
          h('input', {
            type: 'text', id: 'campo-identificacao', value: estado.identificacao, maxlength: 60,
            placeholder: 'Ex.: Mesa 4', autocomplete: 'off',
            oninput: (e) => { estado.identificacao = e.target.value; salvarRascunho(); },
          })),
        h('label', { class: 'campo' },
          h('span', {}, 'Observação do pedido (opcional)'),
          h('input', {
            type: 'text', value: estado.observacao, maxlength: 200, autocomplete: 'off',
            oninput: (e) => { estado.observacao = e.target.value; salvarRascunho(); },
          })),
        estado.erroEnvio ? h('div', { class: 'erro-caixa', role: 'alert' }, estado.erroEnvio) : null),
      h('div', { class: 'folha-rodape' },
        h('div', { class: 'total' },
          h('span', {}, `${itens} ${itens === 1 ? 'item' : 'itens'}`),
          h('strong', {}, dinheiro(total))),
        h('button', {
          class: 'botao primario grande', style: 'width:100%',
          disabled: estado.enviando || itens === 0, onclick: enviarPedido,
        }, estado.enviando ? 'Enviando…' : 'Enviar pedido'))));
}

function linhaDoCarrinho(item) {
  return h('div', { class: 'cartao' },
    h('div', { class: 'linha-item' },
      h('div', {},
        h('div', { class: 'linha-item-nome' }, item.produto.nome),
        h('div', { class: 'linha-item-subtotal' }, dinheiro(item.produto.preco * item.quantidade))),
      h('div', { class: 'quantidade' },
        h('button', { class: 'botao redondo', 'aria-label': 'Tirar um', onclick: () => alterarQuantidade(item.produto, -1) }, '−'),
        h('span', { class: 'quantidade-valor' }, item.quantidade),
        h('button', { class: 'botao redondo', 'aria-label': 'Adicionar mais um', onclick: () => alterarQuantidade(item.produto, 1) }, '+'))),
    h('input', {
      type: 'text', class: 'pequeno', value: item.observacao, maxlength: 120, autocomplete: 'off',
      placeholder: 'Observação do item (ex.: sem cebola)',
      oninput: (e) => { item.observacao = e.target.value; salvarRascunho(); },
    }));
}

/* ---- Pedidos em andamento ---- */

function renderizarListaPedidos() {
  if (!refs.listaPedidos) return;

  if (estado.fila.length === 0) {
    refs.listaPedidos.replaceChildren(h('p', { class: 'vazio' }, 'Nenhum pedido em andamento.'));
    return;
  }

  // Prontos primeiro: são os que o garçom precisa levar.
  const ordenados = [...estado.fila].sort((a, b) =>
    (b.status === 'Pronto') - (a.status === 'Pronto') || a.numero - b.numero);

  refs.listaPedidos.replaceChildren(...ordenados.map(cartaoPedido));
}

function atualizarEmblema() {
  if (!refs.emblema) return;

  const prontos = estado.fila.filter((p) => p.status === 'Pronto').length;
  refs.emblema.hidden = prontos === 0;
  refs.emblema.textContent = String(prontos);
}

function cartaoPedido(pedido) {
  const situacao = STATUS[pedido.status] ?? { texto: pedido.status, classe: '' };

  return h('div', { class: `cartao pedido ${situacao.classe}` },
    h('div', { class: 'pedido-topo' },
      h('div', {}, h('strong', {}, `#${pedido.numero}`), ` · ${pedido.identificacao}`),
      h('span', { class: `chip ${situacao.classe}` }, situacao.texto)),
    h('ul', { class: 'pedido-itens' },
      pedido.itens.map((i) => h('li', {}, `${i.quantidade}× ${i.nome}`, i.observacao ? h('em', {}, ` (${i.observacao})`) : null))),
    pedido.observacao ? h('div', { class: 'pedido-obs' }, pedido.observacao) : null,
    h('div', { class: 'pedido-rodape' },
      h('span', { class: 'sutil' }, `${haQuantoTempo(pedido.criadoEm)} · ${dinheiro(pedido.total)}`),
      acaoDoPedido(pedido)));
}

function acaoDoPedido(pedido) {
  if (pedido.status === 'Pronto') {
    return h('button', { class: 'botao primario', onclick: () => entregar(pedido) }, 'Entregue ao cliente');
  }
  if (pedido.status === 'APreparar') {
    return h('button', { class: 'botao perigo', onclick: () => cancelar(pedido) }, 'Cancelar');
  }
  return null;
}

/* ------------------------------------------------------------------ início */

async function iniciar() {
  const salvo = lerArmazenado(CHAVE_DISPOSITIVO);
  if (salvo?.token) {
    estado.token = salvo.token;
    estado.nome = salvo.nome;
  }

  const achado = location.hash.match(/^#\/parear\/([A-Za-z0-9-]+)/);
  if (achado) estado.codigoDaUrl = achado[1].toUpperCase();

  if (estado.token && !estado.codigoDaUrl) {
    renderizarCarregando();
    try {
      await entrar();
    } catch (erro) {
      if (erro.sessaoEncerrada) return;
      // Sem rede agora: mostra a tela e continua tentando em segundo plano.
      renderizar();
      iniciarAtualizacoes();
      tratar(erro);
    }
    return;
  }

  renderizar();
}

iniciar();
