1- ao sincronizar demora muito ver se pode ser erro no codigo ou é normal pelo volume de dados
   → TRATADO (Modelo Teste 02): volume reduzido. Com detecção desligada, sincroniza só a conta. Com ligada, 1ª vez baixa catálogo de hashes (necessário), depois cache.
   ok

2-ele esta puxando todos os jogos e sub sets do site, era para ele puxar apenas os jogos da conta do usuario
   → TRATADO (Modelo Teste 02): importa só jogos da conta (API_GetUserCompletionProgress) + jogos detectados por hash no PC. Catálogo completo só como cache interno de hashes, nunca importado inteiro. Novo setting: Usuário do RetroAchievements.
   ok

3- pode ter cido por ele puxar tudo mas estava estranhamente não atualizando, tanto que só reparei que estava puxando tudo no momento que cancelei e vi que puxou mais que 16000 itens
   → TRATADO PARCIALMENTE (Modelo Teste 02): importa muito menos, então não deve mais puxar 16000 itens. Progresso visível na UI e cache parcial ao cancelar ficaram ADIADOS para versão futura (ver ESTADO.md).
   ok


Testes da verção 0.0.2

1- Sincronizou os jogos de maneira rapida então agorra temos o basico isso é bom vamos para as melhorias

2- queria que ele sinccronizace os dados de atividade, ultima vez jogado e tempo de jogo

3- sobre o tempo de jogo quero que ele lance o tempo no tempo de atividade (usar de refencia meu outro addon do aniilist)

4- gostaria que a verificação de pastas dele por arquivos de emuladores possa ser sincronizada automaticamente, pegando as pastas cconfiguradas da parte de configurar emuladores do proprio playnite

5- ele n possui icoe pegue o icone do RA para usar como icone do addon
