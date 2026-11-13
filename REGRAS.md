# REGRAS DE OPERAÇÃO

Regras que valem para **toda** interação neste projeto.
Estas regras têm prioridade sobre o costume, sobre a pressa e sobre suposições.

---

## 1. SEMPRE verificar os arquivos antes de tomar qualquer decisão

Esta é a regra mais importante. Ela vem **antes** de qualquer outra.

### Antes de decidir o que fazer

1. **Ler o arquivo.** Precisa ler o conteúdo atual com a ferramenta de leitura
   antes de editar. Nunca editar às cegas.
2. **Listar a pasta.** Antes de criar arquivo, verificar se ele já existe e se
   o caminho é o destino pretendido.
3. **Ler os vizinhos.** Antes de criar um arquivo novo, ler os arquivos
   semelhantes que já existem e seguir o estilo deles — nomes, estrutura,
   convenções, idioma dos comentários.
4. **Nunca sobrescrever sem ler.** Se o conteúdo existente não foi lido, não
   pode ser substituído.
5. **Não inventar o que existe.** Se não tem certeza do que há no arquivo, o
   comportamento correto é ler — não supor. Se ainda assim não der para ler
   (bloqueado, corrompido, sem permissão), dizer isso em vez de chutar.
6. **Se o arquivo não existir**, confirmar que o caminho é realmente o alvo
   certo antes de criar. Não presumir que a pasta existe.

### Por quê

Um arquivo que não foi lido pode:
- ter uma configuração que já resolve o problema
- ter uma restrição que quebra o plano
- ter estilo que, se ignorado, gera código inconsistente com o resto

Supor causa retrabalho. Ler, não.

---

## 2. Escopo de escrita — caminho absoluto e único

**Só pode criar, editar, renomear ou apagar arquivos dentro desta pasta:**

```
D:\Arquivos Pessoais\Arquivos de Denka Akuma HD\Meus Projetos\VSCode\Playnite Adons\Biblioteca RA no Playnite
```

Tudo que estiver **fora** desse caminho está **fora do escopo**. Sem exceção
implicita, sem "só dessa vez".

### O que isso bloqueia explicitamente

As pastas vizinhas estão **fora do escopo** e não devem ser lidas para
modificação, muito menos alteradas:

- `Playnite Adons\GameCard`
- `Playnite Adons\AniList Com Horas (PRONTO) [Español]`
- `Playnite Adons\Gplay exophse (PRONTO)`
- `Playnite Adons\Tema Modo Desktop`
- `Playnite Adons\Tema Modo Tela Cheia`

Qualquer coisa acima de `Biblioteca RA no Playnite` — inclusive a pasta
`Playnite Adons` e a pasta `Meus Projetos` — é somente leitura por coincidência,
não por permissão. **Nada** de escrita lá.

---

## 3. Permitido dentro da pasta do projeto

- Ler, criar, editar, renomear e listar arquivos
- Compilar a solução do projeto (a saída vai para o `bin\`/`obj\` do próprio projeto)
- Rodar testes e scripts que estão dentro da pasta
- Empacotar o `.pext` (saída na própria pasta)
- Instalar pacotes NuGet **no projeto** (via `PackageReference` / `dotnet restore`)

## 4. Proibido

### Fora da pasta
- Criar, editar, renomear ou apagar arquivo em qualquer outro lugar do disco
- Mexer nos addons irmãos, em qualquer subpasta deles
- Modificar pasta acima da pasta do projeto

### No sistema
- Instalar pacotes globais (`npm install -g`, `pip install`, `dotnet tool install`)
- Alterar `PATH`, variáveis de ambiente, registro do Windows
- Instalar, desinstalar ou atualizar software do sistema
- Instalar ou mexer no Playnite fora do fluxo normal (`.pext`)

### Git
- `git commit`, `git push`, `git tag` — **só com pedido explícito**
- Reescrever histórico, forçar push, mexer em branch

### Segurança e segredo
- **Nunca** escrever a Web API Key do RetroAchievements em nenhum arquivo, nem
  no código, nem em `.md`, nem em config versionável. A chave entra **em
  runtime**, pelas settings do addon.
- Nunca devolver, logar ou exibir a chave do usuário na saída do terminal
- Nunca injetar segredo em URL que possa acabar em log

### Código do addon
- Nunca referenciar `Playnite.dll`, `Playnite.Common` ou `Playnite.Toolkit` —
  o Playnite recusa carregar plugins que referenciem assemblies não-SDK
- Nunca fixar `Newtonsoft.Json` fora da **10.0.3** (a versão que o Playnite
  referencia). Versão divergente = assembly duplicado = plugin não carrega
- Nunca gravar arquivo em disco dentro da pasta da extensão (o `.pext` é
  reextraído por cima e apaga tudo). Dado de usuário vai para
  `GetPluginUserDataPath()`
- Nunca declarar `async` sem `await`; nunca ignorar `CancelToken`

### Escopo do pedido
- Não implementar item do backlog do PLANO.md sem o usuário pedir
- Não adicionar dependência, framework ou biblioteca que o projeto não use

---

## 5. Quando travar num impasse

Se a solução exigir sair do escopo:

1. **Parar.**
2. **Perguntar ao usuário**, dizendo o que é preciso, por quê, e o que quebra
   se não for feito.
3. **Esperar resposta.**

O que **não** é aceitável: improvisar um contorno, usar caminho absoluto
"só por garantia", escrever em pasta vizinha, ou criar script que burla a
restrição.

Exceção única e estreita: leitura fora da pasta é permitida para consultar
material de referência (documentação do Playnite, da API do RA, outros addons
como exemplo). Leitura não altera nada. Escrita, nunca.

---

## 6. Como trabalhar neste projeto

- Escrever em **português**, sem exagero
- Seguir o estilo e as convenções que já existirem nos arquivos
- Não adicionar comentário de código sem o usuário pedir
- Preferir a menor mudança que resolve o pedido
- Ao terminar, rodar a checagem que existir (build, testes) e dizer o resultado
- Se algo ficou incerto, dizer que ficou incerto — não assumir que passou
- Manter o `PLANO.md` atualizado quando o escopo mudar

---

## 7. Resumo em uma linha

> Ler antes de decidir. Escrever só dentro da pasta do projeto.
> Fora disso, perguntar.