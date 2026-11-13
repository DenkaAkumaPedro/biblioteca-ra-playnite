# Estado atual do projeto

Atualizado em: 12/11/2026

> Arquivo de memória entre sessões. Ler este arquivo junto com `PLANO.md` e
> `REGRAS.md` ao iniciar qualquer trabalho neste projeto. Atualizar este
> arquivo sempre que a etapa mudar.

## Situação

- **Versão 0.0.1 publicada e em teste pelo usuário** (teste manual em
  andamento — o usuário vai relatar bugs e melhorias depois)
- Repo `biblioteca-ra-playnite` no GitHub: push automático ativo,
  CI (`build.yml`) verde, release `v0.0.1` com o `.pext`
- `addon.yaml` + `manifests/installer.yaml` validados pelo
  `Toolbox verify addon`
- Fase 6 (metadados detalhados) e backlog continuam fora do escopo

## Congelamentos

- **`Modelo Teste 01` é referência congelada.** Não alterar nada dentro
  dela — nem código, nem documentação — sem pedido explícito do usuário.
- As cópias de `PLANO.md`/`REGRAS.md` dentro de `Modelo Teste 01` são o
  último snapshot sincronizado; **parar de sincronizar** por lá.
- O congelamento vale até o usuário pedir o contrário.

## Próximo passo

- Usuário está testando a v0.0.1.
- Na volta: aplicar melhorias e correções de bugs em **`Modelo Teste 02`**
  (cópia de `Modelo Teste 01`), mantendo o 01 intacto.
- Antes de qualquer edição: ler este arquivo, o PLANO e as REGRAS.
