# Política de privacidade — Firaw Work Assistant e Strigoi Companion Assistant

Atualizada em 2 de outubro de 2026.

O Firaw salva tarefas, checklists, anotações e preferências no perfil local do Windows, em `%LOCALAPPDATA%\Firawynix\WorkAssistant`. O módulo Strigoi salva preferências, jogos, sessões, memórias, relatórios sem imagens e diagnósticos em `%LOCALAPPDATA%\StrigoiCompanion`. Os aplicativos não exigem conta e o desenvolvedor não recebe esses arquivos automaticamente.

Ao ativar a captura de jogo, o Strigoi lê a janela escolhida ou acompanhada. As imagens recentes ficam apenas na memória durante a sessão e são descartadas ao pausar ou encerrar a captura. Ao perguntar sobre uma imagem, a amostra atual é enviada somente ao Ollama configurado em `localhost`, por ação do usuário. Se a pesquisa na web for ativada, a consulta e um contexto de texto limitado são enviados ao provedor de pesquisa. Imagens da captura e o diário completo não são enviados nessa pesquisa.

O instalador online do Firaw consulta um manifesto e baixa o pacote adequado. O servidor de downloads pode registrar dados técnicos da requisição, como endereço IP, horário e arquivo solicitado. A distribuição pela Microsoft Store usa os serviços da Microsoft. O movimento automático do cursor é opcional e controlado pelo usuário.

A desinstalação preserva os dados locais para evitar perda acidental. Para apagá-los, feche os aplicativos e remova as duas pastas indicadas acima.

Suporte: hugo@firawynix.com.br.
