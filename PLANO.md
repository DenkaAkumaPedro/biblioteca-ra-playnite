# Biblioteca RA — Plano de Projeto

Addon para o Playnite que importa o catálogo do **RetroAchievements** (retroachievements.org)
para a biblioteca do usuário.

---

## 1. Escopo

### Dentro do escopo (v1)

| # | Funcionalidade | Descrição |
|---|----------------|-----------|
| 1 | Importar jogos da conta | Só os jogos da **conta RA do usuário** (`API_GetUserCompletionProgress`) + jogos detectados por hash no PC. Catálogo completo só como cache interno de hashes — nunca importado inteiro |
| 2 | Metadados | Nome, plataforma, ícone, capa, gênero, desenvolvedora, editora, ano, links |
| 3 | Detectar instalados | Casar os hashes MD5 do RA com os arquivos de ROM presentes no PC do usuário |
| 4 | Configuração | Campo para a Web API Key, **usuário do RA**, pastas de ROM a varrer, botões de ação |
| 5 | Atividade e tempo de jogo | `LastActivity` (última vez jogado, só avança), `PlayCount` (= 1 com atividade) e `Playtime` = **max(local, RA)** por jogo; escrita no `OnLibraryUpdated` com caches próprios (`activity_*.json`); checkboxes de sync |
| 6 | Pastas dos jogos | Botão "Importar pastas dos jogos": coleta diretórios de `Game.Roms[].Path` + `GameActions` Type File dos jogos do Playnite para a lista de pastas |

### Fora do escopo (backlog para depois)

- Integração com emuladores (abrir jogo pelo emulador via `GameAction`)
- Download de ROMs / links de MEGA
- Conquistas, badges, pontos, leaderboards do RA
- Sincronização de progresso de jogos
- Import/export em JSON
- Suporte oficial a Playnite 11 (SDK novo, ainda alpha)

---

## 2. Decisões técnicas

### Stack

| Item | Valor | Motivo |
|------|-------|--------|
| Linguagem | C# | Único que o SDK do Playnite carrega |
| Target framework | `net462` (.NET Framework 4.6.2) | Exigido pelo Playnite 10 |
| NuGet do SDK | `PlayniteSDK` **6.17.0** | É a versão que o Toolbox gera. Conservadora e compatível com toda a linha 10.x |
| Base da classe | `Playnite.SDK.Plugins.LibraryPlugin` | É a API de "biblioteca" do Playnite |
| `Type` no manifest | `GameLibrary` | Cria a entrada própria em *Biblioteca → Libraries* |
| `Module` no manifest | `BibliotecaRA.dll` | Precisa ser um assembly .NET |
| JSON | `Newtonsoft.Json` **10.0.3** | Versão que o Playnite referencia. Versão diferente = assembly duplicado = plugin não carrega |

> **Regra do SDK:** só referenciar `PlayniteSDK`. Nunca `Playnite.dll`,
> `Playnite.Common`, nem `Playnite.Toolkit`. O Playnite **recusa carregar**
> plugins que referenciem assemblies não-SDK. Se precisar de código de lá,
> dar "Add as link" no arquivo-fonte para dentro do projeto.

### Por que o plano NÃO segue o padrão dos addons irmãos

Os projetos `GameCard`, `AniList Com Horas` e `Gplay exophse` (na pasta
`Playnite Adons`) usam:

```yaml
Type: GenericPlugin
Module: app.js
```

**Esse padrão não funciona.** O Playnite carrega assemblies .NET, não módulos
JavaScript — não existe um runtime de Node dentro do Playnite. Um `.pext` com
`Module: app.js` não carrega.

Além disso, `PlaynitePlugin`, `GetMenus()`, `OnInstalled()` e `OnUninstalled()`
**não existem** na API do Playnite 10 — são de outra geração de addons.

Este projeto segue o template `CustomLibraryPlugin` do Toolbox, que é o
caminho suportado e documentado. Os addons irmãos podem ser reescritos depois,
separadamente.

---

## 3. A API do RetroAchievements

O RA tem API pública, documentada e oficial. **Não é scraping** — não há
parsing de HTML, não há CAPTCHA, não há risco de quebrar quando o layout muda.

- Documentação: <https://api-docs.retroachievements.org/>
- Bundle para LLM: <https://api-docs.retroachievements.org/llms.txt>
- Base: `https://retroachievements.org/API/`

### Autenticação

Não é login com senha. É uma **Web API Key**:

