# Estado atual do projeto

Atualizado em: 14/11/2026

> Arquivo de memória entre sessões. Ler este arquivo junto com `PLANO.md` e
> `REGRAS.md` ao iniciar qualquer trabalho neste projeto. Atualizar este
> arquivo sempre que a etapa mudar.

## Situação

- **Versão 0.1.0 implementada no `Modelo MK 1`** — todas as etapas
  aprovadas do `Notas.md` concluídas:
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
- **`.pext` 0.1.0 gerado** em
  `Modelo MK 1\BibliotecaRA_46ec2715-329d-4e68-98ca-a35b7259cc5a_0_1_0.pext`
- Build Release sem erros (só aviso NU1903 do Newtonsoft 10.0.3, pinado
  de propósito pela versão do Playnite)
- Repo `biblioteca-ra-playnite` no GitHub: push automático ativo
- CI (`build.yml`) compila o **Modelo MK 1**
- Fase 6 (metadados detalhados) e backlog continuam fora do escopo
- **Release oficial** (`manifests/installer.yaml`, tag, `gh release`)
  continua só a pedido do usuário

## Como funciona a sincronização de atividade (0.1.0)

- **Automática**: `OnLibraryUpdated` dispara após a atualização da
  biblioteca do Playnite → roda em `Task.Run` (não bloqueia), com trava
  anti-reentrada; loga o resultado; notificação só se algo mudou ou falhou
- **Manual**: botão "Forçar atualização da biblioteca" refaz tudo com
  força total (status visível na tela de configurações)
- **Dados**: `API_GetUserRecentlyPlayedGames` (paginado 50, UTC → local)
  p/ `LastActivity`; `API_GetGameInfoAndUserProgress` (1 req/jogo) p/
  `Playtime` (segundos) — só consulta jogo novo ou cuja última vez jogado
  avançou desde a consulta; fallback grátis: `MostRecentAwardedDate` do
  cache da biblioteca da conta
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

- Usuário instalar o `.pext` 0.1.0 do `Modelo MK 1`
  (**Configurações → Add-ons → Instalar do arquivo**) e testar:
  - atualizar biblioteca → conferir `LastActivity`/`Playtime`/`PlayCount`
    em alguns jogos conhecidos (1ª sync de tempo leva ~2–4 min/500 jogos)
  - botão **Importar pastas dos jogos** nas configurações
  - novo ícone do RA na lista de addons
- Na volta: registrar resultados/bugs no `Notas.md` e decidir se vira
  release oficial ou próxima versão.
- Antes de qualquer edição: ler este arquivo, o PLANO e as REGRAS.
