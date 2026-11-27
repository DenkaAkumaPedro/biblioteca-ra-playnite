# Estado atual do projeto

Atualizado em: 27/11/2026

> Arquivo de memória entre sessões. Ler este arquivo junto com `PLANO.md` e
> `REGRAS.md` ao iniciar qualquer trabalho neste projeto. Atualizar este
> arquivo sempre que a etapa mudar.

## Situação

- **Versão 0.1.3 no `Modelo MK 1`** — corrige o teste da 0.1.2 (relato no
  `Notas.md`):
  1. ❌ Teste da 0.1.2: as sessões do GameActivity eram **substituídas** na
     data mais recente (3h + 2h → 1 sessão de 5h) em vez de acumular
  2. ✅ Causa: `UpsertSession` removia as sessões próprias e recriava uma só
     com o total do RA datada no `LastPlayed`; a API do RA não expõe
     histórico por sessão/dia (só `UserTotalPlaytime` + `LastPlayed`)
  3. ✅ `Services/GameActivityWriter.cs`: escrita agora é **incremental** —
     soma `ElapsedSeconds` das sessões com o `SourceID` fixo (baseline) e
     anexa sessão nova só com o `delta = total − soma`, datada na última vez
     jogado; sem delta não grava nada (idempotente). A sessão única gravada
     pela 0.1.2 vira a base → primeira sync sem contagem em dobro. Guarda:
     `SourceID`/`ElapsedSeconds` desconhecidos → não grava (evita inflar)
  4. ✅ `BibliotecaRA.cs`: campo `PendingSession.TotalSeconds` (era
     `Seconds`); notificação passa a contar "N sessões **novas**" no GA
  5. ✅ Versão **0.1.3** em ambos os `extension.yaml` + `UserAgent`
     `BibliotecaRA/0.1.3`; Notas/README/PLANO/ESTADO atualizados
