# Biblioteca RA no Playnite

Addon para o **Playnite** que importa o catálogo do
[RetroAchievements](https://retroachievements.org) para a biblioteca do
usuário, detectando quais ROMs você já tem no PC por hash MD5.

> **Versão de teste: 0.0.1** — funcionalidade básica de importação. Veja
> [PLANO.md](PLANO.md) para o escopo completo e [REGRAS.md](REGRAS.md) para
> as regras de operação deste projeto.

## O que faz (Modelo Teste 02)

- Importa os jogos da **sua conta do RetroAchievements** (jogos com progresso
  no site) para a biblioteca do Playnite
- Detecta ROMs instaladas: varre as pastas que você indicar, calcula o MD5
  de cada arquivo e casa com os hashes oficiais do RA — match exato, nada de
  adivinhar por nome de arquivo
- Jogos detectados no PC que **não** estão na sua conta também entram no
  import (união conta ∪ detectados)
- O catálogo completo do RA **não** é importado — só é baixado como cache
  interno de hashes, e apenas se a detecção de ROMs estiver ligada
- Preenche: nome, plataforma, ícone e link para a página do jogo no RA
- Jogo detectado como instalado ganha `GameAction` apontando para a ROM
- Cache local com validade configurável (a API do RA exige cache agressivo)
- Web API Key e usuário guardados só nas configurações locais (`ExtensionsData`)

## Instalar

1. Baixe o `.pext` na [release v0.0.1](https://github.com/DenkaAkumaPedro/biblioteca-ra-playnite/releases/tag/v0.0.1)
2. No Playnite: **Configurações → Add-ons → Instalar do arquivo** e escolha
   o arquivo baixado
3. Reinicie o Playnite quando solicitado

## Usar

1. Abra **Configurações → Add-ons → Biblioteca RA → Configurar**
2. Cole sua **Web API Key**: no site do RA, vá em
   [Control Panel → Keys](https://retroachievements.org/controlpanel.php)
   e copie a chave (ela nunca é gravada no código nem enviada a terceiros
   além do próprio RA)
3. Informe seu **usuário do RetroAchievements** (nome no site, não a chave)
4. Adiciona as **pastas de ROM** do seu PC
5. Ajuste as **extensões de arquivo** se quiser (padrão cobre os formats
   comuns de retro)
6. Clique em **Testar conexão** para confirmar chave e usuário
7. Volte à biblioteca e use **Atualizar biblioteca** do Playnite — os jogos
   da sua conta serão baixados e cacheados na primeira vez

### Botões das configurações

| Botão | Função |
|-------|--------|
| Testar conexão | Valida a Web API Key e (se informado) o usuário |
| Forçar atualização da biblioteca | Baixa de novo os jogos da conta; com detecção ligada, atualiza também o cache de hashes |
| Limpar cache | Apaga o cache local (próxima atualização baixa de novo) |

## Desenvolver

### Requisitos

- .NET SDK (build `net462`; o pacote de reference assemblies já vem no cache)
- Playnite 10 com `Toolbox.exe` (veio junto com o Playnite)

### Comandos

```powershell
# build
dotnet build "Modelo Teste 02/playnite-plugin/BibliotecaRA.slnx" -c Release

# empacotar (gera o .pext dentro de "Modelo Teste 02")
& "C:\caminho\do\Playnite\Toolbox.exe" pack `
  "Modelo Teste 02/playnite-plugin/bin/Release/net462" `
  "Modelo Teste 02"

# validar o manifest do addon browser
& "C:\caminho\do\Playnite\Toolbox.exe" verify addon addon.yaml
```

### Estrutura

```
├── addon.yaml                  manifest exibido no navegador de addons do Playnite
├── manifests/installer.yaml    manifesto de instalação (aponta para a release)
├── .github/workflows/build.yml CI: build do Modelo Teste 02 a cada push
├── Notas.md                    bugs relatados no teste da 0.0.1
├── ESTADO.md                   memória de estado entre sessões
├── Modelo Teste 01/            referência congelada (v0.0.1)
└── Modelo Teste 02/            versão ativa de desenvolvimento
    └── playnite-plugin/        projeto C# (net462, PlayniteSDK 6.17.0)
        ├── BibliotecaRA.cs             LibraryPlugin + GetGames()
        ├── BibliotecaRASettings*.cs/xaml  configurações (key + usuário do RA)
        ├── Models/                     modelos da API do RA + settings
        ├── Services/                   ApiClient, biblioteca da conta, hash MD5
        └── Localization/               en_US.xaml, pt_BR.xaml
```

### Publicação (automático)

- **Push automático**: sempre que há mudanças nesta pasta, elas são commitadas
  e enviadas ao GitHub (autorizado pelo usuário até pedir o contrário)
- **CI**: o GitHub Actions compila o projeto a cada push
- **Release**: nova versão = bump no `extension.yaml` + `manifests/installer.yaml`
  + `gh release create` com o `.pext` anexado

### Regras do SDK (não violar)

- Referenciar só `PlayniteSDK` — nunca `Playnite.dll` / `Playnite.Common`
- `Newtonsoft.Json` fixo em **10.0.3** (a versão que o Playnite usa)
- Dado de usuário só em `GetPluginUserDataPath()` — nunca na pasta da extensão

## Limitações conhecidas (Modelo Teste 02)

- Gênero, desenvolvedora e editora não vêm da lista da conta (Fase 6,
  metadados detalhados, fica para depois)
- Plataformas usam o nome do RA como está (ex.: "Mega Drive") — renomeie no
  Playnite se preferir outro nome
- Mensagens de status/erro das configurações estão em português; os rótulos
  têm en_US e pt_BR
- A Web API Key do RA pode ser bloqueada por rate limit se você forçar
  atualizações repetidas em pouco tempo
- Com detecção de ROMs ligada, a 1ª sincronização baixa o catálogo de
  hashes inteiro (necessário — a API não busca por MD5); depois usa cache
- Progresso do sync não aparece na UI do Playnite (só no log) — adiado
- Cancelar no meio do download não retoma de onde parou — adiado

## Links

- Repo: <https://github.com/DenkaAkumaPedro/biblioteca-ra-playnite>
- API do RA: <https://api-docs.retroachievements.org/>
- SDK do Playnite: <https://api.playnite.link/docs/tutorials/extensions/plugins.html>