1. Logado no site, abrir <https://retroachievements.org/controlpanel.php>
2. Seção "Keys" → copiar a **Web API Key**
3. Toda chamada envia `?y=<chave>`

A chave fica nas settings do addon (salva em `ExtensionsData`), **nunca** no
código. Documentação: `/getting-started`.

### Endpoints que a v1 usa

| Endpoint | Parâmetros | Uso |
|----------|-----------|-----|
| `API_GetUserCompletionProgress.php` | `y`, `u=<usuário>`, `c`, `o` | **Jogos da conta do usuário** (fonte do import) + fallback barato de atividade (`MostRecentAwardedDate`) |
| `API_GetConsoleIDs.php` | `y`, `a=1` (só ativos), `g=1` (só jogos) | Lista de sistemas (cache de hashes) |
| `API_GetGameList.php` | `y`, `i=<consoleId>`, `h=1`, `f`, `o`, `c` | Catálogo de um sistema + hashes (só cache interno p/ detecção) |
| `API_GetGame.php` | `y`, `i=<gameId>` | Detalhes de um jogo |
| `API_GetGameHashes.php` | `y`, `i=<gameId>` | Lista de ROMs compatíveis |
| `API_GetUserRecentlyPlayedGames.php` | `y`, `u`, `c` (máx. 50), `o` | **Última vez jogado** por jogo (`LastPlayed` "yyyy-MM-dd HH:mm:ss" em UTC) → `LastActivity` |
| `API_GetGameInfoAndUserProgress.php` | `y`, `g=<gameId>`, `u` | **Tempo de jogo** do usuário no jogo (`UserTotalPlaytime` em **segundos**; 0 = nunca jogou) — 1 requisição por jogo |

### Formato de `API_GetGameList.php` (confirmado na documentação)

```json
[
  {
    "Title": "Advanced Busterhawk: Gley Lancer",
    "ID": 3684,
    "ConsoleID": 1,
    "ConsoleName": "Mega Drive",
    "ImageIcon": "/Images/020895.png",
    "NumAchievements": 44,
    "NumLeaderboards": 33,
    "Points": 595,
    "DateModified": "2022-11-20 03:44:12",
    "ForumTopicID": 1936,
    "Hashes": ["8bd4a97783cda077c342173df0a9b51e", "a13ab653a20fb383337fab1e52ddb0df"]
  }
]
```

### O achado que define a arquitetura

O RA devolve, junto de cada jogo, **os hashes MD5 de todas as ROMs compatíveis**
com ele. Isso é a chave que resolve dois requisitos com uma peça só:

- **Importar catálogo** → cada jogo vira um `GameMetadata` com `GameId` = ID do RA
- **Detectar instalados** → hashear os arquivos de ROM do usuário com MD5 e
  casar com a tabela `Hashes[]`

Não é preciso adivinhar nome de arquivo, nem confiar em pasta. O match é exato.

`MD5` é nativo do .NET (`System.Security.Cryptography.MD5`) — **nenhuma
dependência extra** para a detecção.

### Limitações honestas dos dados

- `Publisher`, `Developer`, `Genre` vêm **vazios** na maioria dos jogos
  (ex.: o Sonic the Hedgehog no exemplo da própria doc está vazio). O catálogo
  do RA é focado em conquistas, não em metadados ricos.
- `Released` vem como `ReleasedAtGranularity` (`day`, `month`, `year`) — usar
  para montar o `ReleaseDate` com a precisão correta.
- Os caminhos de imagem (`/Images/020895.png`) são **relativos ao site**:
  prefixar `https://retroachievements.org`.
- `ImageBoxArt` só existe em `API_GetGame.php`, não no list.

---

## 4. Fluxo de dados

```
1. Settings: usuário cola a Web API Key + nome de usuário do RA + pastas de ROM
                    |
2. API_GetUserCompletionProgress.php (u=<usuário>, paginado)
   ──> jogos da conta do usuário  ── importados SEMPRE
                    |
3. Se "Detectar ROMs" ligado:
   API_GetConsoleIDs + API_GetGameList(h=1) paginado
   ──> cache de hashes (GetPluginUserDataPath)  ── NUNCA importado inteiro
                    |
4. HashService: varre pastas, MD5, casa com Hashes[]
   ──> jogos detectados também entram no import (união)
                    |
5. Montar GameMetadata (conta ∪ detectados):
      GameId      = ID do RA          (chave de reconciliação)
      Name        = Title
      Platforms   = ConsoleName
      Icon        = https://retroachievements.org + ImageIcon
      Links       = https://retroachievements.org/game/<ID>
                    |
6. O Playnite chama GetGames() e insere cada GameMetadata na biblioteca
```

