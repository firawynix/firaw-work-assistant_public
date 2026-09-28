# Firaw • Assistente de trabalho

Aplicativo Windows para lembretes, checklists e avisos sobre a tela. A identidade ciano segue os projetos Firawynix, incluindo FirawMerge e Firaw SnapCopyText.

[Site e apresentação](https://work.firawynix.com.br/) · [Instalador online](https://jogos.firawynix.com.br/api/games/firaw-work-assistant/windows/arquivo) · [Apoiar](https://firawynix.com.br/apoie?de=work-assistant)

## Recursos

- Tarefas com data inicial, prazo original e uma nova data final opcional para registrar prazo **postergado** ou **reduzido**. Os avisos seguem a data final vigente sem perder o prazo original.
- Nome livre para projeto ou ambiente, mesmo sem pasta no computador. Cada lembrete pode vincular uma pasta opcional pelo botão **Vincular pasta**; com o botão direito é possível trocá-la ou desvinculá-la.
- Checklist em cada tarefa, com marcação, remoção e progresso calculado em porcentagem. O botão **Concluir tarefa** também fica no sticker expandido.
- Sticker translúcido e móvel por tarefa, acima das janelas. O botão na barra superior abre a checklist; **Aa** oculta ou mostra o texto; **◐** alterna a transparência. Arraste o canto inferior direito para redimensionar.
- Anotações independentes das tarefas, editáveis diretamente no sticker, com cor, transparência e tamanho ajustáveis.
- O sticker de tarefa também mostra um campo de texto livre para notas rápidas.
- Assistente animado durante tarefas iniciadas e ainda pendentes, inclusive atrasadas: mostra até três projetos lado a lado, com colunas mais compactas quando há três. No menu **Projetos visíveis** do bonequinho ou do ícone da bandeja, escolha **Todos os projetos** ou **Somente** um projeto; depois marque outros se quiser. Use a rolagem ou **Mais** para ver outros projetos; toque em uma tarefa para abrir sua checklist. As cores e expressões acompanham o progresso (vermelho, âmbar, verde e ciano).
- Aviso em janela sobre a tela com **Concluir**, **Adiar 10 min** e **Abrir**.
- Repetição diária ou semanal; ao concluir, o próximo aviso é agendado e a checklist é reiniciada.
- Ícone na bandeja; fechar a janela principal mantém os avisos ativos.
- Ícone de Huginn e Muninn em ciano com corvos maiores e versões próprias para tamanhos pequenos da barra de tarefas e da bandeja.
- O bonequinho tem cinco tamanhos: **Compacto**, **Pequeno**, **Médio**, **Grande** e **Extra grande**, escolhidos no seu menu ou no ícone ao lado do relógio. O clique na rodinha sobre ele oculta; o mesmo clique no ícone da bandeja restaura quando houver tarefas pendentes. Sem tarefas ativas, use **Visualizar bonequinho** na bandeja para abrir uma prévia que não altera seus lembretes.
- Os cartões dos projetos acima do bonequinho têm os mesmos cinco tamanhos em **Tamanho dos projetos**. Essa escolha é independente do tamanho do bonequinho e fica salva para as próximas aberturas.
- A janela do assistente só recebe cliques nos cartões e na silhueta visível do bonequinho; as áreas vazias deixam o clique passar para os outros aplicativos.
- **Ctrl esquerdo + clique** no bonequinho ativa o movimento automático; repita o gesto para desativar e parar imediatamente. O menu do bonequinho e o da bandeja também mostram o estado e permitem alterná-lo. Ao ativar, pode ocorrer um movimento inicial de 1, 3 ou 5 segundos, ou nenhum, conforme **Movimento imediato ao ativar**.
- **Programar movimento automático** define apenas de quantos em quantos minutos o cursor se move e por quantos segundos (mínimo e máximo). Salvar esses valores não ativa o movimento. Use 0 minutos para deixar a programação desligada. O movimento só ocorre enquanto o bonequinho estiver visível e o modo estiver ativo.
- Opção para iniciar com o Windows, apenas para o usuário atual.
- Dados locais em `%LOCALAPPDATA%\Firawynix\WorkAssistant\tasks.json`, anotações em `notes.json` e preferências em `settings.json`.

## Uso rápido

1. Clique em **Nova tarefa** para definir início, prazo e etapas da checklist. Digite o nome do projeto ou ambiente livremente; vincule uma pasta apenas se quiser. Se o prazo mudar, escolha **Postergado** ou **Reduzido** e informe a nova data final. Marque **Fixar sticker na tela** para acompanhar a tarefa sobre as demais janelas.
2. Clique em **Nova anotação** para criar um texto livre. Edite no painel ou diretamente no sticker. A barra superior permite mover, trocar a cor, mudar a transparência e ocultar.
3. O assistente animado aparece enquanto houver tarefas já iniciadas e ainda não concluídas. Clique numa tarefa da lista para abrir sua checklist; arraste o bonequinho para mover a janela. Para conferir o visual sem tarefa ativa, clique com o botão direito no ícone ao lado do relógio e escolha **Visualizar bonequinho**.
4. Fechar a janela principal mantém o aplicativo na bandeja. Use **Sair** no menu do ícone para encerrar completamente.

## Executar

Requer Windows e .NET SDK 10 para desenvolvimento.

```powershell
dotnet run --project .\Firaw.WorkAssistant.csproj
```

## Gerar executável portátil

```powershell
dotnet publish .\Firaw.WorkAssistant.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish
```

Abra `publish\Firaw.WorkAssistant.exe`. O aplicativo não lê dados dos outros projetos; o nome do projeto serve para agrupar os lembretes, e o vínculo opcional permite abrir a pasta escolhida.

## Verificação

```powershell
dotnet run --project .\tests\Assistant.Tests.csproj -c Release
```

## Distribuição x64 e x86

Os instaladores são independentes do .NET instalado no computador. O online escolhe a arquitetura do Windows, consulta o mesmo manifesto utilizado pelo Firaw Center e confere tamanho, SHA-256 e identidade da assinatura antes de executar o pacote completo.

- [Completo x64](https://jogos.firawynix.com.br/api/games/firaw-work-assistant/windows/x64/arquivo)
- [Completo x86](https://jogos.firawynix.com.br/api/games/firaw-work-assistant/windows/x86/arquivo)
- [Manifesto de atualização](https://jogos.firawynix.com.br/api/games/firaw-work-assistant/windows/atualizacao.json)

Para gerar os pacotes, instale o .NET SDK 10 e o NSIS no local indicado em `tools/build-release.ps1`, depois execute:

```powershell
.\tools\build-release.ps1
```

A publicação oficial usa `-Sign` e o certificado Firawynix já instalado no cofre do Windows. Chaves e certificados privados não fazem parte deste repositório. Compilações próprias não substituem a identidade da distribuição oficial.

O site é estático, em `site/public`. Os downloads e o vídeo são artefatos de publicação. Dados locais e capturas de prévia não são versionados.
