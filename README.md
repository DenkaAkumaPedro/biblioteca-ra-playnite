# Biblioteca RA no Playnite

Addon para o **Playnite** que importa o catálogo do
[RetroAchievements](https://retroachievements.org) para a biblioteca do
usuário, detectando quais ROMs você já tem no PC por hash MD5.

> **Versão de teste: 0.0.1** — funcionalidade básica de importação. Veja
> [PLANO.md](PLANO.md) para o escopo completo e [REGRAS.md](REGRAS.md) para
> as regras de operação deste projeto.

## O que faz (v0.0.1)

- Importa o catálogo completo do RA (sistemas + jogos) como jogos **não
  instalados** na biblioteca do Playnite
- Preenche: nome, plataforma, ícone e link para a página do jogo no RA
- Detecta ROMs instaladas: varre as pastas que você indicar, calcula o MD5
  de cada arquivo e casa com os hashes oficiais do RA — match exato, nada de
  adivinhar por nome de arquivo
- Jogo detectado como instalado ganha `GameAction` apontando para a ROM
- Cache local do catálogo com validade configurável (a API do RA exige cache
  agressivo)
- Web API Key guardada só nas suas configurações locais (`ExtensionsData`)

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
3. Adiciona as **pastas de ROM** do seu PC
4. Ajuste as **extensões de arquivo** se quiser (padrão cobre os formats
   comuns de retro)
5. Clique em **Testar conexão** para confirmar a chave
6. Volte à biblioteca e use **Atualizar biblioteca** do Playnite — o
   catálogo será baixado e cacheado na primeira vez

### Botões das configurações

| Botão | Função |
|-------|--------|
| Testar conexão | Faz 1 chamada à API e diz se a chave funciona |
| Forçar atualização do catálogo | Baixa o catálogo inteiro na hora, ignorando o cache |
| Limpar cache | Apaga o cache local (próxima atualização baixa de novo) |

## Desenvolver

### Requisitos

- .NET SDK (build `net462`; o pacote de reference assemblies já vem no cache)
- Playnite 10 com `Toolbox.exe` (veio junto com o Playnite)

### Comandos

```powershell
# build
dotnet build "Modelo Teste 01/playnite-plugin/BibliotecaRA.slnx" -c Release

# empacotar (gera o .pext dentro de "Modelo Teste 01")
& "C:\caminho\do\Playnite\Toolbox.exe" pack `
  "Modelo Teste 01/playnite-plugin/bin/Release/net462" `
  "Modelo Teste 01"

# validar o manifest do addon browser
& "C:\caminho\do\Playnite\Toolbox.exe" verify addon addon.yaml
```

### Estrutura

```
├── addon.yaml                  manifest exibido no navegador de addons do Playnite
├── manifests/installer.yaml    manifesto de instalação (aponta para a release)
├── .github/workflows/build.yml CI: build a cada push
└── Modelo Teste 01/            código-fonte do addon (versão de teste)
    └── playnite-plugin/        projeto C# (net462, PlayniteSDK 6.17.0)
        ├── BibliotecaRA.cs             LibraryPlugin + GetGames()
        ├── BibliotecaRASettings*.cs/xaml  configurações
        ├── Models/                     modelos da API do RA + settings
        ├── Services/                   ApiClient, catálogo+cache, hash MD5
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

## Limitações conhecidas (0.0.1)

- Gênero, desenvolvedora e editora não vêm: a `API_GetGameList` não os traz
  (Fase 6, metadados detalhados, fica para depois)
- Plataformas usam o nome do RA como está (ex.: "Mega Drive") — renomeie no
  Playnite se preferir outro nome
- Mensagens de status/erro das configurações estão em português; os rótulos
  têm en_US e pt_BR
- A Web API Key do RA pode ser bloqueada por rate limit se você forçar
  atualizações repetidas em pouco tempo

## Links

- Repo: <https://github.com/DenkaAkumaPedro/biblioteca-ra-playnite>
- API do RA: <https://api-docs.retroachievements.org/>
- SDK do Playnite: <https://api.playnite.link/docs/tutorials/extensions/plugins.html>