### Paginação e volume

O RA tem ~40 mil jogos. `API_GetGameList` aceita `o` (offset) e `c` (contagem),
então o catálogo de cada sistema é buscado em páginas, o que também controla o
tamanho da resposta em memória.

`API_GetGame.php` (detalhes) é **1 request por jogo**. 40 mil requests é
inviável e o RA vai bloquear. Por isso:

- **Import padrão** → usa só o `GetGameList`. Já traz nome, sistema, ícone,
  links e hashes. Import em segundos.
- **Metadados detalhados** → passo opcional em background, com rate limit
  agressivo e cache. O usuário escolhe se quer gastar isso.

### Cache — obrigatório

A documentação do RA diz textualmente: *"Consider aggressively caching this
endpoint's response... Frequent calls to this endpoint may prompt us to look
into your bandwidth usage."*

Então:

- Catálogo e hashes ficam em cache local (JSON em `GetPluginUserDataPath()`)
- Só re-consulta quando o cache é velho demais (configurável) ou quando o
  usuário manda forçar
- `GetGames()` lê do cache; nunca re-baixa a lista inteira do zero

---

## 5. Mapeamento RA → `GameMetadata`

| Campo RA | Campo `GameMetadata` | Notas |
|----------|----------------------|-------|
| `ID` | `GameId` | **Obrigatório.** String do ID numérico. É a chave de reconciliação — não pode mudar entre versões |
| `Title` | `Name` | |
| `ConsoleName` | `Platforms` | Criar `MetadataProperty` se não existir. **Decisão v0.0.1:** usar o nome do RA como está (ex.: "Mega Drive") — o Playnite não tem lista canônica de plataformas, então qualquer tabela de tradução seria chute; o usuário renomeia no Playnite se quiser |
| `ImageIcon` | `Icon` | Prefixar URL base |
| `ImageBoxArt` (só em `GetGame.php`) | `CoverImage` | Só no passo de metadados detalhados |
| `Genre` | `Genres` | Frequentemente vazio no RA |
| `Developer` | `Developers` | Frequentemente vazio |
| `Publisher` | `Publishers` | Frequentemente vazio |
| `Released` + `ReleasedAtGranularity` | `ReleaseDate` | Respeitar a granularidade |
| `ID` | `Links` | `https://retroachievements.org/game/<ID>` |
| `Hashes[]` | — | Não vai direto pro metadata. Fica em tabela de detecção |
| — | `IsInstalled` | Resultado da detecção por hash |
| — | `GameActions` | Só se `IsInstalled`; sem `GameAction` o jogo aparece instalado mas não roda |

### Regras do SDK que valem para todo o resto do projeto

- `LibraryGetGamesArgs.CancelToken` tem setter **`internal`**. Só ler, nunca
  construir. Checar antes de cada requisição HTTP.
- O SDK **não é thread-safe**. Se algo tocar na UI, passar por
  `PlayniteApi.MainView.UIDispatcher`.
- `GameDatabase` faz o merge por `GameId` + `PluginId`. É o que faz o
  reimport atualizar em vez de duplicar.
- `PlayniteApi.Database.ImportGame(...)` **não existe** na API pública. Com o
  caminho padrão (`GetGames`) não é preciso — o Playnite insere.

---

## 6. Estrutura de pastas

