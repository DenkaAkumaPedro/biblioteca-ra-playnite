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
   ok (aval para a 0.1.0)

2- queria que ele sinccronizace os dados de atividade, ultima vez jogado e tempo de jogo
   → TRATADO (Modelo MK 1): nova ActivityService com caches activity_lastplayed.json (API_GetUserRecentlyPlayedGames, paginado) e activity_playtime.json (API_GetGameInfoAndUserProgress, 1 req/jogo, incremental). Escrita no banco no OnLibraryUpdated: LastActivity só avança, PlayCount vira 1, tudo com BufferedUpdate. Checkboxes novos: Sincronizar última vez jogado / Sincronizar tempo de jogo (padrão ligado). A cada atualização de biblioteca sincroniza; "Forçar atualização" refaz com força total; "Limpar cache" limpa os caches de atividade.
   ok

3- sobre o tempo de jogo quero que ele lance o tempo no tempo de atividade (usar de refencia meu outro addon do aniilist)
   → TRATADO (Modelo MK 1): Playtime = max(tempo local, tempo do RA em segundos) — só sobe, nunca diminui nem duplica (decisão do usuário; sem arquivo de baseline como o AniList). 1ª sync ≈ 2–4 min p/ 500 jogos; depois só busca jogos novos ou jogados desde a última sync. Progresso no log/status ("Tempo de jogo: 120/500...") e gravação incremental (cancelar no meio não perde o já consultado).
   ok

4- gostaria que a verificação de pastas dele por arquivos de emuladores possa ser sincronizada automaticamente, pegando as pastas cconfiguradas da parte de configurar emuladores do proprio playnite
   → TRATADO (Modelo MK 1): a config de emuladores do Playnite não tem campo de pasta de ROM (só pasta do emulador/executável), então por decisão do usuário o botão "Importar pastas dos jogos" varre os jogos do Playnite e coleta as pastas das ROMs (Game.Roms[].Path) e dos GameActions Type File, com dedupe e só pastas existentes.
   ok

5- ele n possui icoe pegue o icone do RA para usar como icone do addon
   → TRATADO (Modelo MK 1): substituído pelo joystick oficial do RA (ra-joystick-square.png, 128x128, repo RAWeb) + campo IconUrl no addon.yaml apontando para o raw do GitHub (aparece no navegador de addons). Toolbox verify addon passou.
   ok