- **Antes disso (0.1.2) — sessões no GameActivity** (relato do teste da
  0.1.1 no `Notas.md`):
  1. ✅ Diagnóstico: **não era bug do sync** — o plugin GameActivity ignora
     `game.Playtime` e só soma as próprias sessões (`Items` →
     `SessionPlaytime`); tempo importado de biblioteca nunca aparece lá
     (issue #203 do GA)
  2. ✅ `Services/GameActivityWriter.cs` (port do addon AniList): grava 1
     sessão por jogo com o tempo do RA via reflexão no banco do GA
     (`GameActivityMonitoring` → `PluginDatabase`); `SourceID` fixo próprio
     (upsert idempotente, nunca infla tempo); `GameActionName =
     "RetroAchievements"`; falha degrada com log se o GA não estiver
     instalado
  3. ✅ Setting `WriteGameActivitySessions` (padrão ligado) + checkbox
     "Gravar sessão no GameActivity" + localização pt_BR/en_US
  4. ✅ `BibliotecaRA.cs`: monta as sessões do cache de tempo (só
     `Seconds > 0`, data = `LastPlayed` ou data da consulta) e chama o
     writer **fora** do `BufferedUpdate` (banco do GA é separado); contagem
     de sessões entra na notificação/status
  5. ✅ Versão **0.1.2** em ambos os `extension.yaml` + `UserAgent`
     `BibliotecaRA/0.1.2`; Notas/README/PLANO/ESTADO atualizados
- **Antes disso (0.1.1) — correção do 429:**
  1. ✅ Causa diagnosticada: **rate limit do servidor do RA** — o addon pedia
     4 req/s (250 ms) e o sync de tempo faz 1 req/jogo; batia no limite e a
     sync morria antes de aplicar o tempo
  2. ✅ `ApiClient`: taxa base **1 req/s** (era 4); em 429 espera
     **15s/60s/180s** (respeita `Retry-After` do servidor); **cooldown
     global** (toda requisição pausa, não só a que falhou); **taxa
     adaptativa** (dobra a cada 429 até 10s, volta devagar ao normal após
     5 min sem 429); exceção nova `RaRateLimitException`
  3. ✅ `ActivityService`: se estourar mesmo assim, o sync de tempo **para
     graciosamente** — cache parcial salvo, `PlaytimeSyncResult` com
     `RateLimited`/`Completed`/`Total`, e o `pending` da próxima sync retoma
     só o que faltou
  4. ✅ `BibliotecaRA.cs`: notificação **informativa** no rate limit (não
     mais "Falha ao sincronizar atividade"); o tempo já consultado é
     aplicado mesmo na parada parcial
- **`.pext` 0.1.2** ainda disponível; **0.1.3** sai no build desta leva
- Build Release sem erros (só aviso NU1903 do Newtonsoft 10.0.3, pinado
  de propósito pela versão do Playnite)
- Repo `biblioteca-ra-playnite` no GitHub: push automático ativo
- CI (`build.yml`) compila o **Modelo MK 1**
- Fase 6 (metadados detalhados) e backlog continuam fora do escopo
- **Release oficial** (`manifests/installer.yaml`, tag, `gh release`)
  continua só a pedido do usuário

## Como funciona a sincronização de atividade (0.1.3)

- **Automática**: `OnLibraryUpdated` dispara após a atualização da
  biblioteca do Playnite → roda em `Task.Run` (não bloqueia), com trava
  anti-reentrada; loga o resultado; notificação só se algo mudou ou se
  parou no rate limit (informativa, não é erro)
- **Manual**: botão "Forçar atualização da biblioteca" refaz tudo com
  força total (status visível na tela de configurações)
- **Dados**: `API_GetUserRecentlyPlayedGames` (paginado 50, UTC → local)
  p/ `LastActivity`; `API_GetGameInfoAndUserProgress` (1 req/jogo) p/
  `Playtime` (segundos) — só consulta jogo novo ou cuja última vez jogado
  avançou desde a consulta; fallback grátis: `MostRecentAwardedDate` do
  cache da biblioteca da conta
- **Sessões no GameActivity**: se a checkbox "Gravar sessão no GameActivity"
  estiver ligada (padrão), após aplicar o tempo o addon grava sessões
  **incrementais** no banco do plugin GameActivity: a soma das sessões da
  extensão (`SourceID` fixo) é a base e cada sync anexa uma sessão nova só
  com o delta do tempo jogado desde a anterior, datada na última vez jogado
  no RA. Sem delta não grava nada — rodar a sync N vezes não infla tempo e
  as sessões antigas ficam no lugar (a única gravada pela 0.1.2 vira a base).
  Limite: jogadas entre duas syncs viram uma sessão só (a API do RA não tem
  histórico por dia). Sem o GA instalado, só avisa no log e segue
- **Rate limit (429)**: taxa base 1 req/s; em 429 espera 15s/60s/180s
  (ou o `Retry-After` do servidor), com cooldown global; a taxa dobra até
  10s e volta devagar após 5 min sem 429. Se estourar após as tentativas,
  o sync de tempo para graciosamente com o cache parcial (não vira erro);
  a próxima sync retoma só o que faltou
- **Janela de frescor**: cache de atividade vale 10 min (força total
  ignora); caches de tempo gravam a cada 10 consultas (cancelar não perde)

## Melhorias adiadas (guardadas para versão futura)

O usuário pediu para lembrar — não implementar ainda:

- **Progresso visível na UI** durante o sync do `GetGames` (hoje só log)
- **Cache parcial retomável** do download do `GetGames` (atividade/tempo
  **já** grava incremental)
- **Correções B1/B2/B5 do sync** (investigação 0.1.1): apply não roda em
  erro/cancelamento antes dele; resposta `[]` envenena cache com
  `Seconds=0`; um jogo com erro derruba o apply inteiro
- ~~Sessões no plugin GameActivity~~ — **implementado na 0.1.2**

## Regra de versionamento de `.pext`

- Para a **v0.1.0** o usuário determinou o número (bump de minor, não da
  unidade mais baixa) — vale só para esta leva.
- Da próxima em diante volta a regra: **aumentar a unidade mais baixa**
  (ex.: 0.1.0 → 0.1.1), a menos que o usuário peça outro número.
- Isso vale para o `Version` no `extension.yaml` (cópia em
  `playnite-plugin/` e a de conferência na pasta do modelo) antes do
  `dotnet build` + `Toolbox pack`.
- Bump para release oficial (`manifests/installer.yaml`, tag, `gh release`)
  continua só a pedido do usuário.

## Congelamentos

- **`Modelo Base` é referência congelada para sempre** (o modelo mais
  simples do projeto). Não alterar nada dentro dela — nem código, nem
  documentação — sem pedido explícito do usuário.
- As cópias de `PLANO.md`/`REGRAS.md` dentro de `Modelo Base` são snapshot;
  **parar de sincronizar** por lá. Mesma coisa para as cópias dentro de
  `Modelo MK 1` — fonte de verdade é a raiz.
- `Modelo Teste 01` foi deletado a pedido do usuário (histórico no git).
- O congelamento vale até o usuário pedir o contrário.

## Próximo passo

- Usuário instalar o `.pext` 0.1.3 do `Modelo MK 1`
  (**Configurações → Add-ons → Instalar do arquivo**) e testar:
  - jogar no RA → atualizar biblioteca → notificação com "**N sessões
    novas no GameActivity**" e conferir no GameActivity que a sessão nova
    entrou **ao lado** das antigas (data = última vez jogado), sem somar o
    tempo em dobro
  - atualizar a biblioteca 2× seguidas **sem jogar nada** → 0 sessões novas
    e o tempo no GA não muda (idempotência)
  - conferir `LastActivity`/`Playtime`/`PlayCount` nos detalhes do jogo no
    Playnite (campo "Tempo de jogo")
  - se o GameActivity não estiver instalado: só aviso no log, sem erro
- Na volta: registrar resultados/bugs no `Notas.md` e decidir se vira
  release oficial ou próxima versão (eventuais correções B1/B2/B5 do sync
  ficam para a próxima; a divisão retroativa do histórico antigo por data
  continua impossível — a API do RA não expõe).
- Antes de qualquer edição: ler este arquivo, o PLANO e as REGRAS.