A raiz do projeto é também a raiz do repositório git
(`github.com/DenkaAkumaPedro/biblioteca-ra-playnite`). O addon em
desenvolvimento fica dentro de `Modelo MK 1\` (o `Modelo Base` é a
referência congelada para sempre do modelo mais simples; o antigo
`Modelo Teste 01` foi deletado — o git guarda o histórico):

```
Biblioteca RA no Playnite\
├── PLANO.md                  este arquivo
├── REGRAS.md                 regras de operação e escopo
├── README.md                 instalar / usar / desenvolver (exibido no GitHub)
├── Notas.md                  bugs relatados nos testes
├── ESTADO.md                 memória de estado entre sessões
├── .gitignore
├── addon.yaml                manifest do addon browser (verify addon usa este)
├── manifests\
│   └── installer.yaml        manifesto de instalação → aponta para a release
├── .github\workflows\
│   └── build.yml             CI: build do Modelo MK 1 a cada push
├── Modelo Base\              referência CONGELADA para sempre — NÃO ALTERAR
│   ├── PLANO.md, REGRAS.md   snapshot (não sincronizar)
│   ├── extension.yaml        cópia de conferência
│   ├── BibliotecaRA_..._0_0_1.pext
│   ├── BibliotecaRA_..._0_0_2.pext
│   └── playnite-plugin\      fonte da v0.0.2 (modelo mais simples)
└── Modelo MK 1\              versão ativa de desenvolvimento
    ├── PLANO.md, REGRAS.md   cópias (snapshot; fonte de verdade é a raiz)
    ├── extension.yaml        cópia para conferência; o de verdade sai do build
    └── playnite-plugin\
        ├── BibliotecaRA.csproj
        ├── BibliotecaRA.slnx
        ├── extension.yaml        manifest — Type: GameLibrary
        ├── icon.png
        ├── BibliotecaRA.cs       a LibraryPlugin
        ├── BibliotecaRASettings.cs           settings (modelo em Models/RaSettings.cs)
        ├── BibliotecaRASettingsView.xaml/.cs view das configurações
        ├── Models\
        │   ├── RaApiModels.cs    deserialização das respostas do RA
        │   └── RaSettings.cs     settings do addon
        ├── Services\
        │   ├── ApiClient.cs      HttpClient, API key, retry, rate limit
        │   ├── CatalogService.cs biblioteca da conta + cache de hashes
        │   └── HashService.cs    MD5 + varredura de pastas
        └── Localization\
            ├── pt_BR.xaml
            └── en_US.xaml
