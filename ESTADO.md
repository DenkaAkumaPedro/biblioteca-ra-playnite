# Estado atual do projeto

Atualizado em: 14/11/2026

> Arquivo de memória entre sessões. Ler este arquivo junto com `PLANO.md` e
> `REGRAS.md` ao iniciar qualquer trabalho neste projeto. Atualizar este
> arquivo sempre que a etapa mudar.

## Situação

- **Reorganização de pastas concluída (Etapa 0 da v0.1.0):**
  - `Modelo Teste 01` **deletado** (apresentava erros; não serve de
    referência nem de base — o git guarda o histórico)
  - `Modelo Teste 02` renomeado para **`Modelo Base`** — congelado para
    sempre como referência do modelo mais simples do projeto (mantém os
    `.pext` 0.0.1 e 0.0.2 como histórico local)
  - **`Modelo MK 1`** criado como cópia do Modelo Base (sem `bin`/`obj`/
    `.pext`, que são artefatos regeneráveis) — **versão ativa**
- **Planejamento da v0.1.0 aprovado pelo usuário** (itens 2–5 do
  `Notas.md` + versão 0.1.0). Decisões do usuário:
  - Item 4 = coletar pastas das **ROMs dos jogos** (`Game.Roms[].Path` +
    `GameActions`), não pastas dos emuladores
  - Playtime: **`max(local, RA)`** — só sobe, nunca diminui/duplica
  - Sessões no plugin GameActivity: **não** nesta versão (deixar p/ depois)
- Repo `biblioteca-ra-playnite` no GitHub: push automático ativo
- CI (`build.yml`) agora compila o **Modelo MK 1**
- Fase 6 (metadados detalhados) e backlog continuam fora do escopo

### Ordem das etapas da v0.1.0 (aprovada)

0. ✅ Reorganização (feita) — 1. Ícone do RA → 2. Atividade (`LastActivity`
   + `Playtime`) → 3. Pastas dos jogos → 4. Docs + bump 0.1.0 + build/pack

### Correções da 0.0.2 já aplicadas (Notas.md)

1. **Importação limitada à conta do usuário** — `GetGames()` baixa só a
   biblioteca da conta via `API_GetUserCompletionProgress` (precisa do
   novo setting *Usuário do RetroAchievements*). O catálogo completo só é
   baixado como cache interno de hashes, e **apenas** se “Detectar ROMs”
   estiver ligado — nunca é importado inteiro para a biblioteca.
   Importa = jogos da conta ∪ jogos detectados por hash no PC.
2. **Demora na sincronização** — reduzida ao eliminar o import do catálogo
   inteiro. Com detecção desligada, sincroniza só a conta (rápido). Com
   detecção ligada, a 1ª vez ainda baixa o catálogo de hashes (necessário;
   a API do RA não busca por MD5); depois usa cache.
3. **Parecia não atualizar / perdia tudo ao cancelar** — parcialmente
   mitigado pelo volume menor. Ver “Melhorias adiadas” abaixo.

## Melhorias adiadas (guardadas para versão futura)

O usuário pediu para lembrar — não implementar ainda:

- **Progresso visível na UI** durante o sync (hoje só vai para o log
  `logger.Debug` no `GetGames`)
- **Cache parcial retomável** — salvar o progresso do download mesmo se o
  usuário cancelar no meio (hoje cancelar zera o progresso)
- **Sessões no plugin GameActivity** (padrão do addon AniList) — o usuário
  optou por deixar para uma versão futura

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
  **parar de sincronizar** por lá.
- `Modelo Teste 01` foi deletado a pedido do usuário (histórico no git).
- O congelamento vale até o usuário pedir o contrário.

## Próximo passo

- Implementar a v0.1.0 no **`Modelo MK 1`** na ordem aprovada:
  Ícone → Atividade (itens 2+3) → Pastas (item 4) → Docs + 0.1.0 + build.
- Usuário instalar o `.pext` gerado (**Configurações → Add-ons → Instalar
  do arquivo**) e testar: usuário do RA, atualizar biblioteca, conferir
  `LastActivity`/`Playtime` em alguns jogos.
- Antes de qualquer edição: ler este arquivo, o PLANO e as REGRAS.
