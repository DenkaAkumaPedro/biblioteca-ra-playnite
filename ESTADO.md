# Estado atual do projeto

Atualizado em: 13/11/2026

> Arquivo de memória entre sessões. Ler este arquivo junto com `PLANO.md` e
> `REGRAS.md` ao iniciar qualquer trabalho neste projeto. Atualizar este
> arquivo sempre que a etapa mudar.

## Situação

- **Versão 0.0.1 publicada e em teste** — correções em andamento a partir dos
  erros anotados em `Notas.md`
- **`Modelo Teste 02` é a versão ativa de desenvolvimento** (cópia do 01 com
  as correções); `Modelo Teste 01` continua congelado como referência
- Repo `biblioteca-ra-playnite` no GitHub: push automático ativo
- CI (`build.yml`) agora compila o **Modelo Teste 02**
- Fase 6 (metadados detalhados) e backlog continuam fora do escopo

### Correções aplicadas no Modelo Teste 02 (Notas.md)

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

## Congelamentos

- **`Modelo Teste 01` é referência congelada.** Não alterar nada dentro
  dela — nem código, nem documentação — sem pedido explícito do usuário.
- As cópias de `PLANO.md`/`REGRAS.md` dentro de `Modelo Teste 01` são o
  último snapshot sincronizado; **parar de sincronizar** por lá.
- O congelamento vale até o usuário pedir o contrário.

## Próximo passo

- Usuário testar a versão do `Modelo Teste 02` (empacotar `.pext` se
  quiser instalar no Playnite).
- Na volta: aplicar as melhorias adiadas e novos bugs relatados.
- Antes de qualquer edição: ler este arquivo, o PLANO e as REGRAS.
