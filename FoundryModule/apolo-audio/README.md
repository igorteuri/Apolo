# Apolo - Áudio ao vivo (módulo para Foundry VTT)

Faz os jogadores ouvirem, dentro do Foundry VTT, a música e os efeitos que o mestre toca no **Apolo**.
O Apolo transmite o áudio por [VDO.Ninja](https://vdo.ninja) e o módulo recebe a transmissão no navegador de cada jogador.
O áudio vai direto do computador do mestre para os jogadores: nada é enviado para o servidor do Foundry.

Testado em: Foundry VTT v14 (declarado compatível a partir da v12).

## 1. Preparar o Apolo (mestre)
1. No Apolo, clique no ícone de envio (VDO.Ninja) na barra de menus, ajuste as opções e clique em **Aplicar**.
   A entrada de áudio padrão é o **Apolo Audio**, o microfone virtual do próprio Apolo: ele leva para o VDO.Ninja
   o som que o Apolo toca, qualquer que seja a saída escolhida. Não precisa instalar cabo virtual
   (exige Windows 10 versão 2004 ou Windows 11).
2. Copie o **link do ouvinte** (ou só o ID que aparece no campo "ID da transmissão").

> Dica: para o mestre continuar ouvindo no próprio computador sem eco, deixe a opção
> "O mestre também ouve pelo Foundry" desligada (é o padrão).

## 2. Instalar o módulo no Foundry
1. Copie a pasta `apolo-audio` (ou extraia o `apolo-audio.zip`) para dentro da pasta `Data/modules/` do Foundry.
   Se você usa o Forge ou outra hospedagem, envie a pasta pelo gerenciador de arquivos deles.
2. Reinicie o Foundry, abra o mundo e ative **Apolo - Áudio ao vivo** em *Gerenciar módulos*.

## 3. Configurar (mestre)
Em *Configurações > Configurações dos módulos*:
- **ID da transmissão do Apolo**: cole o link do ouvinte ou o ID.

Pronto. Cada jogador encontra os controles do Apolo na aba **Playlists** da barra lateral, junto dos controles
de volume do próprio Foundry (os jogadores já estão acostumados a ficar ali):
- depois do primeiro clique em qualquer lugar do Foundry, ele conecta sozinho (os navegadores só liberam o som depois de um clique);
- o botão de fone liga/desliga a escuta; há silenciar e volume, só para aquele jogador;
- o mestre tem o botão de megafone para pedir que todos comecem a ouvir.

Cada jogador pode trocar o local em *Configurações dos módulos > Onde mostrar os controles do Apolo*:
seção de volume da aba Playlists (padrão), botão flutuante (arrastável pela alça) ou os dois.
Se o módulo não achar a seção de volume na sua versão do Foundry, ele mostra o botão flutuante sozinho.

## Observações
- O VDO.Ninja liga cada jogador diretamente ao computador do mestre. Para mesas grandes (mais de 8 pessoas), reduza a "Qualidade (kbps)" no Apolo.
- O atraso é de uma fração de segundo: ótimo para música e ambiente, mas não use para efeitos que precisam bater com uma ação na tela.
- O módulo é gratuito e de código aberto (GPL), como o Apolo.