```

Fora do que entra no `.pext`, na raiz do projeto:

```
Referencias\    trechos da doc da API do RA copiados para consulta offline
Notas\          decisões de projeto e problemas encontrados
```

### Sobre escrever dados em disco

> A instalação e atualização de um addon **substitui o diretório inteiro** da
> extensão. Qualquer arquivo que não faça parte do pacote é perdido.

Então: **nada** de gravar dentro da pasta da extensão. Tudo que for dado de
usuário (settings, cache do catálogo) vai para `GetPluginUserDataPath()`, que
resolve para `%AppData%\Playnite\ExtensionsData\<PluginId>\`
(ou a pasta do app, se o Playnite for portátil).

---

## 7. Fases de implementação

### Fase 0 — Esqueleto
- `Toolbox.exe new LibraryPlugin "Biblioteca RA" "<pasta do projeto>"`
- Ajustar `extension.yaml`, `Newtonsoft.Json` 10.0.3, `Localization/`
- Rodar o `.pext` vazio no Playnite e confirmar que carrega antes de escrever lógica

### Fase 1 — `ApiClient`
- `HttpClient` com `Web API Key` via setting
- Base URL, timeout, `User-Agent` identificável
- Detectar erro `{"success": false}` (chave inválida, rate limit, erro do servidor)
- Retry com backoff e rate limit entre chamadas

### Fase 2 — Modelos
- Deserializar as respostas do RA
- Casar os nomes JSON exatos (`Title`, `ConsoleID`, `ImageIcon`, `Hashes`…)

### Fase 3 — Settings
- Campo da API key
- Pastas de ROM a varrer
- TTL do cache, botão "forçar atualização", botão "limpar cache"
- **Teste de conexão** na tela de settings

### Fase 4 — `GetGames()` + cache
- `API_GetConsoleIDs` → sistemas
- `API_GetGameList` paginado, com cache em disco
- Montar `GameMetadata`, respeitando o `CancelToken`
- Respeitar `args.CancelToken` entre páginas para o Playnite poder cancelar

### Fase 5 — `HashService`
- Índice `MD5 → lista de GameId`s, montado do catálogo em cache
- Varrer as pastas configuradas recursivamente, por extensão
- MD5 por streaming, sem carregar o arquivo inteiro na memória
- Achar o `Game` já importado e marcar `IsInstalled` + `GameActions`
- Usar `BufferedUpdate()` na escrita em massa; `UIDispatcher` se tocar na UI

### Fase 6 — Metadados detalhados (opcional)
- `API_GetGame.php` em background, com rate limit
- Só se o usuário ativar; padrão é desligado

### Fase 7 — Empacotar e testar
- `Toolbox.exe pack "<bin\Release>" "<destino>"` → `BibliotecaRA_*_0_0_1.pext`
  (o Toolbox exclui `Playnite.SDK.dll` e `Newtonsoft.Json.dll` do pacote
  automaticamente — o host fornece ambos)
- `Toolbox.exe verify addon addon.yaml` — atenção: o `verify addon` valida o
  **manifest do addon browser** (`addon.yaml`), não o `extension.yaml`
- Publicar a `.pext` como release no GitHub (ver seção 10)
- Instalação no Playnite fica para o usuário (fora do escopo de escrita)

### Fora do caminho crítico

Atalho para desenvolver sem empacotar a cada vez:
*Playnite → Settings → For developers → External extensions* apontando para `bin\Debug\`.

Para depurar: *Debug → Attach to Process* em `Playnite.DesktopApp.exe`.

---

## 8. Riscos e o que fazer

| Risco | Mitigação |
|-------|-----------|
| API key inválida/ausente | `{"success": false}` precisa ser tratado explicitamente, com mensagem clara nas settings — não stack trace crua |
| RA pede cache agressivo | Cache local obrigatório + nunca re-baixar a lista completa sem o usuário pedir |
| 40 mil jogos | List rápido primeiro; detalhes sob demanda e rate-limited |
| Extensões `.md` vs outras | Varrer todas as extensões relevantes (`.md .zip .7z .nes .sfc .smc .gba .z64 .n64 .iso .cue .gbs .gb .gbc .sms .gg .a26 .j64 .p64 ...`) |
| Pastas grandes | Streaming no MD5 + reportar progresso |
| Patch de tradução com nome diferente | Casar por hash, nunca por nome de arquivo |
| Instalação do `.pext` apaga a pasta | Proibido gravar na pasta da extensão |
| `Newtonsoft.Json` divergente | Fixar em 10.0.3 e conferir a versão do assembly ao subir |
| VS Code não suporta bem `net462` | A doc oficial recomenda **Visual Studio ou Rider**. Se estiver no VS Code, o C# precisa estar configurado para .NET Framework, não .NET 5+ |
| Toque no SDK do Playnite 11 | Não investir agora. Se precisar, isolar `GetGames` numa única classe deixa o port barato |

---

## 9. Fontes

- API `LibraryPlugin` — <https://api.playnite.link/docs/api/Playnite.SDK.Plugins.LibraryPlugin.html>
- Tutorial Library Plugins — <https://api.playnite.link/docs/tutorials/extensions/libraryPlugins.html>
- Tutorial Plugins (net462, deps, Toolbox) — <https://api.playnite.link/docs/tutorials/extensions/plugins.html>
- Data directory — <https://api.playnite.link/docs/tutorials/extensions/dataDirectory.html>
- Toolbox — <https://api.playnite.link/docs/tutorials/toolbox.html>
- `PlayniteSDK` no NuGet — <https://www.nuget.org/packages/PlayniteSDK>
- Doc da API do RA — <https://api-docs.retroachievements.org/>
- Referência de implementação (Steam Library) — <https://github.com/JosefNemec/PlayniteExtensions/tree/master/source/Libraries/SteamLibrary>
- Versão atual do Playnite (10.62, 29/09/2026) — <https://codeberg.org/Playnite/Playnite/releases>

---

## 10. Publicação e versionamento

- **Repositório:** <https://github.com/DenkaAkumaPedro/biblioteca-ra-playnite>
  (público). Raiz do git = raiz deste projeto.
- **Versionamento:** começa em **0.0.1** (versão de teste). Durante a fase
  de teste o formato é `0.0.x`; a partir de 0.1.0 quando sair do rótulo de
  teste. Número sobe a cada release publicada.
- **Push automático:** sempre que houver mudanças na pasta do projeto, elas
  são commitadas e enviadas ao GitHub — autorização permanente do usuário
  até que ele peça o contrário. Histórico nunca é reescrito.
- **CI:** `.github/workflows/build.yml` roda `dotnet build -c Release` em
  `windows-latest` a cada push (e em pull requests).
- **Release:** cada versão vira uma GitHub Release com a `.pext` anexada e
  os manifestos atualizados:
  1. bump da `Version` no `extension.yaml`
  2. novo pacote em `manifests/installer.yaml` (Version, PackageUrl da
     release, RequiredApiVersion, ReleaseDate, Changelog)
  3. `Toolbox.exe pack` → anexar na release → `gh release create`
  4. `Toolbox.exe verify addon addon.yaml` (valida manifest + manifesto de
     instalação via URL)
- **Escopo de escrita:** o git/gh é a única escrita autorizada fora da
  pasta do projeto (REGRAS §4/§5). Instalar o `.pext` no Playnite continua
  sendo coisa do usuário.