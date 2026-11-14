# Estado atual do projeto

Atualizado em: 14/11/2026

> Arquivo de memória entre sessões. Ler este arquivo junto com `PLANO.md` e
> `REGRAS.md` ao iniciar qualquer trabalho neste projeto. Atualizar este
> arquivo sempre que a etapa mudar.

## Situação

- **Versão 0.1.1 implementada no `Modelo MK 1`** — correção do 429 relatado
  no teste da 0.1.0 (`Notas.md`):
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
  5. ✅ Versão **0.1.1** em ambos os `extension.yaml` + `UserAgent`
     `BibliotecaRA/0.1.1`; Notas/README/ESTADO atualizados
- **Antes disso (0.1.0) — tudo implementado e testado:**
  1. ✅ Reorganização: `Modelo Teste 01` deletado; `Modelo Teste 02` →
     **`Modelo Base`** (congelado p/ sempre); **`Modelo MK 1`** criado como
     cópia e é a versão ativa (CI compila ele)
  2. ✅ Ícone: joystick oficial do RA (`ra-joystick-square.png` 128×128 do
     repo RAWeb) + `IconUrl` no `addon.yaml`; `Toolbox verify addon` passou
  3. ✅ Atividade (itens 2+3 do Notas): `ActivityService` com caches
     `activity_lastplayed.json` e `activity_playtime.json`; escrita no
     `OnLibraryUpdated` (padrão AniList: `BufferedUpdate` + `Games.Update`,
     trava `SemaphoreSlim`, lock `Wait(0)`); `LastActivity` só avança;
     `PlayCount` = 1; `Playtime` = **max(local, RA)**; checkboxes
     `SyncActivity`/`SyncPlaytime` (padrão ligado); progresso no status/log
     com gravação incremental do cache de tempo
  4. ✅ Pastas (item 4): botão **"Importar pastas dos jogos"** coleta
     diretórios de `Game.Roms[].Path` + `GameActions` Type File (decisão do
     usuário — a config de emuladores do Playnite não tem pasta de ROM)
  5. ✅ Versão **0.1.0** em ambos os `extension.yaml` + `UserAgent`
     `BibliotecaRA/0.1.0`; docs (Notas/README/PLANO/ESTADO) atualizados
- **`.pext` 0.1.1 gerada** em
  `Modelo MK 1\BibliotecaRA_46ec2715-329d-4e68-98ca-a35b7259cc5a_0_1_1.pext`
- Build Release sem erros (só aviso NU1903 do Newtonsoft 10.0.3, pinado
  de propósito pela versão do Playnite)
- Repo `biblioteca-ra-playnite` no GitHub: push automático ativo
- CI (`build.yml`) compila o **Modelo MK 1**
- Fase 6 (metadados detalhados) e backlog continuam fora do escopo
- **Release oficial** (`manifests/installer.yaml`, tag, `gh release`)
  continua só a pedido do usuário

## Como funciona a sincronização de atividade (0.1.1)

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
- **Sessões no plugin GameActivity** (padrão do addon AniList) — usuário
  optou por deixar para versão futura

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

- Usuário instalar o `.pext` 0.1.1 do `Modelo MK 1`
  (**Configurações → Add-ons → Instalar do arquivo**) e testar:
  - atualizar biblioteca → o tempo de jogo deve importar **sem o erro 429**
    (1ª sync de tempo agora é mais lenta: ≈1 req/s, ~8–9 min/500 jogos);
    se ainda bater limite, a notificação é informativa e a próxima sync
    continua de onde parou
  - conferir `LastActivity`/`Playtime`/`PlayCount` em jogos conhecidos
  - botão **Importar pastas dos jogos** e ícone do RA (se ainda não testou)
- Na volta: registrar resultados/bugs no `Notas.md` e decidir se vira
  release oficial ou próxima versão.
- Antes de qualquer edição: ler este arquivo, o PLANO e as REGRAS.
