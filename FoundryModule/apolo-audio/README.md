# Apolo - Áudio ao vivo (módulo para Foundry VTT)

Faz os jogadores ouvirem, dentro do Foundry VTT, a música e os efeitos que o mestre toca no **Apolo**.
O Apolo transmite o áudio por [VDO.Ninja](https://vdo.ninja) e o módulo recebe a transmissão no navegador de cada jogador.
O áudio vai direto do computador do mestre para os jogadores: nada é enviado para o servidor do Foundry.

Testado em: Foundry VTT v14 (declarado compatível a partir da v12).

## 1. Preparar o Apolo (mestre)
1. Instale um cabo de áudio virtual (por exemplo o [VB-CABLE](https://vb-audio.com/Cable/)).
2. No Apolo, escolha esse cabo como saída de áudio (botão de saída no canto direito da barra de menus).
3. Clique no ícone de envio (VDO.Ninja) na barra de menus, ajuste as opções e clique em **Aplicar**.
   A transmissão começa sozinha.
4. Copie o **link do ouvinte** (ou só o ID que aparece no campo "ID da transmissão").

> Dica: para o mestre continuar ouvindo no próprio computador sem eco, deixe a opção
> "O mestre também ouve pelo Foundry" desligada (é o padrão).

## 2. Instalar o módulo no Foundry
1. Copie a pasta `apolo-audio` (ou extraia o `apolo-audio.zip`) para dentro da pasta `Data/modules/` do Foundry.
   Se você usa o Forge ou outra hospedagem, envie a pasta pelo gerenciador de arquivos deles.
2. Reinicie o Foundry, abra o mundo e ative **Apolo - Áudio ao vivo** em *Gerenciar módulos*.

## 3. Configurar (mestre)
Em *Configurações > Configurações dos módulos*:
- **ID da transmissão do Apolo**: cole o link do ouvinte ou o ID.

Pronto. Cada jogador vê um botão **Ouvir o Apolo** na tela:
- depois do primeiro clique em qualquer lugar do Foundry, ele conecta sozinho (os navegadores só liberam o som depois de um clique);
- o botão tem volume e silenciar, só para aquele jogador;
- o botão pode ser arrastado pela alça à esquerda;
- o mestre tem o botão 📢 para pedir que todos comecem a ouvir.

## Observações
- O VDO.Ninja liga cada jogador diretamente ao computador do mestre. Para mesas grandes (mais de 8 pessoas), reduza a "Qualidade (kbps)" no Apolo.
- O atraso é de uma fração de segundo: ótimo para música e ambiente, mas não use para efeitos que precisam bater com uma ação na tela.
- O módulo é gratuito e de código aberto (GPL), como o Apolo.
